// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;

using idunno.AtProto;
using idunno.AtProto.Jetstream.Archive;

namespace idunno.Bluesky.BotSdk.DataProviders;

/// <summary>Stores bot runtime state in volatile process memory for development and tests.</summary>
/// <remarks>
/// <para>This provider is not suitable for production. It does not survive process restarts, and its lease cannot
/// coordinate separate processes. A warning is logged when storage is initialized.</para>
/// <para>Use a relational provider for durable state, cross-process ownership fencing, and recovery after restart.</para>
/// </remarks>
internal sealed class InMemoryDatabaseProvider(
    BotOptions options,
    TimeProvider timeProvider,
    ILogger<InMemoryDatabaseProvider> logger) : IDatabaseProvider, IBotDatabaseDiagnostics
{
    private static readonly TimeSpan s_leaseLength = TimeSpan.FromSeconds(90);
    private static readonly TimeSpan s_readyDelay = TimeSpan.FromSeconds(40);
    private static readonly TimeSpan s_leaseReserve = TimeSpan.FromSeconds(35);
    private static readonly TimeSpan[] s_rateWindows =
    [
        TimeSpan.FromMinutes(5),
        TimeSpan.FromHours(1),
        TimeSpan.FromDays(1),
        TimeSpan.FromMinutes(5),
        TimeSpan.FromDays(1)
    ];

    private readonly object _gate = new();
    private readonly Guid _owner = Guid.NewGuid();
    private readonly Dictionary<ActionIdentity, WorkState> _work = [];
    private readonly List<RateReservation> _rateReservations = [];
    private readonly List<SkippedData> _skippedData = [];
    private ReplayState? _state;
    private Guid? _leaseOwner;
    private DateTimeOffset _leaseUntil = DateTimeOffset.UnixEpoch;
    private DateTimeOffset _readyAt = DateTimeOffset.UnixEpoch;
    private DateTimeOffset _nextRequestAt = DateTimeOffset.UnixEpoch;
    private DateTimeOffset _cooldownUntil = DateTimeOffset.UnixEpoch;
    private DateTimeOffset _archiveCooldownUntil = DateTimeOffset.UnixEpoch;
    private byte[]? _credentials;
    private bool _initialized;

    /// <inheritdoc/>
    public Guid Owner => _owner;

    /// <inheritdoc/>
    public bool IsDatabaseException(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return false;
    }

    /// <inheritdoc/>
    public bool IsTransientDatabaseException(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return exception is TimeoutException;
    }

    /// <inheritdoc/>
    public Task InitializeAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        bool logWarning;
        lock (_gate)
        {
            logWarning = !_initialized;
            _initialized = true;
        }

        if (logWarning)
        {
            BotLog.InMemoryStorageNotSuitableForProduction(logger);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<DateTimeOffset?> AcquireOrRenewAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            DateTimeOffset now = timeProvider.GetUtcNow();
            if (_leaseOwner is not null && _leaseOwner != _owner && _leaseUntil > now)
            {
                return Task.FromResult<DateTimeOffset?>(null);
            }

            _readyAt = _leaseOwner == _owner && _leaseUntil > now ? _readyAt : now + s_readyDelay;
            _leaseOwner = _owner;
            _leaseUntil = now + s_leaseLength;
            return Task.FromResult<DateTimeOffset?>(_readyAt);
        }
    }

    /// <inheritdoc/>
    public Task ReleaseAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            if (_leaseOwner == _owner)
            {
                _leaseUntil = timeProvider.GetUtcNow();
            }
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<ReplayState> LoadStateAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            RequireOwned();
            _state ??= new ReplayState(
                timeProvider.GetUtcNow(),
                options.JetstreamService,
                options.Did,
                options.DeveloperMode,
                null,
                null,
                0);

            if (_state.Service != options.JetstreamService ||
                _state.BotDid != options.Did ||
                _state.DeveloperMode != options.DeveloperMode)
            {
                throw new InvalidOperationException(
                    "Stored service, bot DID, or developer mode differs. Use a separate database; do not reinterpret existing progress or work.");
            }

            return Task.FromResult(_state);
        }
    }

    /// <inheritdoc/>
    public Task<int> SaveRequestAsync(SnapshotRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            RequireOwned();
            if (request.AfterSeq is not > 0)
            {
                throw new InvalidOperationException("Initial replay must have a positive, deliberately chosen sequence boundary.");
            }

            if (_state is null || _state.RequestJson is not null)
            {
                return Task.FromResult(0);
            }

            _state = _state with
            {
                RequestJson = JsonSerializer.Serialize(request, BotJson.SnapshotRequest),
                ProcessedThrough = request.AfterSeq.Value
            };

            return Task.FromResult(1);
        }
    }

    /// <inheritdoc/>
    public Task<int> HandleAsync(
        long sequence,
        IReadOnlyList<BotAction> actions,
        string? malformedReason,
        CancellationToken cancellationToken,
        AtUri? deletedUri = null)
    {
        ArgumentNullException.ThrowIfNull(actions);
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            RequireOwned();
            if (_state is null)
            {
                throw new InvalidOperationException("Replay state must be loaded before events are handled.");
            }

            if (sequence <= _state.ProcessedThrough)
            {
                return Task.FromResult(0);
            }

            List<(ActionIdentity Identity, WorkState Work)> newWork = [];
            HashSet<ActionIdentity> identities = new(_work.Keys);
            int inserted = 0;
            foreach (BotAction action in actions)
            {
                ValidateAction(action);
                ActionIdentity identity = new(action.SourceUri, action.ActionKind, action.ActionKey);
                if (!identities.Add(identity))
                {
                    continue;
                }

                if (action.SourceSequence != sequence)
                {
                    throw new ArgumentException(
                        "A new action must have the event's source sequence.",
                        nameof(actions));
                }

                string? rootUri = GetRootUri(action.RecordJson);
                newWork.Add((identity, new WorkState(action, timeProvider.GetUtcNow(), null, rootUri, null, null)));
                inserted++;
            }

            if (deletedUri is not null)
            {
                foreach ((ActionIdentity identity, WorkState work) in _work.ToArray())
                {
                    if ((work.Action.Status is "pending" or "uncertain") &&
                        (work.Action.SourceUri == deletedUri || work.RootUri == deletedUri.ToString()))
                    {
                        _work[identity] = work with { Action = work.Action with { TargetDeleted = true } };
                    }
                }
            }

            foreach ((ActionIdentity identity, WorkState work) in newWork)
            {
                _work.Add(identity, work);
            }

            if (malformedReason is not null)
            {
                _skippedData.Add(new SkippedData(timeProvider.GetUtcNow(), sequence, "post", malformedReason));
            }

            _state = _state with { ProcessedThrough = sequence };
            return Task.FromResult(inserted);
        }
    }

    /// <inheritdoc/>
    public Task<long> OverrideLookbackAsync(
        DateTimeOffset cutoff,
        SnapshotRequest replacement,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(replacement);
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            RequireOwned();
            if (_state is null || _state.RequestJson is null)
            {
                throw new InvalidOperationException("Lookback override requires initialized replay state. Start normally first.");
            }

            SnapshotRequest original = JsonSerializer.Deserialize(_state.RequestJson, BotJson.SnapshotRequest)
                ?? throw new InvalidDataException("Stored replay request is null.");
            if (cutoff <= _state.StartedAt || replacement.AfterSeq is not > 0)
            {
                throw new InvalidOperationException(
                    "Forward-only lookback must move the eligibility time forward and have a positive archive boundary.");
            }

            long after = Math.Max(_state.ProcessedThrough, replacement.AfterSeq.Value);
            if (original.BeforeSeq is long upper && after >= upper)
            {
                throw new InvalidOperationException("Lookback override would exceed the original upper sequence bound.");
            }

            _state = _state with
            {
                StartedAt = cutoff,
                RequestJson = JsonSerializer.Serialize(original with { AfterSeq = after }, BotJson.SnapshotRequest),
                CheckpointJson = null,
                ProcessedThrough = after
            };
            _skippedData.Add(new SkippedData(
                timeProvider.GetUtcNow(),
                after,
                "lookback override",
                $"User-directed forward-only override to {cutoff:O}; queued work retained."));

            return Task.FromResult(after);
        }
    }

    /// <inheritdoc/>
    public void SaveCheckpoint(
        SnapshotCheckpoint checkpoint,
        CancellationToken cancellationToken,
        long? consumedThrough = null)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            RequireOwned();
            if (_state is null)
            {
                throw new InvalidOperationException("Replay state must be loaded before a checkpoint is saved.");
            }

            long processed = Math.Max(_state.ProcessedThrough, consumedThrough ?? _state.ProcessedThrough);
            if (checkpoint.LiveAfterSeq is long live && live > processed)
            {
                throw new InvalidOperationException("Missing state or live checkpoint ahead of durable handling.");
            }

            _state = _state with
            {
                ProcessedThrough = processed,
                CheckpointJson = JsonSerializer.Serialize(checkpoint, BotJson.SnapshotCheckpoint)
            };
        }
    }

    /// <inheritdoc/>
    public void RecordSkip(long? sequence, string scope, string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scope);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        lock (_gate)
        {
            RequireOwned();
            _skippedData.Add(new SkippedData(timeProvider.GetUtcNow(), sequence, scope, reason));
        }
    }

    /// <inheritdoc/>
    public Task<BotAction?> NextWorkAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            RequireOwned();
            DateTimeOffset now = timeProvider.GetUtcNow();
            WorkState? next = _work.Values
                .Where(work => work.Action.Status is "pending" or "uncertain")
                .Where(work => work.NextRetryAt <= now)
                .OrderBy(work => work.NextRetryAt)
                .ThenBy(work => work.Action.SourceSequence)
                .FirstOrDefault();
            return Task.FromResult(next?.Action);
        }
    }

    /// <inheritdoc/>
    public Task<int> MarkAttemptAsync(BotAction work, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(work);
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            RequireOwned();
            ActionIdentity identity = Identity(work);
            if (!_work.TryGetValue(identity, out WorkState? current) ||
                current.Action.Status is not ("pending" or "uncertain") ||
                current.Action.TargetDeleted)
            {
                return Task.FromResult(0);
            }

            _work[identity] = current with
            {
                Action = current.Action with
                {
                    Status = "uncertain",
                    Attempts = current.Action.Attempts + 1
                }
            };
            return Task.FromResult(1);
        }
    }

    /// <inheritdoc/>
    public Task<int> FinishAsync(
        BotAction work,
        string status,
        string? remoteReference,
        string reason,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(work);
        ArgumentException.ThrowIfNullOrWhiteSpace(status);
        ArgumentNullException.ThrowIfNull(reason);
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            RequireOwned();
            ActionIdentity identity = Identity(work);
            if (!_work.TryGetValue(identity, out WorkState? current) ||
                current.Action.Status is not ("pending" or "uncertain"))
            {
                return Task.FromResult(0);
            }

            _work[identity] = current with
            {
                Action = current.Action with { Status = status },
                CompletedAt = timeProvider.GetUtcNow(),
                RemoteUri = remoteReference,
                Outcome = reason
            };
            return Task.FromResult(1);
        }
    }

    /// <inheritdoc/>
    public Task<int> RetryAsync(
        BotAction work,
        DateTimeOffset retryAt,
        string reason,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(work);
        ArgumentNullException.ThrowIfNull(reason);
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            RequireOwned();
            ActionIdentity identity = Identity(work);
            if (!_work.TryGetValue(identity, out WorkState? current) ||
                current.Action.Status is not ("pending" or "uncertain"))
            {
                return Task.FromResult(0);
            }

            _work[identity] = current with { NextRetryAt = retryAt, Outcome = reason };
            return Task.FromResult(1);
        }
    }

    /// <inheritdoc/>
    public Task<byte[]?> LoadCredentialsAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            RequireOwned();
            return Task.FromResult(_credentials?.ToArray());
        }
    }

    /// <inheritdoc/>
    public Task<int> SaveCredentialsAsync(byte[]? encrypted, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            RequireOwned();
            _credentials = encrypted?.ToArray();
            return Task.FromResult(1);
        }
    }

    /// <inheritdoc/>
    public Task<DateTimeOffset?> ReserveRequestAsync(int points, bool session, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            RequireOwned();
            DateTimeOffset now = timeProvider.GetUtcNow();
            DateTimeOffset waitUntil = _nextRequestAt > _cooldownUntil ? _nextRequestAt : _cooldownUntil;
            int[] thresholds =
            [
                options.RequestsPerFiveMinutes,
                options.HourlyWritePoints,
                options.DailyWritePoints,
                options.SessionsPerFiveMinutes,
                options.SessionsPerDay
            ];
            int[] requested = [1, points, points, session ? 1 : 0, session ? 1 : 0];

            for (int windowIndex = 0; windowIndex < s_rateWindows.Length; windowIndex++)
            {
                DateTimeOffset boundary = now - s_rateWindows[windowIndex];
                int total = 0;
                DateTimeOffset? first = null;
                foreach (RateReservation reservation in _rateReservations)
                {
                    if (reservation.At <= boundary)
                    {
                        continue;
                    }

                    int contribution = windowIndex switch
                    {
                        0 => 1,
                        1 or 2 => reservation.Points,
                        3 or 4 => reservation.Session ? 1 : 0,
                        _ => 0
                    };
                    total += contribution;
                    if (contribution > 0 && (first is null || reservation.At < first.Value))
                    {
                        first = reservation.At;
                    }
                }

                if (requested[windowIndex] > 0 && total + requested[windowIndex] > thresholds[windowIndex])
                {
                    if (first is null)
                    {
                        throw new InvalidOperationException("A request exceeds its entire configured rate budget.");
                    }

                    DateTimeOffset eligibleAt = first.Value + s_rateWindows[windowIndex] + TimeSpan.FromSeconds(1);
                    if (eligibleAt > waitUntil)
                    {
                        waitUntil = eligibleAt;
                    }
                }
            }

            if (waitUntil > now)
            {
                return Task.FromResult<DateTimeOffset?>(waitUntil);
            }

            _rateReservations.Add(new RateReservation(now, points, session));
            _nextRequestAt = now + TimeSpan.FromSeconds(options.MinimumRequestIntervalSeconds);
            return Task.FromResult<DateTimeOffset?>(null);
        }
    }

    /// <inheritdoc/>
    public Task<int> CooldownAsync(DateTimeOffset until, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            RequireOwned();
            _cooldownUntil = until > _cooldownUntil ? until : _cooldownUntil;
            return Task.FromResult(1);
        }
    }

    /// <inheritdoc/>
    public Task<DateTimeOffset> ArchiveCooldownAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            RequireOwned();
            return Task.FromResult(_archiveCooldownUntil);
        }
    }

    /// <inheritdoc/>
    public Task<int> SaveArchiveCooldownAsync(DateTimeOffset until, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            RequireOwned();
            _archiveCooldownUntil = until > _archiveCooldownUntil ? until : _archiveCooldownUntil;
            return Task.FromResult(1);
        }
    }

    /// <inheritdoc/>
    public Task<int> PruneAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            RequireOwned();
            DateTimeOffset now = timeProvider.GetUtcNow();
            DateTimeOffset retentionBoundary = now - TimeSpan.FromDays(options.CompletedRetentionDays);
            DateTimeOffset rateBoundary = now - TimeSpan.FromDays(1);
            int before = _work.Count + _rateReservations.Count + _skippedData.Count;
            _rateReservations.RemoveAll(reservation => reservation.At < rateBoundary);
            _skippedData.RemoveAll(skip => skip.RecordedAt < retentionBoundary);
            long processedThrough = _state?.ProcessedThrough ?? 0;
            foreach ((ActionIdentity identity, WorkState work) in _work.ToArray())
            {
                if ((work.Action.Status is "sent" or "not-sent" or "simulated") &&
                    work.CompletedAt is DateTimeOffset completedAt &&
                    completedAt < retentionBoundary &&
                    work.Action.SourceSequence <= processedThrough)
                {
                    _work.Remove(identity);
                }
            }

            return Task.FromResult(before - _work.Count - _rateReservations.Count - _skippedData.Count);
        }
    }

    /// <inheritdoc/>
    public Task CheckConnectionAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<BotOperationalSnapshot> GetOperationalSnapshotAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            long pending = _work.Values.LongCount(work => work.Action.Status == "pending");
            long uncertain = _work.Values.LongCount(work => work.Action.Status == "uncertain");
            long retryDue = _work.Values.LongCount(work =>
                (work.Action.Status is "pending" or "uncertain") && work.NextRetryAt <= timeProvider.GetUtcNow());
            long sent = _work.Values.LongCount(work => work.Action.Status == "sent");
            long rejected = _work.Values.LongCount(work => work.Action.Status == "not-sent");
            long simulated = _work.Values.LongCount(work => work.Action.Status == "simulated");
            return Task.FromResult(new BotOperationalSnapshot(
                _state?.ProcessedThrough ?? 0,
                pending,
                uncertain,
                retryDue,
                sent,
                rejected,
                simulated));
        }
    }

    private void RequireOwned()
    {
        DateTimeOffset now = timeProvider.GetUtcNow();
        if (!_initialized ||
            _leaseOwner != _owner ||
            _leaseUntil <= now + s_leaseReserve ||
            _readyAt > now)
        {
            throw new OwnershipLostException();
        }
    }

    private static void ValidateAction(BotAction action)
    {
        if (string.IsNullOrEmpty(action.ActionKind) ||
            action.ActionKind.Length > 128 ||
            string.IsNullOrEmpty(action.ActionKey) ||
            action.ActionKey.Length > 128 ||
            action.PayloadVersion <= 0)
        {
            throw new ArgumentException(
                "Action kinds and keys require 1–128 characters and payload versions must be positive.",
                nameof(action));
        }
    }

    private static ActionIdentity Identity(BotAction action) =>
        new(action.SourceUri, action.ActionKind, action.ActionKey);

    private static string? GetRootUri(string recordJson)
    {
        using JsonDocument record = JsonDocument.Parse(recordJson);
        if (record.RootElement.TryGetProperty("reply", out JsonElement reply) &&
            reply.TryGetProperty("root", out JsonElement root) &&
            root.TryGetProperty("uri", out JsonElement uri))
        {
            return uri.GetString();
        }

        return null;
    }

    private readonly record struct ActionIdentity(AtUri SourceUri, string Kind, string Key);

    private sealed record WorkState(
        BotAction Action,
        DateTimeOffset NextRetryAt,
        DateTimeOffset? CompletedAt,
        string? RootUri,
        string? RemoteUri,
        string? Outcome);

    private readonly record struct RateReservation(DateTimeOffset At, int Points, bool Session);

    private readonly record struct SkippedData(DateTimeOffset RecordedAt, long? Sequence, string Scope, string Reason);
}
