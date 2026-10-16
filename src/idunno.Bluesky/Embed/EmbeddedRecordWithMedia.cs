// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.Bluesky.Embed;

/// <summary>
/// An embedded record with media.
/// </summary>
public record EmbeddedRecordWithMedia : EmbeddedMediaBase
{
    /// <summary>
    /// Creates a new <see cref="EmbeddedRecordWithMedia"/>
    /// </summary>
    /// <param name="record">The embedded record.</param>
    /// <param name="media">The media in the record.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="record"/> or <paramref name="media"/> is <see langword="null"/>.</exception>
    [JsonConstructor]
    public EmbeddedRecordWithMedia(EmbeddedRecord record, EmbeddedMediaBase media)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(media);

        Media = media;
        Record = record;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="EmbeddedRecordWithMedia"/> class containing an external card.
    /// </summary>
    /// <param name="record">The record to embed.</param>
    /// <param name="media">The external card or media to attach to the record.</param>
    /// <exception cref="ArgumentNullException"><paramref name="record"/> or <paramref name="media"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="media"/> is neither an external card nor embedded media.</exception>
    /// <remarks>
    /// <para>The card is represented by <see cref="EmbeddedExternalMedia"/> in <see cref="Media"/>.</para>
    /// </remarks>
    public EmbeddedRecordWithMedia(EmbeddedRecord record, EmbeddedBase media)
        : this(record, media switch
        {
            EmbeddedExternal externalCard => new EmbeddedExternalMedia(externalCard.External),
            EmbeddedMediaBase embeddedMedia => embeddedMedia,
            null => throw new ArgumentNullException(nameof(media)),
            _ => throw new ArgumentException("The embed must be media or an external card.", nameof(media))
        })
    {
    }

    /// <summary>
    /// Gets the media in the record.
    /// </summary>
    [JsonInclude]
    public EmbeddedMediaBase Media { get; init; }

    /// <summary>
    /// Gets the record to embed.
    /// </summary>
    [JsonInclude]
    public EmbeddedRecord Record { get; init; }
}
