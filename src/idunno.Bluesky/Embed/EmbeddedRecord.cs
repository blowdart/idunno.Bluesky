// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

using idunno.AtProto.Repo;

namespace idunno.Bluesky.Embed;

/// <summary>
/// Represents an embedded record pointing to a strongly referenced record, such as another post.
/// </summary>
public record EmbeddedRecord : EmbeddedBase
{
    /// <summary>
    /// Creates a new instance of <see cref="EmbeddedRecord"/>.
    /// </summary>
    /// <param name="record">A <see cref="StrongReference"/> to the record to embed.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="record"/> is <see langword="null" />.</exception>
    public EmbeddedRecord(StrongReference record)
    {
        ArgumentNullException.ThrowIfNull(record);

        Record = record;
    }

    /// <summary>
    /// Gets a <see cref="StrongReference"/> to the record to embed.
    /// </summary>
    [JsonInclude]
    [JsonRequired]
    public StrongReference Record { get; init; }
}