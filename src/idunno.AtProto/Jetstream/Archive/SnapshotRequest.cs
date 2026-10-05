// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;

namespace idunno.AtProto.Jetstream.Archive;

/// <summary>
/// Specifies the archive events to plan and decode.
/// </summary>
public sealed record SnapshotRequest
{
    /// <summary>Gets or sets the event kinds to include; empty includes every kind.</summary>
    public IReadOnlyList<JetStreamEventKind>? Kinds { get; init; }

    /// <summary>Gets or sets the repository DIDs to include; empty includes every DID.</summary>
    public IReadOnlyList<Did>? Dids { get; init; }

    /// <summary>Gets or sets collection selectors for record events; empty includes every collection.</summary>
    public IReadOnlyList<CollectionSelector>? Collections { get; init; }

    /// <summary>Gets or sets the exclusive starting sequence.</summary>
    public long? AfterSeq { get; init; }

    /// <summary>Gets or sets the inclusive upper sequence bound.</summary>
    public long? BeforeSeq { get; init; }

    internal SnapshotRequest SnapshotFilters() => this with
    {
        Kinds = Kinds is null ? null : Array.AsReadOnly(Kinds.ToArray()),
        Dids = Dids is null ? null : Array.AsReadOnly(Dids.ToArray()),
        Collections = Collections is null ? null : Array.AsReadOnly(Collections.ToArray())
    };

    internal string Fingerprint(Uri service)
    {
        using MemoryStream stream = new();
        using (BinaryWriter writer = new(stream, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write(service.Scheme);
            writer.Write(service.IdnHost);
            writer.Write(service.Port);
            writer.Write(AfterSeq ?? -1);
            writer.Write(BeforeSeq ?? -1);
            WriteValues(writer, Kinds?.Select(kind => ((int)kind).ToString(System.Globalization.CultureInfo.InvariantCulture)));
            WriteValues(writer, Dids?.Select(did => did.Value));
            WriteValues(writer, Collections?.Select(collection => collection.ToString()));
        }

        return Convert.ToHexString(SHA256.HashData(stream.ToArray()));
    }

    private static void WriteValues(BinaryWriter writer, IEnumerable<string>? values)
    {
        string[] sorted = values?.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray() ?? [];
        writer.Write(sorted.Length);
        foreach (string value in sorted)
        {
            writer.Write(value);
        }
    }
}

/// <summary>
/// Represents durably persistable at-least-once snapshot progress.
/// </summary>
/// <remarks>
/// <para>Persist this value only after the events preceding it have been handled. A resumed run may repeat events
/// from an interrupted block. If a new plan detects a changed segment checksum after compaction, the segment is read again.
/// A generation mismatch during download stops enumeration; resume a new enumeration from the latest persisted checkpoint
/// to obtain a fresh plan rather than changing the saved checksum or byte offset.</para>
/// </remarks>
public sealed record SnapshotCheckpoint
{
    /// <summary>Gets the fingerprint of the original request and service used to create this checkpoint.</summary>
    public string? RequestFingerprint { get; init; }

    /// <summary>Gets the exclusive replay fallback starting sequence, if it differs from the original request.</summary>
    public long? ReplayAfterSeq { get; init; }
    /// <summary>Gets the pinned sealed tip for this snapshot.</summary>
    [JsonRequired]
    public required long SealedTipSeq { get; init; }

    /// <summary>Gets the exclusive starting sequence of the current plan page.</summary>
    [JsonRequired]
    public required long PlanAfterSeq { get; init; }

    /// <summary>Gets the segment currently being processed, if any.</summary>
    public string? SegmentName { get; init; }

    /// <summary>Gets the segment checksum used for this checkpoint, if any.</summary>
    public string? SegmentChecksum { get; init; }

    /// <summary>Gets the next block index in this segment.</summary>
    public int NextBlockIndex { get; init; }

    /// <summary>Gets the byte offset of the next frame in a whole-segment download.</summary>
    public long NextByteOffset { get; init; }

    /// <summary>Gets the last live sequence handled after the archive-to-live handoff, if any.</summary>
    /// <remarks><para>This cursor is exclusive on resume; the live server may repeat it, so consumers must handle
    /// at-least-once delivery.</para></remarks>
    public long? LiveAfterSeq { get; init; }
}
