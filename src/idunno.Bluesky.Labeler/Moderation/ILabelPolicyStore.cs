// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;

using idunno.AtProto;
using idunno.Bluesky.Labeler.Administration;

namespace idunno.Bluesky.Labeler.Moderation;

/// <summary>Persists immutable policies and commit-valid declaration gates on the shared issuer store.</summary>
/// <remarks><para>This is trusted application infrastructure, not an HTTP contract. Verification
/// observations must originate from an independent trusted PDS reader, never an administrative request
/// body's assertion of success. Publication credentials are not stored here.</para></remarks>
public interface ILabelPolicyStore
{
    /// <summary>Reads a retained immutable policy version after checking live inspection authority.</summary>
    /// <param name="caller">The provider-issued viewer or higher.</param>
    /// <param name="version">The exact policy version.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The retained policy or a classified failure.</returns>
    Task<LabelServiceResult<LabelPolicyDefinition>> GetPolicyVersionAsync(
        LabelAdministrationCaller caller,
        string version,
        CancellationToken cancellationToken = default);

    /// <summary>Reads a bounded ascending page of immutable versions.</summary>
    /// <param name="caller">The provider-issued viewer or higher.</param>
    /// <param name="afterVersion">The exclusive version boundary, or <see langword="null"/> for the first page.</param>
    /// <param name="limit">The maximum results, between one and one hundred.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The authorized retained versions or a classified failure.</returns>
    Task<LabelServiceResult<IReadOnlyList<LabelPolicyDefinition>>> GetPolicyVersionsAsync(
        LabelAdministrationCaller caller,
        string? afterVersion = null,
        int limit = 100,
        CancellationToken cancellationToken = default);

    /// <summary>Reads bounded retained policy administration and observation history.</summary>
    /// <param name="caller">The provider-issued viewer or higher.</param>
    /// <param name="afterSequence">The exclusive history boundary.</param>
    /// <param name="limit">The maximum results, between one and one hundred.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The authorized historical events or a classified failure.</returns>
    Task<LabelServiceResult<IReadOnlyList<LabelPolicyHistoryEntry>>> GetPolicyHistoryAsync(
        LabelAdministrationCaller caller,
        long afterSequence = 0,
        int limit = 100,
        CancellationToken cancellationToken = default);

    /// <summary>Recovers the retained result of a stable policy operation without repeating publication.</summary>
    /// <param name="caller">The provider-issued viewer or higher.</param>
    /// <param name="operationId">The original administrative operation identity.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The historical result or a classified failure.</returns>
    Task<LabelServiceResult<LabelPolicyHistoryEntry>> GetPolicyOperationAsync(
        LabelAdministrationCaller caller,
        string operationId,
        CancellationToken cancellationToken = default);

    /// <summary>Enables fail-closed moderation gates without selecting or publishing a declaration.</summary>
    /// <param name="verificationFreshness">The maximum observation age, or <see langword="null"/> for 60 seconds.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The initialized durable gate.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The freshness is less than one millisecond or greater than one day.</exception>
    /// <remarks><para>Initialization is host-owned. Reopening with a different freshness interval fails
    /// rather than silently changing an established cross-host contract.</para></remarks>
    Task<LabelPolicyState> InitializePolicyAsync(TimeSpan? verificationFreshness = null, CancellationToken cancellationToken = default);

    /// <summary>Retains an immutable draft without disrupting the active declaration.</summary>
    /// <param name="caller">The provider-issued administrator.</param>
    /// <param name="operationId">The stable same-intent operation identity.</param>
    /// <param name="policy">The complete draft.</param>
    /// <param name="reason">The administrative reason.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The retained draft or a classified failure.</returns>
    Task<LabelServiceResult<LabelPolicyDefinition>> SavePolicyAsync(
        LabelAdministrationCaller caller, string operationId, LabelPolicyDefinition policy, string reason, CancellationToken cancellationToken = default);

    /// <summary>Closes reports and new issuance before a conditional remote declaration write.</summary>
    /// <param name="caller">The provider-issued administrator.</param>
    /// <param name="operationId">The stable publication identity.</param>
    /// <param name="version">The retained draft version.</param>
    /// <param name="expectedRevision">The observed gate revision.</param>
    /// <param name="previousCid">The expected previous PDS CID, or <see langword="null"/> for creation.</param>
    /// <param name="reason">The administrative reason.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The durable publication intent or a classified failure.</returns>
    Task<LabelServiceResult<LabelPolicyState>> BeginPolicyPublicationAsync(
        LabelAdministrationCaller caller, string operationId, string version, long expectedRevision, Cid? previousCid, string reason,
        CancellationToken cancellationToken = default);

    /// <summary>Abandons a pending publication while keeping reports and new issuance closed.</summary>
    /// <param name="caller">The provider-issued administrator.</param>
    /// <param name="operationId">The stable same-intent recovery identity.</param>
    /// <param name="expectedRevision">The observed pending gate revision.</param>
    /// <param name="reason">The explicit administrative recovery reason.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The retained fail-closed recovery outcome or a classified failure.</returns>
    /// <remarks><para>This operation does not cancel an in-flight remote write or roll back the PDS.
    /// The selected policy and previous CID remain available for inspection. A new explicit publication
    /// intent followed by independent verification is required to reopen moderation.</para></remarks>
    Task<LabelServiceResult<LabelPolicyState>> AbandonPolicyPublicationAsync(
        LabelAdministrationCaller caller,
        string operationId,
        long expectedRevision,
        string reason,
        CancellationToken cancellationToken = default);

    /// <summary>Reads the current gate for protected administration.</summary>
    /// <param name="caller">The provider-issued viewer or higher.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The current gate or a classified failure.</returns>
    Task<LabelServiceResult<LabelPolicyState>> GetPolicyStateAsync(LabelAdministrationCaller caller, CancellationToken cancellationToken = default);

    /// <summary>Reads the current gate for a trusted verifier or policy provider without exposing an administrative route.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The durable gate, or <see langword="null"/> when moderation has never been enabled.</returns>
    Task<LabelPolicyState?> ReadPolicyStateAsync(CancellationToken cancellationToken = default);

    /// <summary>Records an independently retrieved PDS declaration using an optimistic observation fence.</summary>
    /// <param name="expectedRevision">The revision read before starting network verification.</param>
    /// <param name="verificationStartedAt">The database-clock instant captured before the network lookup.</param>
    /// <param name="observedCid">The CID returned by the discovered authoritative PDS, or <see langword="null"/> for authoritative absence.</param>
    /// <param name="declaration">The complete exact-issuer declaration, or a JSON <see langword="null"/> for authoritative absence.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The resulting verified or conflicted gate, or a stale-observation failure.</returns>
    /// <remarks><para>A matching observation cannot clear a previously detected conflict; an administrator
    /// must first create a new publication/reconciliation intent. No network work occurs inside the transaction.</para></remarks>
    Task<LabelServiceResult<LabelPolicyState>> ObservePolicyAsync(
        long expectedRevision, DateTimeOffset verificationStartedAt, Cid? observedCid, JsonElement declaration, CancellationToken cancellationToken = default);
}
