// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Data.Common;
using System.Security.Cryptography;

using idunno.AtProto;
using idunno.AtProto.Labels;
using idunno.Bluesky.Labeler.Maintenance;
using idunno.Bluesky.Labeler.Storage;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace idunno.Bluesky.Labeler.Issuance;

/// <summary>Authorizes, reconciles, signs and durably queues label decisions independently of a host.</summary>
/// <param name="issuer">The bound issuer DID.</param>
/// <param name="signingKey">The established caller-owned signing key.</param>
/// <param name="store">The initialized decision storage provider.</param>
/// <param name="authorizer">The required authorization implementation.</param>
/// <param name="policyProvider">The active issuance-policy provider.</param>
/// <param name="httpClient">The configured caller-owned HTTP client.</param>
/// <param name="timeProvider">An optional service clock.</param>
/// <param name="loggerFactory">An optional logger factory.</param>
/// <exception cref="ArgumentNullException">A required dependency is <see langword="null"/>.</exception>
/// <remarks>
/// <para>Receipts represent committed decisions, not network delivery. A separate trusted hosting worker
/// advances publication. The service borrows its dependencies and never creates replacement keys.</para>
/// <para>The caller context must come from a trusted host. The authorizer is checked on every invocation;
/// its version is audit evidence, not an atomic team-revocation fence. Administrative hosting must
/// integrate commit-valid authorization with the team provider before exposing mutation endpoints.</para>
/// <para>The host owns key/client disposal. Storage initialization and publication are separate operations.</para>
/// </remarks>
public sealed partial class LabelIssuanceService(
    Did issuer,
    LabelSigningKey signingKey,
    ILabelStore store,
    ILabelIssuanceAuthorizer authorizer,
    ILabelIssuancePolicyProvider policyProvider,
    HttpClient httpClient,
    TimeProvider? timeProvider = null,
    ILoggerFactory? loggerFactory = null)
{
    private readonly Did _issuer = issuer ?? throw new ArgumentNullException(nameof(issuer));
    private readonly LabelSigningKey _signingKey = signingKey ?? throw new ArgumentNullException(nameof(signingKey));
    private readonly ILabelStore _store = store ?? throw new ArgumentNullException(nameof(store));
    private readonly ILabelIssuanceAuthorizer _authorizer = authorizer ?? throw new ArgumentNullException(nameof(authorizer));
    private readonly ILabelIssuancePolicyProvider _policyProvider = policyProvider ?? throw new ArgumentNullException(nameof(policyProvider));
    private readonly LabelTargetResolver _resolver = new(
        httpClient ?? throw new ArgumentNullException(nameof(httpClient)),
        loggerFactory ?? NullLoggerFactory.Instance);
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;
    private readonly ILogger _logger = (loggerFactory ?? NullLoggerFactory.Instance).CreateLogger<LabelIssuanceService>();

    /// <summary>Executes an issue or negation request, returning its original receipt on an identical retry.</summary>
    /// <param name="request">The immutable decision and operation identity.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A committed receipt or an actionable failure.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">The store is bound to another issuer.</exception>
    /// <exception cref="OperationCanceledException">Cancellation occurred before commit submission.</exception>
    /// <remarks><para>A successful no-op is audited but allocates no event sequence. An uncertain commit
    /// must be recovered by its original operation identity, not replaced by another ID.</para></remarks>
    public async Task<LabelServiceResult<LabelOperationReceipt>> ExecuteAsync(
        LabelOperationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        LabelServiceResult<LabelOperationReceipt> result = await ExecuteCoreAsync(request, cancellationToken).ConfigureAwait(false);
        return Report(result, request.Kind.ToString());
    }

    /// <summary>Recovers prior intent or authorizes, resolves, reconciles and signs a new atomic decision.</summary>
    /// <remarks><para>Receipt recovery precedes network and policy work so a retry cannot change a
    /// committed target or signature. The provider rechecks the observed revision when committing.</para></remarks>
    private async Task<LabelServiceResult<LabelOperationReceipt>> ExecuteCoreAsync(LabelOperationRequest request, CancellationToken token)
    {
        LabelAuthorizationAction action = request.Kind == LabelOperationKind.Issue
            ? LabelAuthorizationAction.Issue : LabelAuthorizationAction.Negate;
        LabelServiceResult<LabelAuthorizationGrant> admission = await AuthorizeAsync(
            action: action,
            context: request.Context,
            operationId: request.OperationId,
            requested: request.Target,
            resolved: null,
            value: request.Value,
            admission: true,
            token: token).ConfigureAwait(false);
        if (!admission.Succeeded)
        {
            return LabelServiceResult<LabelOperationReceipt>.Failure(admission.Error);
        }

        // Recover before network resolution, policy lookup, clock sampling or signing. Handles can change
        // and ECDSA signatures can differ, so neither is suitable for reconstructing a committed retry.
        LabelServiceResult<LabelOperationReceipt> recovered = await _store.GetOperationAsync(request.OperationId, token).ConfigureAwait(false);
        if (recovered.Succeeded)
        {
            return await AuthorizeReceiptAsync(
                request: request,
                action: action,
                receipt: recovered,
                token: token).ConfigureAwait(false);
        }

        if (recovered.Error.Code != LabelServiceErrorCode.OperationNotFound)
        {
            return recovered;
        }

        LabelServiceResult<LabelDecisionCommit> prepared = await PrepareDecisionAsync(request, token).ConfigureAwait(false);
        if (!prepared.Succeeded)
        {
            return LabelServiceResult<LabelOperationReceipt>.Failure(prepared.Error);
        }

        LabelDecisionCommit commit = prepared.Result;
        LabelServiceResult<LabelOperationReceipt> committed = await _store.CommitDecisionAsync(commit, token).ConfigureAwait(false);
        if (committed.Succeeded)
        {
            CheckIssuer(committed.Result.StateAtCommit);
            // A concurrent same-ID writer can win after our initial lookup, including a handle
            // resolving differently. Authorize the stored target, never merely our discarded one.
            if (committed.Result.ResolvedTarget.Subject == commit.ResolvedTarget.Subject &&
                committed.Result.ResolvedTarget.Cid == commit.ResolvedTarget.Cid)
            {
                return committed;
            }

            try
            {
                return await AuthorizeReceiptAsync(
                    request: request,
                    action: action,
                    receipt: committed,
                    token: token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                return Failure<LabelOperationReceipt>(
                    LabelServiceErrorCode.OutcomeUnknown,
                    "The operation committed but receipt authorization was cancelled; recover the original operation ID.");
            }
        }

        if (committed.Error.Code is LabelServiceErrorCode.OutcomeUnknown or LabelServiceErrorCode.StaleDecision &&
            !token.IsCancellationRequested)
        {
            // Recovery is bounded to one read and never converts absence/unavailability into rollback.
            // Cancellation during recovery must preserve the uncertain submitted outcome.
            try
            {
                LabelServiceResult<LabelOperationReceipt> receipt = await _store.GetOperationAsync(request.OperationId, token).ConfigureAwait(false);
                if (receipt.Succeeded)
                {
                    return await AuthorizeReceiptAsync(
                        request: request,
                        action: action,
                        receipt: receipt,
                        token: token).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                return committed;
            }
        }

        return committed;
    }

    /// <summary>Prepares one resolved, authorized and signed decision without writing labels or receipts.</summary>
    /// <remarks><para>The caller must perform coarse admission and same-intent receipt recovery first.
    /// Compound workflows reuse this preparation before submitting all actions in one provider transaction.</para></remarks>
    internal async Task<LabelServiceResult<LabelDecisionCommit>> PrepareDecisionAsync(LabelOperationRequest request, CancellationToken token)
    {
        LabelAuthorizationAction action = request.Kind == LabelOperationKind.Issue
            ? LabelAuthorizationAction.Issue : LabelAuthorizationAction.Negate;
        LabelTarget resolved;
        if (request.Kind == LabelOperationKind.Negate)
        {
            if (request.Target.Repository is not Did)
            {
                return Failure<LabelDecisionCommit>(
                    LabelServiceErrorCode.InvalidTarget,
                    "Withdrawal requires a stable DID target and does not resolve handles.");
            }

            resolved = request.Target;
        }
        else
        {
            LabelServiceResult<LabelTarget> resolution = await _resolver.ResolveAsync(request.Target, token).ConfigureAwait(false);
            if (!resolution.Succeeded)
            {
                return LabelServiceResult<LabelDecisionCommit>.Failure(resolution.Error);
            }

            resolved = resolution.Result;
        }

        LabelServiceResult<LabelAuthorizationGrant> grant = await AuthorizeAsync(
            action: action,
            context: request.Context,
            operationId: request.OperationId,
            requested: request.Target,
            resolved: resolved,
            value: request.Value,
            admission: false,
            token: token).ConfigureAwait(false);
        if (!grant.Succeeded)
        {
            return LabelServiceResult<LabelDecisionCommit>.Failure(grant.Error);
        }

        string? policyVersion = null;
        if (request.Kind == LabelOperationKind.Issue)
        {
            LabelServiceResult<LabelIssuancePolicy> policy = await _policyProvider.GetPolicyAsync(token).ConfigureAwait(false);
            if (!policy.Succeeded)
            {
                return LabelServiceResult<LabelDecisionCommit>.Failure(policy.Error);
            }

            policyVersion = policy.Result.Version;
            if (!policy.Result.DeclaredValues.Contains(request.Value, StringComparer.Ordinal))
            {
                return Failure<LabelDecisionCommit>(
                    LabelServiceErrorCode.ValueNotDeclared,
                    "The requested label value is not declared by the active policy.");
            }

            if ((resolved.Kind == LabelTargetKind.Account && !policy.Result.AllowAccounts) ||
                (resolved.Kind == LabelTargetKind.Record && (!policy.Result.AllowRecords ||
                    (policy.Result.AllowedRecordCollections is { } collections && !collections.Contains(resolved.RecordUri!.Collection!)))))
            {
                return Failure<LabelDecisionCommit>(
                    LabelServiceErrorCode.TargetNotAllowed,
                    "The active issuance policy does not allow this target.");
            }
        }

        LabelServiceResult<LabelDecisionSnapshot> snapshot = await _store.ReadDecisionAsync(resolved, request.Value, token).ConfigureAwait(false);
        if (!snapshot.Succeeded)
        {
            return LabelServiceResult<LabelDecisionCommit>.Failure(snapshot.Error);
        }

        CheckIssuer(snapshot.Result.State);
        if (snapshot.Result.State.Phase != LabelerLifecyclePhase.Active)
        {
            return Failure<LabelDecisionCommit>(
                LabelServiceErrorCode.IssuanceClosed,
                "Ordinary issuance and negation are closed by the labeler lifecycle.");
        }

        StoredLabel? current = snapshot.Result.CurrentLabel;
        if (request.Kind == LabelOperationKind.Negate)
        {
            if (request.Target.Cid is not null && (current is null || request.Target.Cid != current.SignedLabel.Label.Cid))
            {
                return Failure<LabelDecisionCommit>(
                    LabelServiceErrorCode.StaleDecision,
                    "The withdrawal CID does not match the current stored decision.");
            }

            if (resolved.RecordUri is { } uri &&
                current is not null &&
                resolved.Cid != current.SignedLabel.Label.Cid)
            {
                resolved = LabelTarget.Record(uri, current.SignedLabel.Label.Cid);
                // Copying a stored CID changes the authorized scope, including for audited no-ops.
                // Use the final grant in the audit and reject denial before signing or committing.
                grant = await AuthorizeAsync(
                    action: action,
                    context: request.Context,
                    operationId: request.OperationId,
                    requested: request.Target,
                    resolved: resolved,
                    value: request.Value,
                    admission: false,
                    token: token).ConfigureAwait(false);
                if (!grant.Succeeded)
                {
                    return LabelServiceResult<LabelDecisionCommit>.Failure(grant.Error);
                }
            }
        }

        DateTimeOffset now = _timeProvider.GetUtcNow();
        SignedLabelSnapshot? signed = null;
        string? publicKey = null;
        if (LabelDecisionRules.NeedsLabel(request, current, now))
        {
            DateTimeOffset created = now;
            if (current is not null && created <= current.SignedLabel.Label.CreationTimestamp)
            {
                DateTimeOffset previous = current.SignedLabel.Label.CreationTimestamp.ToUniversalTime();
                if (previous == DateTimeOffset.MaxValue)
                {
                    return Failure<LabelDecisionCommit>(
                        LabelServiceErrorCode.TimestampExhausted,
                        "No representable creation instant is later than the current decision.");
                }

                // One tick beyond the truncated value is also strictly beyond any retained sub-tick
                // fraction. The provider rechecks exact timestamp ordering under its stream lock.
                created = previous.AddTicks(1);
            }

            if (request.ExpiresAt is { } expiry && expiry <= created)
            {
                return Failure<LabelDecisionCommit>(
                    LabelServiceErrorCode.InvalidExpiry,
                    "Expiry must be later than the final service-assigned creation timestamp.");
            }

            try
            {
                Label label = request.Kind == LabelOperationKind.Issue
                    ? _signingKey.CreateLabel(
                        source: _issuer,
                        uri: resolved.Subject,
                        value: request.Value,
                        creationTimestamp: created,
                        cid: resolved.Cid,
                        expiresAt: request.ExpiresAt)
                    : _signingKey.CreateNegation(
                        source: _issuer,
                        uri: resolved.Subject,
                        value: request.Value,
                        creationTimestamp: created,
                        cid: resolved.Cid);
                signed = new(label);
                publicKey = _signingKey.ExportPublicKeyMultibase();
            }
            catch (Exception exception) when (exception is CryptographicException or PlatformNotSupportedException)
            {
                return LabelServiceResult<LabelDecisionCommit>.Failure(
                    new(
                        code: LabelServiceErrorCode.SigningFailed,
                        message: "The established signing key could not sign this decision.",
                        exception: exception));
            }
        }

        LabelAuditRecord audit = new(
            evaluatedAt: now,
            authorizationVersion: grant.Result.Version,
            policyVersion: policyVersion,
            signingKeyMultibase: publicKey);
        LabelDecisionCommit commit = new(
            request: request,
            resolvedTarget: resolved,
            expectedRevision: snapshot.Result.State.Revision,
            signedLabel: signed,
            audit: audit)
        {
            AdministrationCaller = grant.Result.AdministrationCaller
        };
        return LabelServiceResult<LabelDecisionCommit>.Success(commit);
    }

    /// <summary>Checks recovered scope before returning any original outcome or intent conflict.</summary>
    private async Task<LabelServiceResult<LabelOperationReceipt>> AuthorizeReceiptAsync(
        LabelOperationRequest request,
        LabelAuthorizationAction action,
        LabelServiceResult<LabelOperationReceipt> receipt,
        CancellationToken token)
    {
        CheckIssuer(receipt.Result!.StateAtCommit);
        LabelServiceResult<LabelAuthorizationGrant> authorized = await AuthorizeAsync(
            action: action,
            context: request.Context,
            operationId: request.OperationId,
            requested: request.Target,
            resolved: receipt.Result.ResolvedTarget,
            value: receipt.Result.Request.Value,
            admission: false,
            token: token).ConfigureAwait(false);
        if (!authorized.Succeeded)
        {
            return LabelServiceResult<LabelOperationReceipt>.Failure(authorized.Error);
        }

        return receipt.Result.Request.SemanticallyEquals(request) ? receipt :
            Failure<LabelOperationReceipt>(
                LabelServiceErrorCode.IdempotencyConflict,
                "The operation ID already identifies different intent; recover its original receipt.");
    }

    /// <summary>Recovers a retained service receipt after authorizing the current caller to inspect it.</summary>
    /// <param name="operationId">The original service-operation identity.</param>
    /// <param name="context">The current trusted caller context.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The original receipt or an explicit lookup/authorization failure.</returns>
    /// <exception cref="ArgumentException"><paramref name="operationId"/> is invalid.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is <see langword="null"/>.</exception>
    public async Task<LabelServiceResult<LabelOperationReceipt>> GetOperationAsync(
        string operationId,
        LabelOperationContext context,
        CancellationToken cancellationToken = default)
    {
        LabelInput.ValidateText(operationId, nameof(operationId), 256);
        ArgumentNullException.ThrowIfNull(context);
        LabelServiceResult<LabelAuthorizationGrant> admission = await AuthorizeAsync(
            action: LabelAuthorizationAction.InspectOperation,
            context: context,
            operationId: operationId,
            requested: null,
            resolved: null,
            value: null,
            admission: true,
            token: cancellationToken).ConfigureAwait(false);
        if (!admission.Succeeded)
        {
            return Report(LabelServiceResult<LabelOperationReceipt>.Failure(admission.Error), "inspect-operation");
        }

        LabelServiceResult<LabelOperationReceipt> receipt = await _store.GetOperationAsync(operationId, cancellationToken).ConfigureAwait(false);
        if (receipt.Succeeded)
        {
            CheckIssuer(receipt.Result.StateAtCommit);
            LabelServiceResult<LabelAuthorizationGrant> grant = await AuthorizeAsync(
                action: LabelAuthorizationAction.InspectOperation,
                context: context,
                operationId: operationId,
                requested: receipt.Result.Request.Target,
                resolved: receipt.Result.ResolvedTarget,
                value: receipt.Result.Request.Value,
                admission: false,
                token: cancellationToken).ConfigureAwait(false);
            if (!grant.Succeeded)
            {
                return Report(LabelServiceResult<LabelOperationReceipt>.Failure(grant.Error), "inspect-operation");
            }
        }

        return Report(receipt, "inspect-operation");
    }

    /// <summary>Reads current local publication boundaries after authorizing the caller.</summary>
    /// <param name="context">The current trusted caller context.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The durable state or an actionable failure.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">The store is bound to another issuer.</exception>
    public async Task<LabelServiceResult<LabelStoreState>> GetPublicationStateAsync(
        LabelOperationContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        LabelServiceResult<LabelAuthorizationGrant> grant = await AuthorizeAsync(
            action: LabelAuthorizationAction.InspectPublication,
            context: context,
            operationId: null,
            requested: null,
            resolved: null,
            value: null,
            admission: false,
            token: cancellationToken).ConfigureAwait(false);
        if (!grant.Succeeded)
        {
            return Report(LabelServiceResult<LabelStoreState>.Failure(grant.Error), "inspect-publication");
        }

        try
        {
            LabelStoreState state = await _store.GetStateAsync(cancellationToken).ConfigureAwait(false);
            CheckIssuer(state);
            return LabelServiceResult<LabelStoreState>.Success(state);
        }
        catch (DbException exception)
        {
            return Report(
                LabelServiceResult<LabelStoreState>.Failure(
                    new(
                        code: LabelServiceErrorCode.StorageUnavailable,
                        message: "Publication state could not be read; restore database availability.",
                        exception: exception)),
                "inspect-publication");
        }
    }

    /// <summary>Builds the host authorizer's scope, distinguishing coarse admission from a resolved-target check.</summary>
    private Task<LabelServiceResult<LabelAuthorizationGrant>> AuthorizeAsync(
        LabelAuthorizationAction action,
        LabelOperationContext context,
        string? operationId,
        LabelTarget? requested,
        LabelTarget? resolved,
        string? value,
        bool admission,
        CancellationToken token) =>
        _authorizer.AuthorizeAsync(
            new(
                issuer: _issuer,
                action: action,
                context: context,
                operationId: operationId,
                requestedTarget: requested,
                resolvedTarget: resolved,
                value: value,
                isAdmissionCheck: admission),
            token);

    /// <summary>Prevents a misconfigured service from operating on another issuer's persistent stream.</summary>
    private void CheckIssuer(LabelStoreState state)
    {
        if (!string.Equals(state.Issuer, _issuer.Value, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The configured service issuer differs from the bound label store.");
        }
    }

    /// <summary>Logs only the failure category while preserving the full diagnostic result for its caller.</summary>
    private LabelServiceResult<T> Report<T>(LabelServiceResult<T> result, string operation) where T : class
    {
        if (!result.Succeeded)
        {
            LabelIssuanceLog.Rejected(_logger, operation, result.Error.Code);
        }

        return result;
    }

    /// <summary>Creates a classified failure for a service rule that has no underlying transport exception.</summary>
    private static LabelServiceResult<T> Failure<T>(LabelServiceErrorCode code, string message) where T : class =>
        LabelServiceResult<T>.Failure(
            new(
                code: code,
                message: message));
}
