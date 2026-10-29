// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;

namespace idunno.Bluesky.Labeler.Moderation;

/// <summary>Exposes only the host-owned, revision-fenced deleted-post review workflow, never a browser or remote command.</summary>
internal interface ILabelDeletedPostStore
{
    /// <summary>Reads a bounded batch due for authoritative existence checks, resuming persisted scheduling after restart.</summary>
    Task<LabelServiceResult<LabelDeletedPostBatch>> ReadDeletedPostBatchAsync(CancellationToken cancellationToken);

    /// <summary>Records a check and, only after authoritative deletion confirmation, atomically closes unchanged case work.</summary>
    Task<LabelServiceResult<LabelDeletedPostCheck>> CompleteDeletedPostCheckAsync(
        LabelDeletedPostCandidate candidate,
        long observedAt,
        bool deleted,
        CancellationToken cancellationToken);
}

/// <summary>Retains the exact workflow revision observed before a public PDS lookup.</summary>
internal sealed record LabelDeletedPostCandidate(long CaseId, long Revision, AtUri Uri);

/// <summary>Contains one database-clock observation and at most sixteen due cases.</summary>
internal sealed record LabelDeletedPostBatch(long ObservedAt, IReadOnlyList<LabelDeletedPostCandidate> Entries);

/// <summary>Distinguishes a completed closure from a check that left review work unchanged.</summary>
internal sealed record LabelDeletedPostCheck(long CaseId, bool Closed);
