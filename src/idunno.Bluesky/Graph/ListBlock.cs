// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

using idunno.AtProto;
using idunno.Bluesky.Record;

namespace idunno.Bluesky.Graph;

/// <summary>
/// Record representing a block relationship against an entire an entire list of accounts (actors).
/// </summary>
[JsonPolymorphic(
    IgnoreUnrecognizedTypeDiscriminators = false,
    UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization)]
[JsonDerivedType(typeof(ListBlock), RecordType.ListBlock)]
public record ListBlock : BlueskyTimestampedRecord
{
    /// <summary>
    /// Creates a new instance of the <see cref="ListBlock"/> record.
    /// </summary>
    /// <param name="subject">The reference <see cref="AtUri"/> to the mod list record.</param>
    /// <param name="createdAt">The timestamp when the record was created.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="subject"/> is <see langword="null"/>.</exception>
    public ListBlock(AtUri subject, DateTimeOffset createdAt)
        : base(createdAt)
    {
        ArgumentNullException.ThrowIfNull(subject);
        Subject = subject;
    }

    /// <summary>
    /// Creates a new instance of the <see cref="ListBlock"/> record.
    /// </summary>
    /// <param name="subject">The reference <see cref="AtUri"/> to the mod list record.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="subject"/> is <see langword="null"/>.</exception>
    public ListBlock(AtUri subject)
        : base()
    {
        ArgumentNullException.ThrowIfNull(subject);
        Subject = subject;
    }

    /// <summary>
    /// Gets the reference <see cref="AtUri"/> to the mod list record.
    /// </summary>
    [JsonInclude]
    [JsonRequired]
    public AtUri Subject { get; init; }
}
