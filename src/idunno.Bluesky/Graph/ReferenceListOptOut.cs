// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

using idunno.AtProto;
using idunno.Bluesky.Record;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace idunno.Bluesky.Graph;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Record requesting that its author be omitted from the public presentation of a reference list.
/// This record is only enforced when the subject list's current purpose is app.bsky.graph.defs#referencelist.
/// AppView indexes at most one record per actor and list pair, and ignores duplicate records.
/// </summary>
[JsonPolymorphic(IgnoreUnrecognizedTypeDiscriminators = false, UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization)]
[JsonDerivedType(typeof(ReferenceListOptOut), RecordType.ReferenceListOptOut)]
public record ReferenceListOptOut: BlueskyTimestampedRecord
{
    /// <summary>
    /// Creates a new instance of the <see cref="ReferenceListOptOut"/> record.
    /// </summary>
    /// <param name="subject">The canonical, DID-based AT URI of the app.bsky.graph.list record from which the author requests omission.</param>
    /// <param name="createdAt">The timestamp when the record was created.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="subject"/> is <see langword="null"/>.</exception>
    [JsonConstructor]
    public ReferenceListOptOut(AtUri subject, DateTimeOffset createdAt)
        : base(createdAt)
    {
        ArgumentNullException.ThrowIfNull(subject);
        Subject = subject;
    }

    /// <summary>
    /// Creates a new instance of the <see cref="ReferenceListOptOut"/> record.
    /// </summary>
    /// <param name="subject">The canonical, DID-based AT URI of the app.bsky.graph.list record from which the author requests omission.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="subject"/> is <see langword="null"/>.</exception>
    public ReferenceListOptOut(AtUri subject)
        : base()
    {
        ArgumentNullException.ThrowIfNull(subject);
        Subject = subject;
    }

    /// <summary>
    /// Gets the canonical, DID-based AT URI of the app.bsky.graph.list record from which the author requests omission.
    /// </summary>
    public AtUri Subject { get; init; }
}
