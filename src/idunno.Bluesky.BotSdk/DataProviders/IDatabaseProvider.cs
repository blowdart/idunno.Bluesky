// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;
using idunno.AtProto.Jetstream.Archive;

namespace idunno.Bluesky.BotSdk.DataProviders;

/// <summary>Persists ownership, replay progress, complete actions, credentials, and rate accounting.</summary>
/// <remarks>
/// <para>Implementations must fence durable operations against the current ownership lease. Event effects and
/// progress must commit together; processed-through progress must never precede durable handling.</para>
/// <para>Preserve complete request/checkpoint JSON and fixed action records. Remote writes are outside the
/// storage transaction and require reconciliation. Synchronous SDK callbacks must have bounded database deadlines.</para>
/// <para>Throw <see cref="OwnershipLostException"/> when a fenced operation is rejected because ownership is lost.</para>
/// </remarks>
public interface IDatabaseProvider
{
    /// <summary>Gets the unique identity of this provider's process owner.</summary>
    Guid Owner { get; }

    /// <summary>Determines whether an exception originated from the underlying database client.</summary>
    /// <param name="exception">The exception to classify.</param>
    /// <returns><see langword="true"/> for a database exception; otherwise, <see langword="false"/>.</returns>
    bool IsDatabaseException(Exception exception);

    /// <summary>Determines whether a database failure permits retry under the existing recovery policy.</summary>
    /// <param name="exception">The exception to classify.</param>
    /// <returns><see langword="true"/> for a transient database failure; otherwise, <see langword="false"/>.</returns>
    bool IsTransientDatabaseException(Exception exception);

    /// <summary>Initializes storage without deleting existing state.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task InitializeAsync(CancellationToken cancellationToken);

    /// <summary>Acquires or renews ownership using database time and a deployment drain period.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The time at which this owner may work, or <see langword="null"/> when another process owns the lease.</returns>
    Task<DateTimeOffset?> AcquireOrRenewAsync(CancellationToken cancellationToken);

    /// <summary>Releases this owner's lease without releasing another owner's lease.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task ReleaseAsync(CancellationToken cancellationToken);

    /// <summary>Loads or initializes durable replay state and validates its account and service identity.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The complete saved replay state.</returns>
    Task<ReplayState> LoadStateAsync(CancellationToken cancellationToken);

    /// <summary>Saves the initial replay request only when no request has already been saved.</summary>
    /// <param name="request">The complete original request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number of affected rows.</returns>
    Task<int> SaveRequestAsync(SnapshotRequest request, CancellationToken cancellationToken);

    /// <summary>Commits event effects and processed-through progress in one transaction.</summary>
    /// <param name="sequence">The event sequence, not a timestamp.</param>
    /// <param name="actions">The complete actions to enqueue, or an empty list for an event without actions.</param>
    /// <param name="malformedReason">The reason for auditing a malformed post, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <param name="deletedUri">The deleted source URI to mark, or <see langword="null"/>.</param>
    /// <returns>The number of newly inserted actions, excluding existing composite identities.</returns>
    /// <exception cref="ArgumentException">An action kind or key is empty or longer than 128 characters, its payload version is not positive, or a new action has a different source sequence.</exception>
    /// <remarks>
    /// <para>An action is identified by its source URI, kind, and key. All new actions, deletion flags, audit
    /// entries, and progress commit together or roll back together. Separate action identities may reference
    /// the same fixed remote record key; the action handler must reconcile the remote record's identity.</para>
    /// <para>Already processed events and existing action identities do not regenerate saved records.</para>
    /// </remarks>
    Task<int> HandleAsync(
        long sequence,
        IReadOnlyList<BotAction> actions,
        string? malformedReason,
        CancellationToken cancellationToken,
        AtUri? deletedUri = null);

    /// <summary>Applies an audited forward-only history skip while retaining queued work and request filters.</summary>
    /// <param name="cutoff">The fixed witnessed-time cutoff requested by the user.</param>
    /// <param name="replacement">The request containing the proposed starting boundary.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The resulting processed-through boundary.</returns>
    Task<long> OverrideLookbackAsync(DateTimeOffset cutoff, SnapshotRequest replacement, CancellationToken cancellationToken);

    /// <summary>Saves the complete replay checkpoint after preceding events have been durably handled.</summary>
    /// <param name="checkpoint">The complete SDK checkpoint.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <param name="consumedThrough">The consumed boundary to coalesce with durable progress, or <see langword="null"/>.</param>
    void SaveCheckpoint(SnapshotCheckpoint checkpoint, CancellationToken cancellationToken, long? consumedThrough = null);

    /// <summary>Audits an explicitly permitted archive skip as potential data loss.</summary>
    /// <param name="sequence">The skipped sequence, or <see langword="null"/> for a block.</param>
    /// <param name="scope">The scope of skipped data.</param>
    /// <param name="reason">The reason for skipping.</param>
    void RecordSkip(long? sequence, string scope, string reason);

    /// <summary>Loads the next pending or uncertain action whose retry time has arrived.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The eligible work, or <see langword="null"/>.</returns>
    Task<BotAction?> NextWorkAsync(CancellationToken cancellationToken);

    /// <summary>Marks one composite action identity uncertain and increments its attempt count before a remote write.</summary>
    /// <param name="work">The work being attempted.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number of affected rows.</returns>
    Task<int> MarkAttemptAsync(
        BotAction work,
        CancellationToken cancellationToken);

    /// <summary>Saves a terminal action outcome for one composite identity.</summary>
    /// <param name="work">The completed work.</param>
    /// <param name="status">The terminal delivery status.</param>
    /// <param name="remoteReference">The remote action URI, or <see langword="null"/> when not sent.</param>
    /// <param name="reason">The outcome reason.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number of affected rows.</returns>
    Task<int> FinishAsync(
        BotAction work,
        string status,
        string? remoteReference,
        string reason,
        CancellationToken cancellationToken);

    /// <summary>Schedules another reconciliation or send attempt for one identity without changing its fixed record.</summary>
    /// <param name="work">The work to retry.</param>
    /// <param name="retryAt">The earliest next attempt time.</param>
    /// <param name="reason">The retry reason.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number of affected rows.</returns>
    Task<int> RetryAsync(
        BotAction work,
        DateTimeOffset retryAt,
        string reason,
        CancellationToken cancellationToken);

    /// <summary>Loads the encrypted refresh-credential envelope.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The encrypted envelope, or <see langword="null"/>.</returns>
    Task<byte[]?> LoadCredentialsAsync(CancellationToken cancellationToken);

    /// <summary>Saves or clears the encrypted refresh-credential envelope.</summary>
    /// <param name="encrypted">The encrypted envelope, or <see langword="null"/> to clear it.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number of affected rows.</returns>
    Task<int> SaveCredentialsAsync(byte[]? encrypted, CancellationToken cancellationToken);

    /// <summary>Reserves durable outbound budget before a request, or reports when budget becomes available.</summary>
    /// <param name="points">The repository write-point cost.</param>
    /// <param name="session"><see langword="true"/> for session creation; otherwise, <see langword="false"/>.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The required wait deadline, or <see langword="null"/> when the request was reserved.</returns>
    Task<DateTimeOffset?> ReserveRequestAsync(int points, bool session, CancellationToken cancellationToken);

    /// <summary>Extends the durable outbound cooldown without shortening an existing cooldown.</summary>
    /// <param name="until">The cooldown deadline.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number of affected rows.</returns>
    Task<int> CooldownAsync(DateTimeOffset until, CancellationToken cancellationToken);

    /// <summary>Loads the durable archive cooldown deadline.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The saved cooldown deadline.</returns>
    Task<DateTimeOffset> ArchiveCooldownAsync(CancellationToken cancellationToken);

    /// <summary>Extends the durable archive cooldown without shortening an existing cooldown.</summary>
    /// <param name="until">The cooldown deadline.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number of affected rows.</returns>
    Task<int> SaveArchiveCooldownAsync(DateTimeOffset until, CancellationToken cancellationToken);

    /// <summary>Prunes terminal work only when retention and processed-through coverage permit it.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number of affected rows.</returns>
    /// <remarks><para>Retain pending and uncertain work and preserve rate accounting for its rolling windows.</para></remarks>
    Task<int> PruneAsync(CancellationToken cancellationToken);
}

/// <summary>Provides optional read-only connection and operational diagnostics for a bot database.</summary>
/// <remarks>
/// <para>Built-in providers implement this interface. Custom providers may implement it to support the
/// sample's doctor check and Prometheus endpoint without expanding the core durable-storage contract.</para>
/// </remarks>
public interface IBotDatabaseDiagnostics
{
    /// <summary>Checks database connectivity without creating schema or changing durable state.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    Task CheckConnectionAsync(CancellationToken cancellationToken);

    /// <summary>Reads a point-in-time operational snapshot without changing durable state.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>Durable progress and counts for pending, uncertain, and retained terminal actions.</returns>
    Task<BotOperationalSnapshot> GetOperationalSnapshotAsync(CancellationToken cancellationToken);
}

/// <summary>Describes durable queue and replay state for operational metrics.</summary>
/// <param name="ProcessedThrough">The greatest sequence whose effects are durably handled; this is not a timestamp or event count.</param>
/// <param name="PendingActions">The number of actions waiting for their first send attempt.</param>
/// <param name="UncertainActions">The number of actions whose remote effect may need reconciliation.</param>
/// <param name="RetryDueActions">The number of pending or uncertain actions whose saved retry time has arrived.</param>
/// <param name="SentActions">The number of sent actions still within terminal-row retention.</param>
/// <param name="RejectedActions">The number of terminally rejected actions still within terminal-row retention.</param>
/// <param name="SimulatedActions">The number of simulated actions still within terminal-row retention.</param>
public sealed record BotOperationalSnapshot(
    long ProcessedThrough,
    long PendingActions,
    long UncertainActions,
    long RetryDueActions,
    long SentActions,
    long RejectedActions,
    long SimulatedActions);
