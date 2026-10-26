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
    public async Task<LabelServiceResult<LabelPolicyState>> GetVerifiedStateAsync(
        bool forceVerification = false,
        CancellationToken cancellationToken = default)
    {
        try
        {
            LabelPolicyState? state = await _store.ReadPolicyStateAsync(cancellationToken).ConfigureAwait(false);
            if (state is not null && state.Issuer != _issuer)
            {
                return Reject(LabelServiceErrorCode.PolicyUnavailable, "The policy store is bound to another issuer.");
            }

            if (state?.Policy is null || state.Phase == LabelPolicyPhase.Conflict)
            {
                return Reject(LabelServiceErrorCode.PolicyUnavailable, "An explicit verified policy publication is required.");
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
                return Reject(LabelServiceErrorCode.PolicyUnavailable, "The issuer's authoritative repository could not be resolved.");
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
                return Reject(LabelServiceErrorCode.PolicyUnavailable, "The authoritative declaration could not be read.");
            }

            Cid? observedCid;
            string observedJson;
            if (response.Succeeded)
            {
                if (response.Result.Uri != uri || response.Result.Value is null)
                {
                    return Reject(LabelServiceErrorCode.PolicyUnavailable, "The repository did not return the exact issuer declaration.");
                }

                observedCid = response.Result.Cid;
                try
                {
                    observedJson = response.Result.Value.ToJsonString();
                    using JsonDocument remote = JsonDocument.Parse(observedJson);
                    _ = LabelPolicyDefinition.CanonicalDeclaration(remote.RootElement);
                }
                catch (ArgumentException exception)
                {
                    return Reject(LabelServiceErrorCode.PolicyUnavailable, "The authoritative declaration is invalid or exceeds its bounds.", exception);
                }
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

            return observation.Result.Phase == LabelPolicyPhase.VerifiedActive &&
                observation.Result.VerifiedUntil > observation.Result.DatabaseTime
                ? observation : Reject(LabelServiceErrorCode.PolicyUnavailable, "The declaration differs from the selected policy; the gate remains closed.");
        }
        catch (HttpRequestException exception)
        {
            return Reject(LabelServiceErrorCode.PolicyUnavailable, "Declaration verification failed.", exception);
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            return Reject(LabelServiceErrorCode.PolicyUnavailable, "Declaration verification timed out.", exception);
        }
        catch (JsonException exception)
        {
            return Reject(LabelServiceErrorCode.PolicyUnavailable, "The declaration response was not valid JSON.", exception);
        }
        catch (DbException exception)
        {
            return Reject(LabelServiceErrorCode.StorageUnavailable, "The declaration verification store is unavailable.", exception);
        }
    }

    /// <summary>Surfaces a classified failure without logging declaration contents or repository diagnostics.</summary>
    private LabelServiceResult<LabelPolicyState> Reject(LabelServiceErrorCode code, string message, Exception? exception = null)
    {
        LabelModerationLog.Rejected(_logger, "policy-verification", code);
        return LabelServiceResult<LabelPolicyState>.Failure(new(code: code, message: message, exception: exception));
    }
}

/// <summary>Logs bounded moderation failure categories without sensitive subjects, reasons or credentials.</summary>
internal static partial class LabelModerationLog
{
    /// <summary>Emits a categorized failure without retaining untrusted exception text.</summary>
    [LoggerMessage(1, LogLevel.Warning, "Moderation service rejected {Operation} with {Code}.")]
    internal static partial void Rejected(ILogger logger, string operation, LabelServiceErrorCode code);
}
