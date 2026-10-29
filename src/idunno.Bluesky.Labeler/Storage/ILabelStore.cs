// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;
using idunno.Bluesky.Labeler.Administration;
using idunno.Bluesky.Labeler.Issuance;
using idunno.Bluesky.Labeler.Maintenance;

namespace idunno.Bluesky.Labeler.Storage;

/// <summary>
/// Provides durable storage for a standalone labeler's signed labels, publication progress and subscription history,
/// including atomic decision/audit receipts and checkpoints for retiring the labeler and explicitly purging its local history.
/// </summary>
/// <remarks>
/// <para>Each database belongs to one issuer. Mutations serialize with sequence allocation and lifecycle checks.
/// Append atomically stores a fixed batch, receipt and pending publication; only a published prefix is readable.
/// Retry ambiguous writes by looking up their receipt, never by generating a new signature.</para>
/// <para>
/// Retirement is the permanent, orderly shutdown of label issuance: ordinary issuance closes, the caller enumerates
/// existing history and signs any required negations, and those negations are published before a caller-selected
/// distribution grace period begins. After that period, retirement can be completed and local history may be
/// explicitly purged. These low-level methods persist caller-supplied progress; they do not themselves sign labels,
/// serve subscribers or acknowledge downstream delivery. Hosted administration uses a separate authorized, audited
/// retirement workflow with confirmation challenges and provider-backed bounded selection and checkpoints.
/// </para>
/// <para>Decision commits keep receipts, audit outcomes and signed label appends transactionally consistent.
/// Decision snapshots are observations, not locks; commits check the expected revision under the mutation lock.</para>
/// <para>Authorization, policy, signing and private-key storage belong to callers. Additional authorization
/// operations requiring atomicity must extend typed provider operations, not use a second independent write or raw database access.</para>
/// </remarks>
public interface ILabelStore : ILabelDistributionStore
{
    /// <summary>
    /// Atomically persists an already signed label batch, allocates its ordered sequences and records an idempotency
    /// receipt, optionally advancing retirement enumeration in the same transaction.
    /// </summary>
    /// <param name="operationId">The nonblank, ordinal idempotency key of at most 256 UTF-8 bytes, containing no U+0000 characters and retained until explicit purge.</param>
    /// <param name="labels">The fixed signed batch of at most 1,000 labels; an empty batch is allowed only with a retirement checkpoint.</param>
    /// <param name="expectedRevision">An optional required store revision, or <see langword="null"/> to omit the optimistic concurrency check.</param>
    /// <param name="retirementThrough">
    /// An optional inclusive sequence in the frozen pre-retirement history through which the caller has completed
    /// enumeration, not the sequence of a newly appended negation.
    /// </param>
    /// <param name="caller">The authorized maintenance caller for administrative retirement batches, or <see langword="null"/> for ordinary issuance.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The original receipt for an identical retry, or a newly committed receipt.</returns>
    /// <exception cref="ArgumentException">The operation key or batch is invalid.</exception>
    /// <exception cref="ArgumentOutOfRangeException">An input exceeds a supported size limit.</exception>
    /// <exception cref="InvalidOperationException">
    /// The key identifies different input, the required revision is stale, or the lifecycle phase or retirement checkpoint does not permit the append.
    /// </exception>
    /// <exception cref="InvalidDataException">A signed label belongs to another issuer.</exception>
    /// <exception cref="OverflowException">A sequence or revision cannot be incremented.</exception>
    /// <remarks>
    /// <para>
    /// Newly committed labels remain pending and cannot be queried or replayed until <see cref="PublishAsync"/>
    /// advances publication. The batch, sequences, receipt and optional checkpoint commit together.
    /// </para>
    /// <para>
    /// An identical key and signed input returns the original receipt before checking the expected revision.
    /// Changed input under that key fails. Preserve the exact signed batch for retries: signing the same payload
    /// again can produce a different signature. A commit-time failure can leave the outcome uncertain;
    /// use <see cref="GetReceiptAsync"/> to recover the acknowledgement.
    /// </para>
    /// <para>
    /// After <see cref="BeginRetirementAsync"/>, new batches require a strictly increasing checkpoint no greater
    /// than the frozen retirement upper bound and may contain only negations. An empty batch can checkpoint
    /// inspected history that requires no negations. Reaching the upper bound ends enumeration.
    /// Selecting applicable labels, verifying cryptographic trust, authorizing issuance and deciding supersession
    /// policy remain caller responsibilities.
    /// </para>
    /// </remarks>
    Task<LabelAppendReceipt> AppendAsync(
        string operationId,
        IReadOnlyList<SignedLabelSnapshot> labels,
        long? expectedRevision = null,
        long? retirementThrough = null,
        LabelAdministrationCaller? caller = null,
        CancellationToken cancellationToken = default);

    /// <summary>Replaces signatures for a bounded set of existing labels without changing their signed fields or sequence positions.</summary>
    /// <param name="labels">The ordered stored labels containing replacement signatures and their original sequences.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the atomic signature replacement.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="labels"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The batch is not strictly ordered or contains invalid sequence positions.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The batch contains more than 1,000 labels.</exception>
    /// <exception cref="InvalidOperationException">A sequence does not exist or is not yet published.</exception>
    /// <exception cref="InvalidDataException">A replacement changes signed fields or has a different issuer.</exception>
    /// <remarks>
    /// <para>Use this after pausing issuance and verifying that the replacement public key is published. It changes only signatures; each label's timestamp, other signed fields and sequence remain unchanged. Submit bounded batches and persist the returned batch boundary externally so a rotation can resume after interruption.</para>
    /// </remarks>
    Task ReplaceSignaturesAsync(
        IReadOnlyList<StoredLabel> labels,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Starts a caller-selected waiting period after retirement enumeration and publication finish, allowing
    /// downstream consumers time to retrieve negations before retirement can complete.
    /// </summary>
    /// <param name="expectedRevision">The expected revision.</param>
    /// <param name="gracePeriod">The positive minimum distribution waiting period selected by the caller, not an AT Protocol mandated duration.</param>
    /// <param name="caller">The authorized maintenance caller, or <see langword="null"/> for local administration.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The resulting durable state.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The grace period is not positive or the resulting deadline is outside the supported timestamp range.</exception>
    /// <exception cref="InvalidOperationException">The revision is stale, retirement enumeration is incomplete, or issued labels remain unpublished.</exception>
    /// <exception cref="OverflowException">The deadline or revision cannot be represented.</exception>
    /// <remarks>
    /// <para>
    /// The persisted grace deadline uses database time sampled after acquiring the state lock.
    /// The duration is rounded up to milliseconds with an extra millisecond for clock quantization.
    /// Provider recreation preserves the deadline.
    /// </para>
    /// <para>
    /// The host remains responsible for serving published history throughout the waiting period.
    /// Starting or reaching the deadline neither proves downstream receipt nor automatically completes retirement,
    /// deletes history or destroys signing keys. Call <see cref="CompleteRetirementAsync"/> explicitly afterward.
    /// </para>
    /// </remarks>
    Task<LabelStoreState> BeginGracePeriodAsync(
        long expectedRevision,
        TimeSpan gracePeriod,
        LabelAdministrationCaller? caller = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Permanently closes ordinary label issuance to begin an orderly labeler shutdown, freezing the existing
    /// history boundary that the caller must inspect for retirement negations.
    /// </summary>
    /// <param name="expectedRevision">The expected revision.</param>
    /// <param name="caller">The authorized maintenance caller, or <see langword="null"/> for local administration.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The resulting durable state.</returns>
    /// <exception cref="InvalidOperationException">The revision is stale or the store is no longer active.</exception>
    /// <remarks>
    /// <para>
    /// Retirement withdraws applicable labels before the labeler ceases distribution; it is not immediate deletion.
    /// The caller enumerates history through the returned retirement upper bound, chooses which labels require
    /// negation, signs those negations and appends them with progress checkpoints through <see cref="AppendAsync"/>.
    /// Publish existing pending history before reading it through query or replay.
    /// </para>
    /// <para>
    /// The store does not generate negations or run enumeration in the background.
    /// An empty history enters the distribution phase immediately; otherwise enumeration must reach the frozen
    /// bound before <see cref="BeginGracePeriodAsync"/> is permitted. There is no operation to reopen ordinary issuance.
    /// </para>
    /// </remarks>
    Task<LabelStoreState> BeginRetirementAsync(
        long expectedRevision,
        LabelAdministrationCaller? caller = null,
        CancellationToken cancellationToken = default);

    /// <summary>Atomically commits a decision, audit outcome and optional signed label.</summary>
    /// <param name="decision">The immutable decision commit input.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The original receipt for an identical retry or the newly committed receipt.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="decision"/> is <see langword="null"/>.</exception>
    /// <exception cref="OperationCanceledException">The operation is canceled. After commit submission, the returned outcome may be uncertain.</exception>
    Task<LabelServiceResult<LabelOperationReceipt>> CommitDecisionAsync(
        LabelDecisionCommit decision,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks the labeler's orderly issuance shutdown as complete once its distribution grace deadline has passed,
    /// making its local history eligible for a separately authorized purge.
    /// </summary>
    /// <param name="expectedRevision">The expected revision.</param>
    /// <param name="caller">The authorized maintenance caller, or <see langword="null"/> for local administration.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The resulting durable state.</returns>
    /// <exception cref="InvalidOperationException">The revision is stale, the store is not in its grace period, or the database-clock deadline has not been reached.</exception>
    /// <remarks>
    /// <para>
    /// This transition retains signed history and receipts for query and replay.
    /// It does not delete data, stop a host, erase downstream copies, destroy private keys or acknowledge subscriber receipt.
    /// Local removal requires an explicit subsequent call to <see cref="PurgeAsync"/>.
    /// </para>
    /// </remarks>
    Task<LabelStoreState> CompleteRetirementAsync(
        long expectedRevision,
        LabelAdministrationCaller? caller = null,
        CancellationToken cancellationToken = default);

    /// <summary>Recovers the original committed service-operation receipt.</summary>
    /// <param name="operationId">The nonblank service-operation identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The retained receipt or an operation-not-found error.</returns>
    /// <exception cref="ArgumentException"><paramref name="operationId"/> is blank, contains U+0000 or is not valid UTF-8.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="operationId"/> exceeds 256 UTF-8 bytes.</exception>
    /// <exception cref="OperationCanceledException">The operation is canceled.</exception>
    Task<LabelServiceResult<LabelOperationReceipt>> GetOperationAsync(
        string operationId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Recovers the original append acknowledgement by operation key so a caller can resolve an uncertain write
    /// without generating new signatures.
    /// </summary>
    /// <param name="operationId">The nonblank idempotency key of at most 256 UTF-8 bytes, containing no U+0000 characters.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The receipt, or <see langword="null"/> when the key has not committed or was explicitly purged.</returns>
    /// <exception cref="ArgumentException">The operation key is blank, contains U+0000 or cannot be encoded as UTF-8.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The operation key exceeds 256 UTF-8 bytes.</exception>
    /// <remarks>
    /// <para>
    /// An absent receipt permits retrying the same fixed signed command, not substituting different input.
    /// Receipts survive provider recreation and remain available until explicit local purge.
    /// </para>
    /// </remarks>
    Task<LabelAppendReceipt?> GetReceiptAsync(string operationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Samples publication state, pending-label count and oldest pending age for monitoring publisher progress,
    /// and records the corresponding persistence metric measurements.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The cross-process durable snapshot.</returns>
    /// <remarks>
    /// <para>
    /// The age uses the database clock and is zero for an empty backlog. The snapshot describes shared durable
    /// storage rather than a process-local queue; do not sum identical database snapshots from multiple hosts.
    /// Sampling does not publish labels, create a timer or start a background monitoring task.
    /// </para>
    /// </remarks>
    Task<LabelStoreSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads the persisted stream identity, publication boundaries and lifecycle checkpoints needed to resume work
    /// or make an optimistic concurrency decision.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The durable state.</returns>
    /// <remarks>
    /// <para>
    /// Use the returned revision for lifecycle operations or an append whose decision depends on this state.
    /// Another writer can change the state after this read; a revision check detects that change.
    /// This operation does not publish pending labels or advance lifecycle progress.
    /// </para>
    /// </remarks>
    Task<LabelStoreState> GetStateAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Prepares a provider database for label storage by creating or resuming its schema and binding its stream
    /// to one issuer without deleting existing history.
    /// </summary>
    /// <param name="issuer">The configured issuer.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing schema initialization and issuer binding.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="issuer"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">The database belongs to another issuer or required storage guarantees are unavailable.</exception>
    /// <exception cref="InvalidDataException">The schema version is unsupported.</exception>
    /// <remarks>
    /// <para>
    /// Call this operation with setup privileges before using a new store, including a database created from
    /// the packaged SQL script. Repeating initialization for the same issuer preserves its stream identity and data;
    /// initialization does not reopen a retiring or purged store. Registration alone does not initialize storage.
    /// </para>
    /// </remarks>
    Task InitializeAsync(Did issuer, CancellationToken cancellationToken = default);

    /// <summary>
    /// Makes the next bounded group of pending labels durably visible to queries and subscription replay,
    /// preserving their committed sequence order.
    /// </summary>
    /// <param name="limit">The maximum number of labels, from 1 through 1,000.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The resulting durable state.</returns>
    /// <remarks>
    /// <para>
    /// Repeated calls drain the gap between issued and published sequences. A call with no pending labels
    /// leaves the state unchanged. A failed transaction exposes no partial promotion; restart can resume
    /// from the persisted published boundary.
    /// </para>
    /// <para>
    /// Publication is local durable visibility, not a network send or acknowledgement by a remote subscriber.
    /// This operation has no external work between claim and completion, so it needs no expiring work lease.
    /// </para>
    /// </remarks>
    Task<LabelStoreState> PublishAsync(int limit = 100, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a bounded batch of local signed-label history and append receipts after completed retirement,
    /// persisting a checkpoint so a caller can resume deletion after interruption.
    /// </summary>
    /// <param name="expectedRevision">The expected revision.</param>
    /// <param name="confirmed"><see langword="true"/> to confirm local destruction; otherwise, <see langword="false"/> is rejected.</param>
    /// <param name="limit">The maximum labels and, separately, receipts removed per call, from 1 through 1,000.</param>
    /// <param name="caller">The authorized maintenance caller, or <see langword="null"/> for local administration.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The resulting checkpoint with advanced replay floor.</returns>
    /// <exception cref="ArgumentException">Local destruction is not explicitly confirmed.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The batch limit is outside the supported range.</exception>
    /// <exception cref="InvalidOperationException">The revision is stale or the store is neither retired nor already purging.</exception>
    /// <remarks>
    /// <para>
    /// Callers must authorize and confirm this destructive operation. Repeat it with each returned revision
    /// until the phase is <see cref="LabelerLifecyclePhase.Purged"/>; a completed purge is not a request to reopen issuance.
    /// Advancing the replay floor makes older resume positions and affected query cursors invalid.
    /// Deleted receipts no longer establish idempotency outcomes.
    /// </para>
    /// <para>
    /// Stream identity, sequence high-water marks and lifecycle metadata survive; no sequence is reused.
    /// Purge does not destroy private keys, erase backups or guarantee removal of downstream copies.
    /// </para>
    /// </remarks>
    Task<LabelStoreState> PurgeAsync(
        long expectedRevision,
        bool confirmed,
        int limit = 100,
        LabelAdministrationCaller? caller = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a page of published signed labels matching subject and issuer filters, using a fixed published
    /// boundary so later publications do not change the history being paginated.
    /// </summary>
    /// <param name="uriPatterns">The list of at most 100 exact subject URIs or trailing-asterisk prefixes; an empty list matches all subjects.</param>
    /// <param name="source">An optional source filter.</param>
    /// <param name="cursor">An optional continuation from the previous page.</param>
    /// <param name="limit">The page size, from 1 through 1,000.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The page and its fixed published tip.</returns>
    /// <exception cref="InvalidDataException">The cursor is malformed, incompatible with the stream or filters, beyond publication, or pruned.</exception>
    /// <remarks>
    /// <para>
    /// Matching is ordinal and case-sensitive. Continue by passing the returned cursor with the same filters;
    /// a <see langword="null"/> result cursor means the captured history is exhausted.
    /// Unpublished labels are excluded, but expiry and negation do not automatically remove historical events.
    /// </para>
    /// <para>
    /// Cursors are navigation tokens, not authorization capabilities or downstream delivery acknowledgements.
    /// Explicit purge can invalidate a continuation that requires deleted history.
    /// </para>
    /// </remarks>
    Task<LabelPage> QueryAsync(
        IReadOnlyList<string> uriPatterns,
        Did? source = null,
        string? cursor = null,
        int limit = 100,
        CancellationToken cancellationToken = default);

    /// <summary>Reads the current decision for a resolved target and label value.</summary>
    /// <param name="resolvedTarget">The target whose repository has been resolved to a DID.</param>
    /// <param name="value">The label value.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The decision snapshot or a classified storage or history error.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="resolvedTarget"/> or <paramref name="value"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="value"/> is blank, contains U+0000 or is not valid UTF-8.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> exceeds 128 UTF-8 bytes.</exception>
    /// <exception cref="OperationCanceledException">The operation is canceled.</exception>
    Task<LabelServiceResult<LabelDecisionSnapshot>> ReadDecisionAsync(
        LabelTarget resolvedTarget,
        string value,
        CancellationToken cancellationToken = default);

    /// <summary>Reads several current decisions from one consistent indexed history snapshot.</summary>
    /// <param name="queries">The resolved target and value pairs to inspect.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>Decision results in the same order as the supplied queries.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="queries"/> or one of its targets is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">A target is unresolved or a value is invalid.</exception>
    /// <exception cref="ArgumentOutOfRangeException">More than 400 decisions are requested.</exception>
    Task<IReadOnlyList<LabelServiceResult<LabelDecisionSnapshot>>> ReadDecisionBatchAsync(
        IReadOnlyList<(LabelTarget Target, string Value)> queries,
        CancellationToken cancellationToken = default);

    /// <summary>Reads latest subject/value creation timestamps for negating all retained retirement slots, including tied CID variants.</summary>
    /// <param name="queries">The resolved targets and label values.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The latest timestamps, or <see langword="null"/> for missing history, in query order.</returns>
    /// <exception cref="ArgumentNullException">The queries or a target are <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">A target is unresolved or a value is invalid.</exception>
    /// <exception cref="ArgumentOutOfRangeException">More than 400 queries are supplied.</exception>
    /// <exception cref="InvalidOperationException">The store is not in the retirement enumeration phase.</exception>
    /// <remarks><para>This does not choose a current label. Equal-time conflicting variants remain ambiguous for ordinary decisions.</para></remarks>
    Task<IReadOnlyList<DateTimeOffset?>> ReadRetirementTimestampsAsync(
        IReadOnlyList<(LabelTarget Target, string Value)> queries,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves ordered published label events within explicit stream and sequence boundaries so a subscription
    /// can catch up or resume without renumbering historical events.
    /// </summary>
    /// <param name="streamId">The durable stream identity obtained from state or a page.</param>
    /// <param name="after">The exclusive sequence boundary.</param>
    /// <param name="through">The inclusive captured published tip.</param>
    /// <param name="limit">The page size, from 1 through 1,000.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The ordered replay page; each label is one immutable subscription event.</returns>
    /// <exception cref="InvalidDataException">The stream differs, the boundaries are reversed, required history is pruned, or the end exceeds publication.</exception>
    /// <remarks>
    /// <para>
    /// Capture the stream identity and published boundary from store state or a query result.
    /// Continue after the last returned sequence with the same inclusive end boundary until it is drained,
    /// then capture a newer published boundary to read subsequent events. Replay results do not supply query cursors.
    /// </para>
    /// <para>
    /// This operation only reads durable history: it does not open a subscription connection, wait for future
    /// publication or record subscriber acknowledgements. A subscriber must retain its own resume position.
    /// </para>
    /// </remarks>
    Task<LabelPage> ReplayAsync(
        Guid streamId,
        long after,
        long through,
        int limit = 100,
        CancellationToken cancellationToken = default);
}
