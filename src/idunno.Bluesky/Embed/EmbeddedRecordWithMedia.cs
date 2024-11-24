// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

using idunno.AtProto.Repo;

namespace idunno.Bluesky.Embed
{
    public record EmbeddedRecordWithMedia : EmbeddedRecord
    {
        [JsonConstructor]
        public EmbeddedRecordWithMedia(StrongReference record, EmbeddedMediaBase media) : base(record)
        {
            Media = media;
        }

        [JsonInclude]
        public EmbeddedMediaBase Media { get; init; }
    }
}
