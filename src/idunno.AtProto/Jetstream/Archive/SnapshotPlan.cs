// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.AtProto.Jetstream.Archive;

/// <summary>
/// Describes a page of the sealed Jetstream archive.
/// </summary>
public sealed record SnapshotPlan
{
    /// <summary>Gets the last sequence covered by this plan page.</summary>
    [JsonRequired]
    public required long PlannedThroughSeq { get; init; }

    /// <summary>Gets the sealed tip to pin for subsequent pages.</summary>
    [JsonRequired]
    public required long SealedTipSeq { get; init; }

    /// <summary>Gets the segments to download.</summary>
    [JsonRequired]
    public required IReadOnlyList<PlannedSegment> Segments { get; init; }

    /// <summary>Gets the planner statistics.</summary>
    [JsonRequired]
    public required SnapshotPlanStats Stats { get; init; }
}

/// <summary>
/// Describes a segment or selected block ranges in a snapshot plan.
/// </summary>
public sealed record PlannedSegment
{
    /// <summary>Gets the name of the segment.</summary>
    [JsonRequired]
    public required string Name { get; init; }

    /// <summary>Gets the zero-based segment index.</summary>
    [JsonRequired]
    public required long Index { get; init; }

    /// <summary>Gets the segment's hexadecimal metadata checksum.</summary>
    [JsonRequired]
    public required string Checksum { get; init; }

    /// <summary>Gets the first sequence in the segment.</summary>
    [JsonRequired]
    public required long MinSeq { get; init; }

    /// <summary>Gets the last sequence in the segment.</summary>
    [JsonRequired]
    public required long MaxSeq { get; init; }

    /// <summary>Gets the download mode, either <c>blocks</c> or <c>segment</c>.</summary>
    [JsonRequired]
    public required string Mode { get; init; }

    /// <summary>Gets inclusive block ranges when <see cref="Mode"/> is <c>blocks</c>.</summary>
    public IReadOnlyList<BlockRange>? Blocks { get; init; }
}

/// <summary>
/// Describes an inclusive range of block indices.
/// </summary>
public sealed record BlockRange
{
    /// <summary>Gets the first block index.</summary>
    [JsonRequired]
    public required int First { get; init; }

    /// <summary>Gets the last block index.</summary>
    [JsonRequired]
    public required int Last { get; init; }
}

/// <summary>
/// Describes the work examined by the snapshot planner.
/// </summary>
public sealed record SnapshotPlanStats
{
    /// <summary>Gets the number of segments examined.</summary>
    [JsonRequired]
    public required long SegmentsExamined { get; init; }

    /// <summary>Gets the number of segments matched.</summary>
    [JsonRequired]
    public required long SegmentsMatched { get; init; }

    /// <summary>Gets the number of blocks matched.</summary>
    [JsonRequired]
    public required long BlocksMatched { get; init; }

    /// <summary>Gets the number of entries counted toward the page limit.</summary>
    [JsonRequired]
    public required long Entries { get; init; }
}

/// <summary>
/// Describes a page of sealed segments available on a Jetstream server.
/// </summary>
public sealed record SegmentList
{
    /// <summary>Gets the cursor for the following page, if any.</summary>
    public string? Cursor { get; init; }

    /// <summary>Gets the segments in this page.</summary>
    [JsonRequired]
    public required IReadOnlyList<SegmentInfo> Segments { get; init; }
}

/// <summary>
/// Describes a sealed segment in the Jetstream archive.
/// </summary>
public sealed record SegmentInfo
{
    /// <summary>Gets the segment filename.</summary>
    [JsonRequired]
    public required string Name { get; init; }

    /// <summary>Gets the segment index.</summary>
    [JsonRequired]
    public required long Index { get; init; }

    /// <summary>Gets the segment size in bytes.</summary>
    [JsonRequired]
    public required long SizeBytes { get; init; }

    /// <summary>Gets the segment metadata checksum.</summary>
    [JsonRequired]
    public required string Checksum { get; init; }

    /// <summary>Gets the number of events in the segment.</summary>
    [JsonRequired]
    public required long EventCount { get; init; }

    /// <summary>Gets the first sequence.</summary>
    [JsonRequired]
    public required long MinSeq { get; init; }

    /// <summary>Gets the last sequence.</summary>
    [JsonRequired]
    public required long MaxSeq { get; init; }

    /// <summary>Gets the earliest witnessed time in Unix microseconds.</summary>
    [JsonRequired]
    public required long MinWitnessedAt { get; init; }

    /// <summary>Gets the latest witnessed time in Unix microseconds.</summary>
    [JsonRequired]
    public required long MaxWitnessedAt { get; init; }
}
