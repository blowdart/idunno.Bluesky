// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.Bluesky.Labeler.Moderation;

namespace idunno.Bluesky.Labeler.Administration;

/// <summary>Separates authorized moderation searches from host-owned indexing of public subject identities.</summary>
internal interface ILabelSearchStore
{
    /// <summary>Searches normalized retained text and fresh verified handles under live inspection authority.</summary>
    Task<LabelServiceResult<LabelSearchPage>> SearchAsync(
        LabelAdministrationCaller caller,
        string query,
        long page,
        CancellationToken cancellationToken);

    /// <summary>Reads at most thirty-two public identity lookup candidates; no report or appeal text leaves storage.</summary>
    Task<LabelServiceResult<LabelHandleIndexBatch>> ReadHandleIndexBatchAsync(CancellationToken cancellationToken);

    /// <summary>Persists a verified handle or an explicit failed lookup without allowing an older observation to overwrite a newer one.</summary>
    Task<LabelServiceResult<LabelHandleIndexBatch>> SaveHandleIndexBatchAsync(
        LabelHandleIndexBatch batch,
        CancellationToken cancellationToken);
}

/// <summary>Retains an opaque case reference and its public repository DID for background verification.</summary>
internal sealed record LabelHandleIndexEntry(long CaseId, string Did, string? Handle = null);

/// <summary>Fences a bounded set of public handle observations with the database clock sampled before lookup.</summary>
internal sealed record LabelHandleIndexBatch(long ObservedAt, IReadOnlyList<LabelHandleIndexEntry> Entries);

/// <summary>Contains one report or appeal match without reconstructing the original evidence.</summary>
internal sealed record LabelSearchHit(
    LabelModerationReport? Report = null,
    LabelAppeal? Appeal = null,
    string? MatchText = null,
    long? RelatedReportId = null);

/// <summary>Contains a counted page plus explicit handle-index coverage rather than claiming incomplete handle results are exhaustive.</summary>
internal sealed record LabelSearchPage(
    long Page,
    long Total,
    IReadOnlyList<LabelSearchHit> Hits,
    long PendingHandleCases,
    long UnavailableHandleCases);
