// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Data.Common;
using System.Net;
using System.Text.Json;

using idunno.AtProto;
using idunno.AtProto.Repo;
using idunno.Bluesky.Labeler.Issuance;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace idunno.Bluesky.Labeler.Moderation;

/// <summary>Independently verifies declarations and supplies commit-fenced Xenon issuance policies.</summary>
/// <param name="issuer">The bound labeler DID.</param>
/// <param name="store">The shared issuer policy store.</param>
/// <param name="httpClient">The caller-owned hardened identity and repository client.</param>
/// <param name="loggerFactory">An optional logger factory.</param>
/// <exception cref="ArgumentNullException">A required dependency is <see langword="null"/>.</exception>
/// <remarks><para>No repository credentials are accepted or retained. The verification deadline begins
/// before network resolution using the provider's clock. Failed refreshes never extend cached permission.</para></remarks>
public sealed class LabelPolicyService(
    Did issuer,
    ILabelPolicyStore store,
    HttpClient httpClient,
    ILoggerFactory? loggerFactory = null) : ILabelIssuancePolicyProvider
{
    private readonly Did _issuer = issuer ?? throw new ArgumentNullException(nameof(issuer));
    private readonly ILabelPolicyStore _store = store ?? throw new ArgumentNullException(nameof(store));
    private readonly HttpClient _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    private readonly ILoggerFactory _loggerFactory = loggerFactory ?? NullLoggerFactory.Instance;
    private readonly ILogger _logger = (loggerFactory ?? NullLoggerFactory.Instance).CreateLogger<LabelPolicyService>();

    /// <inheritdoc/>
    public async Task<LabelServiceResult<LabelIssuancePolicy>> GetPolicyAsync(CancellationToken cancellationToken = default)
    {
        LabelServiceResult<LabelPolicyState> result = await GetVerifiedStateAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        return result.Succeeded ? LabelServiceResult<LabelIssuancePolicy>.Success(result.Result.Policy!.Issuance)
            : LabelServiceResult<LabelIssuancePolicy>.Failure(result.Error);
    }

    /// <summary>Gets a fresh independently verified gate, optionally bypassing a still-fresh observation.</summary>
    /// <param name="forceVerification"><see langword="true"/> to perform another independent lookup; otherwise, <see langword="false"/>.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A fresh verified gate or an explicit unavailable, conflicted or storage outcome.</returns>
    /// <remarks><para>This is a trusted application service, not a publication-authorization shortcut.
    /// A matching declaration may complete an already-authorized publication intent; detected conflict
    /// requires a new administrator intent even if the external record is subsequently restored.</para></remarks>
    public Task<LabelServiceResult<LabelPolicyState>> GetVerifiedStateAsync(
        bool forceVerification = false,
        CancellationToken cancellationToken = default) =>
        VerifyAsync(forceVerification, false, cancellationToken);

    /// <summary>Refreshes only an active policy; the store revision fences publication changes during remote lookup.</summary>
    internal Task<LabelServiceResult<LabelPolicyState>> VerifyActivePolicyAsync(CancellationToken cancellationToken) =>
        VerifyAsync(true, true, cancellationToken);

    /// <summary>Checks an independent declaration while optionally excluding pending publication from automatic verification.</summary>
    private async Task<LabelServiceResult<LabelPolicyState>> VerifyAsync(
        bool forceVerification,
        bool activeOnly,
        CancellationToken cancellationToken)
    {
        try
        {
            LabelPolicyState? state = await _store.ReadPolicyStateAsync(cancellationToken).ConfigureAwait(false);
            if (activeOnly && state?.Phase != LabelPolicyPhase.VerifiedActive)
            {
                return Reject(
                    code: LabelServiceErrorCode.PolicyUnavailable,
                    message: "Scheduled verification requires an active policy.",
                    category: "PolicyNotActive");
            }
            if (state is not null && state.Issuer != _issuer)
            {
                return Reject(
                    code: LabelServiceErrorCode.PolicyUnavailable,
                    message: "The policy database belongs to a different labeler account.",
                    category: "IssuerMismatch");
            }

            if (state?.Policy is null || state.Phase is LabelPolicyPhase.Conflict or LabelPolicyPhase.Withdrawing)
            {
                return Reject(
                    code: LabelServiceErrorCode.PolicyUnavailable,
                    message: "Publish and check a policy before allowing new reports or label changes.",
                    category: "PolicyNotActive");
            }

            if (state.VerifiedUntil is { } deadline &&
                PolicyVerificationClock.IsRollbackBeyondTolerance(deadline - state.VerificationFreshness, state.DatabaseTime))
            {
                long rollbackMilliseconds = (long)((deadline - state.VerificationFreshness) - state.DatabaseTime).TotalMilliseconds;
                return Reject(
                    code: LabelServiceErrorCode.PolicyUnavailable,
                    message: $"The database clock is {rollbackMilliseconds} milliseconds earlier than the saved policy check. An administrator must investigate before checks can continue.",
                    category: "ClockRollback");
            }

            if (!forceVerification && state.Phase == LabelPolicyPhase.VerifiedActive && state.VerifiedUntil > state.DatabaseTime)
            {
                return LabelServiceResult<LabelPolicyState>.Success(state);
            }

            Uri? pds = await IdentityResolution.ResolvePdsAsync(
                did: _issuer,
                httpClient: _httpClient,
                loggerFactory: _loggerFactory,
                cancellationToken: cancellationToken).ConfigureAwait(false);
            if (pds is null)
            {
                return Reject(
                    code: LabelServiceErrorCode.PolicyUnavailable,
                    message: "The server storing the labeler account's data could not be found.",
                    category: "ServerDiscoveryFailed");
            }

            AtUri uri = new($"at://{_issuer}/app.bsky.labeler.service/self");
            AtProtoHttpResult<AtProtoRepositoryRecord> response = await AtProtoServer.GetRecord(
                repo: _issuer,
                collection: new("app.bsky.labeler.service"),
                rKey: new("self"),
                cid: null,
                service: pds,
                accessCredentials: null,
                httpClient: _httpClient,
                loggerFactory: _loggerFactory,
                cancellationToken: cancellationToken).ConfigureAwait(false);
            bool missing = !response.Succeeded &&
                (response.StatusCode == HttpStatusCode.NotFound || response.AtErrorDetail?.Error == "RecordNotFound");
            if (!response.Succeeded && !missing)
            {
                return Reject(
                    code: LabelServiceErrorCode.PolicyUnavailable,
                    message: "The account's public policy could not be read.",
                    category: "PublicPolicyReadFailed");
            }

            Cid? observedCid;
            string observedJson;
            if (response.Succeeded)
            {
                if (response.Result.Uri != uri || response.Result.Value is null)
                {
                    return Reject(
                        code: LabelServiceErrorCode.PolicyUnavailable,
                        message: "The account server returned a policy that does not match this labeler account.",
                        category: "IssuerMismatch");
                }

                observedCid = response.Result.Cid;
                observedJson = response.Result.Value.ToJsonString();
            }
            else
            {
                observedCid = null;
                observedJson = "null";
            }

            using JsonDocument declaration = JsonDocument.Parse(observedJson);
            LabelServiceResult<LabelPolicyState> observation = await _store.ObservePolicyAsync(
                expectedRevision: state.Revision,
                verificationStartedAt: state.DatabaseTime,
                observedCid: observedCid,
                declaration: declaration.RootElement,
                cancellationToken: cancellationToken).ConfigureAwait(false);
            if (!observation.Succeeded)
            {
                LabelModerationLog.Rejected(_logger, "policy-verification", observation.Error.Code);
                return observation;
            }

            if (observation.Result.Phase == LabelPolicyPhase.VerifiedActive &&
                observation.Result.VerifiedUntil > observation.Result.DatabaseTime)
            {
                return observation;
            }

            bool mismatch = observation.Result.Phase == LabelPolicyPhase.Conflict;
            return Reject(
                code: LabelServiceErrorCode.PolicyUnavailable,
                message: mismatch
                    ? "The account's public policy differs from the selected policy. New reports and label changes remain blocked."
                    : "The policy check did not establish a fresh active verification.",
                category: mismatch ? "PolicyMismatch" : "VerificationExpired");
        }
        catch (HttpRequestException exception)
        {
            return Reject(
                code: LabelServiceErrorCode.PolicyUnavailable,
                message: "The public policy check failed.",
                exception: exception,
                category: "NetworkFailure");
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            return Reject(
                code: LabelServiceErrorCode.PolicyUnavailable,
                message: "The public policy check took too long.",
                exception: exception,
                category: "Timeout");
        }
        catch (JsonException exception)
        {
            return Reject(
                code: LabelServiceErrorCode.PolicyUnavailable,
                message: "The account server returned invalid public policy JSON.",
                exception: exception,
                category: "InvalidPublicPolicy");
        }
        catch (DbException exception)
        {
            return Reject(
                code: LabelServiceErrorCode.StorageUnavailable,
                message: "The declaration verification store is unavailable.",
                exception: exception,
                category: "StorageUnavailable");
        }
    }

    /// <summary>Surfaces a classified failure without logging declaration contents or repository diagnostics.</summary>
    private LabelServiceResult<LabelPolicyState> Reject(
        LabelServiceErrorCode code,
        string message,
        Exception? exception = null,
        string? category = null)
    {
        LabelModerationLog.VerificationRejected(_logger, code, message);
        return LabelServiceResult<LabelPolicyState>.Failure(new(code: code, message: message, exception: exception)
        {
            VerificationFailureCategory = category
        });
    }
}

/// <summary>Applies the same bounded database-clock rollback tolerance to policy verification and admission.</summary>
internal static class PolicyVerificationClock
{
    internal const long RollbackToleranceMilliseconds = 5_000;

    /// <summary>Determines whether database time precedes a verification start by more than the allowed tolerance.</summary>
    internal static bool IsRollbackBeyondTolerance(long verificationStart, long databaseTime) =>
        verificationStart - databaseTime > RollbackToleranceMilliseconds;

    /// <summary>Determines whether database time precedes a verification start by more than the allowed tolerance.</summary>
    internal static bool IsRollbackBeyondTolerance(DateTimeOffset verificationStart, DateTimeOffset databaseTime) =>
        verificationStart - databaseTime > TimeSpan.FromMilliseconds(RollbackToleranceMilliseconds);
}

/// <summary>Logs bounded moderation failure categories without sensitive subjects, reasons or credentials.</summary>
internal static partial class LabelModerationLog
{
    /// <summary>Emits a categorized failure without retaining untrusted exception text.</summary>
    [LoggerMessage(1, LogLevel.Warning, "Moderation service rejected {Operation} with {Code}.")]
    internal static partial void Rejected(ILogger logger, string operation, LabelServiceErrorCode code);

    /// <summary>Emits a locally authored policy verification failure reason without remote data.</summary>
    [LoggerMessage(2, LogLevel.Warning, "Policy verification rejected with {Code}: {Reason}")]
    internal static partial void VerificationRejected(ILogger logger, LabelServiceErrorCode code, string reason);
}
