// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

using idunno.AtProto.Repo;

namespace idunno.Bluesky.Embed
{
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
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="record"/> or <paramref name="media"/> is null.</exception>
        [JsonConstructor]
        public EmbeddedRecordWithMedia(StrongReference record, EmbeddedMediaBase media)
        {
            ArgumentNullException.ThrowIfNull(record);
            ArgumentNullException.ThrowIfNull(media);

            Media = media;
            Record = new EmbeddedRecord(record);
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
        public EmbeddedBase Record { get; init; }
    }
}
