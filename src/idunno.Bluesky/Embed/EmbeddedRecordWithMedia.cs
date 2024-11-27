// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

using idunno.AtProto.Repo;

namespace idunno.Bluesky.Embed
{
    /// <summary>
    /// An embedded record with media.
    /// </summary>
    public record EmbeddedRecordWithMedia : EmbeddedRecord
    {
        /// <summary>
        /// Creates a new <see cref="EmbeddedRecordWithMedia"/>
        /// </summary>
        /// <param name="record">The embedded record.</param>
        /// <param name="media">The media in the record.</param>
        [JsonConstructor]
        public EmbeddedRecordWithMedia(StrongReference record, EmbeddedMediaBase media) : base(record)
        {
            Media = media;
        }

        /// <summary>
        /// The media in the record.
        /// </summary>
        [JsonInclude]
        public EmbeddedMediaBase Media { get; init; }
    }
}
