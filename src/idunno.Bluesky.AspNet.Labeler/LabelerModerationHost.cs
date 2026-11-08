// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Data.Common;
using System.Diagnostics.Metrics;
using System.Security.Cryptography;
using System.Text.Json;

using idunno.AtProto;
using idunno.Bluesky.Labeler;
using idunno.Bluesky.Labeler.Administration;
using idunno.Bluesky.Labeler.Issuance;
using idunno.Bluesky.Labeler.Moderation;
using idunno.Bluesky.Labeler.Storage;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

namespace idunno.Bluesky.AspNet.Labeler;

/// <summary>Bounds independent reporter admission and adapts standard XRPC without accepting team credentials.</summary>
/// <param name="issuer">The bound labeler DID.</param>
/// <param name="httpClient">The caller-owned hardened identity client.</param>
/// <param name="store">The shared signed-label and decision store.</param>
/// <param name="reports">The shared atomic report store.</param>
/// <param name="policyStore">The shared policy gate store.</param>
/// <param name="settings">The validated immutable report limits.</param>
/// <param name="clock">The clock shared by deadlines and admission windows.</param>
/// <param name="logger">The operational logger.</param>
/// <param name="meterFactory">The DI-owned instrumentation factory.</param>
internal sealed partial class LabelerModerationHost(
    Did issuer,
    HttpClient httpClient,
    ILabelStore store,
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

    /// <summary>Observes URI-only evidence after authentication without changing its original subject.</summary>
    private async Task ResolveRecordAsync(LabelReportSubmission submission, CancellationToken token)
    {
        if (submission.Subject is { RecordUri: { } uri, Cid: null })
        {
            submission.RecordResolution = await ReportRecordResolver.ResolveAsync(uri, httpClient, clock, token).ConfigureAwait(false);
            Count("record-" + submission.RecordResolution.Status);
        }
    }

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
                AuthenticationRejected(logger, "com.atproto.moderation.createReport", AuthenticationFailureCategory(exception));
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
                !PolicyVerificationClock.IsRollbackBeyondTolerance(validUntil - storedPolicy.VerificationFreshness, storedPolicy.DatabaseTime)
                    ? storedPolicy.Revision
                    : null;
            await ResolveRecordAsync(submission, linked.Token).ConfigureAwait(false);
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
            DependencyRejected(logger, "com.atproto.moderation.createReport", DependencyFailureCategory(exception));
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

    /// <summary>Accepts an Ozone label appeal routed to this labeler by the reporter's PDS.</summary>
    internal async Task AppealActionedSubjectAsync(HttpContext context)
    {
        context.Response.Headers.CacheControl = "no-store";
        if (!TryAdmit())
        {
            Count("throttled");
            context.Response.Headers.RetryAfter = "60";
            await ErrorAsync(context, "RateLimitExceeded", "Appeal admission is temporarily exhausted.", StatusCodes.Status429TooManyRequests).ConfigureAwait(false);
            return;
        }

        using CancellationTokenSource deadline = new(TimeSpan.FromSeconds(30), clock);
        using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(deadline.Token, context.RequestAborted);
        try
        {
            // Early transport rejection only; service-token authentication and appeal ownership checks follow.
            // Hosts using forwarded headers must trust only legitimate proxies when establishing IsHttps.
            if (!context.Request.IsHttps)
            {
                Rejected(logger, "appeal-https-required");
                Count("transport-rejected");
                await ErrorAsync(context, "HttpsRequired", "HTTPS is required.", StatusCodes.Status403Forbidden).ConfigureAwait(false);
                return;
            }

            // Early request-shape rejection only, not authorization. Authentication, ownership and active-label
            // checks follow; the bounded body reader also rejects oversized bodies regardless of Content-Length.
            if (!context.Request.HasJsonContentType() || context.Request.QueryString.HasValue ||
                context.Request.ContentLength > ModerationWire.MaximumReportBytes)
            {
                Rejected(logger, "invalid-request");
                Count("invalid-request");
                await ErrorAsync(context, "InvalidAppealSubject", "A bounded JSON appeal without query parameters is required.", StatusCodes.Status400BadRequest).ConfigureAwait(false);
                return;
            }

            OzoneAppealRequest appeal;
            try
            {
                byte[] body = await AdministrationWire.ReadAsync(
                    context.Request.Body, ModerationWire.MaximumReportBytes, linked.Token).ConfigureAwait(false);
                appeal = ModerationWire.ParseOzoneAppeal(body);
            }
            catch (NotSupportedException exception)
            {
                Rejected(logger, "not-appealable");
                Count("not-appealable");
                await ErrorAsync(context, "NotAppealable", exception.Message, StatusCodes.Status400BadRequest).ConfigureAwait(false);
                return;
            }
            catch (Exception exception) when (exception is JsonException or ArgumentException or FormatException or InvalidOperationException)
            {
                Rejected(logger, "invalid-request");
                Count("invalid-request");
                await ErrorAsync(context, "InvalidAppealSubject", "The appeal subject or action is invalid.", StatusCodes.Status400BadRequest).ConfigureAwait(false);
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

                authentication = await ServiceAuthentication.VerifyModerationAppealAsync(
                    token: header[7..],
                    labeler: issuer,
                    httpClient: httpClient,
                    timeProvider: clock,
                    maximumLifetime: settings.MaximumServiceTokenLifetime,
                    cancellationToken: linked.Token).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is InvalidDataException or CryptographicException)
            {
                AuthenticationRejected(logger, "tools.ozone.inbox.appealActionedSubject", AuthenticationFailureCategory(exception));
                Count("invalid-token");
                await ErrorAsync(context, "InvalidToken", "Reporter service authentication failed.", StatusCodes.Status401Unauthorized).ConfigureAwait(false);
                return;
            }

            if (authentication.Reporter != appeal.Repository)
            {
                Rejected(logger, "appeal-owner-mismatch");
                await AuditAppealRejectionAsync(authentication, "appeal-owner-mismatch", linked.Token).ConfigureAwait(false);
                Count("authentication-denied");
                await ErrorAsync(context, "InvalidAppealSubject", "Only the affected account may appeal this label.", StatusCodes.Status400BadRequest).ConfigureAwait(false);
                return;
            }

            LabelTarget target = appeal.RecordUri is { } recordUri
                ? LabelTarget.Record(recordUri)
                : LabelTarget.Account(appeal.Repository);
            LabelServiceResult<LabelDecisionSnapshot> decision = await store.ReadDecisionAsync(
                resolvedTarget: target,
                value: appeal.LabelValue,
                cancellationToken: linked.Token).ConfigureAwait(false);
            if (!decision.Succeeded)
            {
                AppealRejected(logger, "label-decision-unavailable", context.TraceIdentifier);
                await AuditAppealRejectionAsync(authentication, "label-decision-unavailable", linked.Token).ConfigureAwait(false);
                Count("unavailable");
                await ErrorAsync(context, "ServiceUnavailable", "The current label decision could not be established.", StatusCodes.Status503ServiceUnavailable).ConfigureAwait(false);
                return;
            }

            StoredLabel? currentLabel = decision.Result.CurrentLabel;
            if (currentLabel is null ||
                currentLabel.SignedLabel.Label.IsNegationLabel ||
                currentLabel.SignedLabel.Label.ExpiresAt is { } expiresAt && expiresAt <= clock.GetUtcNow() ||
                appeal.Cid is { } requestedCid && currentLabel.SignedLabel.Label.Cid is { } labelCid && labelCid != requestedCid)
            {
                string category = currentLabel switch
                {
                    null => "label-missing",
                    _ when currentLabel.SignedLabel.Label.IsNegationLabel => "label-negated",
                    _ when currentLabel.SignedLabel.Label.ExpiresAt is { } expiry && expiry <= clock.GetUtcNow() => "label-expired",
                    _ => "label-cid-mismatch"
                };
                AppealRejected(logger, category, context.TraceIdentifier);
                await AuditAppealRejectionAsync(authentication, category, linked.Token).ConfigureAwait(false);
                Count("not-appealable");
                await ErrorAsync(context, "NotAppealable", "The selected label is not active on this subject.", StatusCodes.Status400BadRequest).ConfigureAwait(false);
                return;
            }

            LabelReportSubmission submission;
            try
            {
                submission = appeal.ToSubmission();
            }
            catch (ArgumentException)
            {
                Rejected(logger, "invalid-request");
                await AuditAppealRejectionAsync(authentication, "appeal-input-invalid", linked.Token).ConfigureAwait(false);
                Count("invalid-request");
                await ErrorAsync(context, "InvalidAppealSubject", "The appeal input is invalid.", StatusCodes.Status400BadRequest).ConfigureAwait(false);
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
                !PolicyVerificationClock.IsRollbackBeyondTolerance(validUntil - storedPolicy.VerificationFreshness, storedPolicy.DatabaseTime)
                    ? storedPolicy.Revision
                    : null;
            await ResolveRecordAsync(submission, linked.Token).ConfigureAwait(false);
            LabelServiceResult<LabelReportIntakeReceipt> result = await reports.AcceptReportAsync(
                authentication: authentication,
                submission: submission,
                expectedPolicyRevision: expectedPolicyRevision,
                maximumPendingReports: settings.MaximumPendingReports,
                cancellationToken: linked.Token).ConfigureAwait(false);
            if (!result.Succeeded)
            {
                string error = result.Error.Code switch
                {
                    LabelServiceErrorCode.Unauthorized or LabelServiceErrorCode.TargetNotAllowed => "InvalidAppealSubject",
                    LabelServiceErrorCode.PendingReportQueueFull => "ServiceUnavailable",
                    _ => "ServiceUnavailable"
                };
                int status = result.Error.Code is LabelServiceErrorCode.Unauthorized or LabelServiceErrorCode.TargetNotAllowed
                    ? StatusCodes.Status400BadRequest
                    : StatusCodes.Status503ServiceUnavailable;
                AppealRejected(logger, result.Error.Code switch
                {
                    LabelServiceErrorCode.Unauthorized => "report-authentication-or-replay-denied",
                    LabelServiceErrorCode.TargetNotAllowed => "policy-subject-denied",
                    LabelServiceErrorCode.PendingReportQueueFull => "pending-queue-full",
                    _ => "appeal-storage-unavailable"
                }, context.TraceIdentifier);
                Count(error == "InvalidAppealSubject" ? "authentication-denied" : "unavailable");
                if (result.Error.Code == LabelServiceErrorCode.PendingReportQueueFull)
                {
                    context.Response.Headers.RetryAfter = "300";
                }

                await ErrorAsync(context, error, result.Error.Message, status).ConfigureAwait(false);
                return;
            }

            Count(result.Result.PendingVerification ? "queued-appeal" : "appeal-accepted");
            DateTimeOffset submittedAt = result.Result.CreatedAt;
            ReportSubjectView subjectView = ModerationWire.ProtocolSubjectView(submission.Subject);
            AppealActionedSubjectOutput output = new(
                Src: issuer.Value,
                Subject: subjectView,
                Enforcement: new("labeled", "labelOnly", [appeal.LabelValue]),
                IsRead: false,
                Appeal: new("pending", submittedAt),
                CreatedAt: submittedAt,
                UpdatedAt: submittedAt);
            context.Response.ContentType = "application/json";
            byte[] response = JsonSerializer.SerializeToUtf8Bytes(
                output, LabelerModerationJsonContext.Default.AppealActionedSubjectOutput);
            await context.Response.Body.WriteAsync(response, context.RequestAborted).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!context.RequestAborted.IsCancellationRequested)
        {
            Rejected(logger, "deadline");
            Count("unavailable");
            await ErrorAsync(context, "ServiceUnavailable", "The appeal deadline ended; a submitted appeal may still have succeeded.", StatusCodes.Status503ServiceUnavailable).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is DbException or HttpRequestException or InvalidDataException)
        {
            DependencyRejected(logger, "tools.ozone.inbox.appealActionedSubject", DependencyFailureCategory(exception));
            Count("unavailable");
            await ErrorAsync(context, "ServiceUnavailable", "A required appeal dependency is unavailable.", StatusCodes.Status503ServiceUnavailable).ConfigureAwait(false);
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
                !PolicyVerificationClock.IsRollbackBeyondTolerance(validUntil - gate.VerificationFreshness, now)
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

    /// <summary>Persists authenticated pre-intake rejection reasons and surfaces audit failures explicitly.</summary>
    private async Task AuditAppealRejectionAsync(
        ModerationReportAuthenticationResult authentication,
        string category,
        CancellationToken token)
    {
        LabelServiceResult<LabelAdministrationReceipt> result =
            await reports.AuditRejectedAppealAsync(authentication, category, token).ConfigureAwait(false);
        if (!result.Succeeded)
        {
            Rejected(logger, result.Error.Code == LabelServiceErrorCode.Unauthorized
                ? "appeal-rejection-audit-authentication-or-replay-denied"
                : "appeal-rejection-audit-unavailable");
            throw new InvalidDataException("The authenticated appeal rejection could not be audited.");
        }
    }

    /// <summary>Allows only known verification categories across the protocol/transport exception boundary.</summary>
    private static string AuthenticationFailureCategory(Exception exception) =>
        exception.Data["ServiceAuthenticationFailure"] switch
        {
            "token-malformed" => "token-malformed",
            "token-header-invalid" => "token-header-invalid",
            "token-audience-mismatch" => "token-audience-mismatch",
            "token-method-mismatch" => "token-method-mismatch",
            "token-issuer-invalid" => "token-issuer-invalid",
            "token-replay-identifier-invalid" => "token-replay-identifier-invalid",
            "token-lifetime-invalid" => "token-lifetime-invalid",
            "token-expired" => "token-expired",
            "token-not-yet-valid" => "token-not-yet-valid",
            "token-signing-key-invalid" => "token-signing-key-invalid",
            "token-algorithm-mismatch" => "token-algorithm-mismatch",
            "token-signature-invalid" => "token-signature-invalid",
            _ => "authorization-header-or-token-invalid"
        };

    /// <summary>Classifies dependency errors without retaining connection strings, URLs or response content.</summary>
    private static string DependencyFailureCategory(Exception exception) => exception switch
    {
        DbException => "database-unavailable",
        HttpRequestException => "identity-or-pds-http-failure",
        _ => "dependency-data-invalid"
    };

    /// <summary>Logs only the fixed method and safe authentication rejection category.</summary>
    [LoggerMessage(2, LogLevel.Warning, "Moderation authentication rejected for {Method}: {Category}.")]
    private static partial void AuthenticationRejected(ILogger logger, string method, string category);

    /// <summary>Logs a specific appeal rejection with its server request correlation identifier.</summary>
    [LoggerMessage(3, LogLevel.Warning, "Moderation appeal rejected: {Category}. Request {RequestId}.")]
    private static partial void AppealRejected(ILogger logger, string category, string requestId);

    /// <summary>Logs dependency classification without exception messages or confidential request contents.</summary>
    [LoggerMessage(4, LogLevel.Error, "Moderation dependency failed for {Method}: {Category}.")]
    private static partial void DependencyRejected(ILogger logger, string method, string category);
}
