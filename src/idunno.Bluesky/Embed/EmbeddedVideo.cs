// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

using idunno.AtProto.Repo;

namespace idunno.Bluesky.Embed
{
    public record EmbeddedVideo : EmbeddedMediaBase
    {
        [JsonConstructor]
        public EmbeddedVideo(Blob video, ICollection<Caption> captions, string altText, AspectRatio? aspectRatio)
        {
            Video = video;
            Captions = captions;
            AltText = altText;
            AspectRatio = aspectRatio;
        }

        [JsonInclude]
        public Blob Video { get; init; }

        [JsonInclude]
        public ICollection<Caption> Captions { get; init; }

        [JsonInclude]
        [JsonPropertyName("alt")]
        public string AltText { get; init; }

        [JsonInclude]
        public AspectRatio? AspectRatio { get; init; }
    }

    public record Caption
    {
        [JsonConstructor]
        public Caption(string lang, Blob file)
        {
            Lang = lang;
            File = file;
        }

        [JsonInclude]
        public string Lang { get; init; }

        [JsonInclude]
        public Blob File { get; init; }
    }
}
