// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Data.Common;
using System.Diagnostics.Metrics;
using System.Security.Cryptography;
using System.Text.Json;

using idunno.AtProto;
using idunno.Bluesky.Labeler;
using idunno.Bluesky.Labeler.Moderation;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

namespace idunno.Bluesky.AspNet.Labeler;

/// <summary>Bounds independent reporter admission and adapts standard XRPC without accepting team credentials.</summary>
/// <param name="issuer">The bound labeler DID.</param>
/// <param name="httpClient">The caller-owned hardened identity client.</param>
/// <param name="reports">The shared atomic report store.</param>
/// <param name="policyStore">The shared policy gate store.</param>
/// <param name="settings">The validated immutable report limits.</param>
/// <param name="clock">The clock shared by deadlines and admission windows.</param>
/// <param name="logger">The operational logger.</param>
/// <param name="meterFactory">The DI-owned instrumentation factory.</param>
internal sealed partial class LabelerModerationHost(
    Did issuer,
    HttpClient httpClient,
    ILabelReportStore reports,
    ILabelPolicyStore policyStore,
    LabelerModerationOptions settings,
    TimeProvider clock,
    ILogger<LabelerModerationHost> logger,
    IMeterFactory meterFactory) : IHealthCheck, IDisposable
{
    internal const string MeterName = "idunno.Bluesky.AspNet.Labeler.Moderation";
#if NET9_0_OR_GREATER
    private readonly Lock _admissionLock = new();
#else
    private readonly object _admissionLock = new();
#endif
    private long _windowStarted = clock.GetTimestamp();
    private int _windowCount;
    private int _active;
    private readonly Meter _meter = meterFactory.Create(MeterName);
    private Counter<long>? _requests;

    /// <summary>Releases this component's instruments with its owning service provider.</summary>
    public void Dispose() => _meter.Dispose();

    /// <summary>Admits and handles one bounded request without appending unauthenticated flood traffic to audit.</summary>
    internal async Task ReportAsync(HttpContext context)
    {
        context.Response.Headers.CacheControl = "no-store";
        if (!TryAdmit())
        {
            Count("throttled");
            context.Response.Headers.RetryAfter = "60";
            await ErrorAsync(context, "RateLimitExceeded", "Report admission is temporarily exhausted.", StatusCodes.Status429TooManyRequests).ConfigureAwait(false);
            return;
        }

        using CancellationTokenSource deadline = new(TimeSpan.FromSeconds(30), clock);
        using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(deadline.Token, context.RequestAborted);
        try
        {
            if (!context.Request.IsHttps)
            {
                Count("transport-rejected");
                await ErrorAsync(context, "HttpsRequired", "HTTPS is required.", StatusCodes.Status403Forbidden).ConfigureAwait(false);
                return;
            }

            if (!context.Request.HasJsonContentType() || context.Request.QueryString.HasValue ||
                context.Request.ContentLength > ModerationWire.MaximumReportBytes)
            {
                Count("invalid-request");
                await ErrorAsync(context, "InvalidRequest", "A bounded JSON report without query parameters is required.", StatusCodes.Status400BadRequest).ConfigureAwait(false);
                return;
            }

            LabelReportSubmission submission;
            try
            {
                byte[] body = await AdministrationWire.ReadAsync(
                    context.Request.Body, ModerationWire.MaximumReportBytes, linked.Token).ConfigureAwait(false);
                submission = ModerationWire.ParseReport(body);
            }
            catch (Exception exception) when (exception is JsonException or ArgumentException or FormatException)
            {
                Rejected(logger, "invalid-request");
                Count("invalid-request");
                await ErrorAsync(context, "InvalidRequest", "The report input is invalid or exceeds its bounds.", StatusCodes.Status400BadRequest).ConfigureAwait(false);
                return;
            }

            ModerationReportAuthenticationResult authentication;
            try
            {
                string header = context.Request.Headers.Authorization.ToString();
                if (!header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) || header.Length > 8199)
                {
                    throw new InvalidDataException("A service token is required.");
                }

                authentication = await ServiceAuthentication.VerifyModerationReportAsync(
                    token: header[7..],
                    labeler: issuer,
                    httpClient: httpClient,
                    timeProvider: clock,
                    maximumLifetime: settings.MaximumServiceTokenLifetime,
                    cancellationToken: linked.Token).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is InvalidDataException or CryptographicException)
            {
                Rejected(logger, "invalid-token");
                Count("invalid-token");
                await ErrorAsync(context, "InvalidToken", "Reporter service authentication failed.", StatusCodes.Status401Unauthorized).ConfigureAwait(false);
                return;
            }

            LabelPolicyState? storedPolicy = await policyStore.ReadPolicyStateAsync(linked.Token).ConfigureAwait(false);
            long? expectedPolicyRevision = storedPolicy is
            {
                Phase: LabelPolicyPhase.VerifiedActive,
                VerifiedUntil: { } validUntil
            } &&
                storedPolicy.Issuer == issuer &&
                storedPolicy.VerificationFreshness == settings.VerificationFreshness &&
                storedPolicy.DatabaseTime < validUntil &&
                storedPolicy.DatabaseTime >= validUntil - storedPolicy.VerificationFreshness
                    ? storedPolicy.Revision
                    : null;
            LabelServiceResult<LabelReportIntakeReceipt> result = await reports.AcceptReportAsync(
                authentication: authentication,
                submission: submission,
                expectedPolicyRevision: expectedPolicyRevision,
                maximumPendingReports: settings.MaximumPendingReports,
                cancellationToken: linked.Token).ConfigureAwait(false);
            if (!result.Succeeded)
            {
                string category = result.Error.Code switch
                {
                    LabelServiceErrorCode.Unauthorized => "authentication-denied",
                    LabelServiceErrorCode.TargetNotAllowed => "scope-denied",
                    LabelServiceErrorCode.PendingReportQueueFull => "pending-queue-full",
                    _ => "unavailable"
                };
                Rejected(logger, category);
                Count(category);
                if (result.Error.Code == LabelServiceErrorCode.PendingReportQueueFull)
                {
                    context.Response.Headers.RetryAfter = "300";
                }
                int status = result.Error.Code switch
                {
                    LabelServiceErrorCode.Unauthorized => StatusCodes.Status401Unauthorized,
                    LabelServiceErrorCode.TargetNotAllowed => StatusCodes.Status400BadRequest,
                    _ => StatusCodes.Status503ServiceUnavailable
                };
                string error = status switch
                {
                    StatusCodes.Status401Unauthorized => "InvalidToken",
                    StatusCodes.Status400BadRequest => "InvalidRequest",
                    _ => "ServiceUnavailable"
                };
                await ErrorAsync(context, error, result.Error.Message, status).ConfigureAwait(false);
                return;
            }

            Count(result.Result.PendingVerification ? "queued-policy" : "accepted");
            context.Response.ContentType = "application/json";
            byte[] output = JsonSerializer.SerializeToUtf8Bytes(
                ModerationWire.ReportOutput(result.Result, submission, authentication.Reporter),
                LabelerModerationJsonContext.Default.ReportOutput);
            await context.Response.Body.WriteAsync(output, context.RequestAborted).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!context.RequestAborted.IsCancellationRequested)
        {
            Rejected(logger, "deadline");
            Count("unavailable");
            await ErrorAsync(context, "ServiceUnavailable", "The report deadline ended; a submitted commit may still have succeeded.", StatusCodes.Status503ServiceUnavailable).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is DbException or HttpRequestException or InvalidDataException)
        {
            Rejected(logger, "dependency-unavailable");
            Count("unavailable");
            await ErrorAsync(context, "ServiceUnavailable", "A required report dependency is unavailable.", StatusCodes.Status503ServiceUnavailable).ConfigureAwait(false);
        }
        finally
        {
            lock (_admissionLock)
            {
                _active--;
            }
        }
    }

    /// <summary>Checks only enabled moderation dependencies; public label distribution has an independent health check.</summary>
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        using CancellationTokenSource deadline = new(TimeSpan.FromSeconds(30), clock);
        using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(deadline.Token, cancellationToken);
        try
        {
            LabelPolicyState? gate = await policyStore.ReadPolicyStateAsync(linked.Token).ConfigureAwait(false);
            if (gate is null || gate.VerificationFreshness != settings.VerificationFreshness)
            {
                return HealthCheckResult.Unhealthy("Moderation policy initialization is missing or inconsistent across hosts.");
            }

            DateTimeOffset now = gate.DatabaseTime;
            return gate.Phase == LabelPolicyPhase.VerifiedActive &&
                gate.VerifiedUntil is { } validUntil && now < validUntil &&
                now >= validUntil - gate.VerificationFreshness
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy("Moderation has no fresh independently verified declaration.");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            Rejected(logger, "health-deadline");
            return HealthCheckResult.Unhealthy("Reading moderation policy health exceeded its deadline.");
        }
        catch (Exception exception) when (exception is DbException or InvalidDataException)
        {
            Rejected(logger, "health-unavailable");
            return HealthCheckResult.Unhealthy("Moderation storage is unavailable.");
        }
    }

    /// <summary>Limits both in-flight work and per-minute authentication/audit load using the injected monotonic clock.</summary>
    private bool TryAdmit()
    {
        lock (_admissionLock)
        {
            long now = clock.GetTimestamp();
            if (clock.GetElapsedTime(_windowStarted, now) >= TimeSpan.FromMinutes(1))
            {
                _windowStarted = now;
                _windowCount = 0;
            }

            if (_active >= settings.MaximumConcurrentReports || _windowCount >= settings.ReportsPerMinute)
            {
                return false;
            }

            _active++;
            _windowCount++;
            return true;
        }
    }

    /// <summary>Records only server-selected, bounded categories without subjects, reporters or evidence.</summary>
    private void Count(string outcome)
    {
        Counter<long> counter;
        lock (_admissionLock)
        {
            counter = _requests ??= _meter.CreateCounter<long>("idunno.bluesky.labeler.moderation.reports", "{request}");
        }

        counter.Add(1, new KeyValuePair<string, object?>("outcome", outcome));
    }

    /// <summary>Writes only the standard error shape and explicit transport status.</summary>
    private static async Task ErrorAsync(HttpContext context, string error, string message, int status)
    {
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/json";
        if (status == StatusCodes.Status401Unauthorized)
        {
            context.Response.Headers.WWWAuthenticate = "Bearer";
        }

        byte[] output = JsonSerializer.SerializeToUtf8Bytes(
            new ModerationXrpcError(error, message), LabelerModerationJsonContext.Default.ModerationXrpcError);
        await context.Response.Body.WriteAsync(output, context.RequestAborted).ConfigureAwait(false);
    }

    /// <summary>Logs fixed failure categories without bearer tokens, report text or exception input.</summary>
    [LoggerMessage(1, LogLevel.Warning, "Moderation request rejected: {Category}.")]
    private static partial void Rejected(ILogger logger, string category);
}
