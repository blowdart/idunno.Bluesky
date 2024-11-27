// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

using idunno.AtProto.Repo;

namespace idunno.Bluesky.Embed
{
    /// <summary>
    /// Defines the record structure for embedding videos in a post.
    /// </summary>
    public record EmbeddedVideo : EmbeddedMediaBase
    {
        /// <summary>
        /// Constructs a new instance of <see cref="EmbeddedVideo"/>
        /// </summary>
        /// <param name="video">The <see cref="Blob"/> containing the video.</param>
        /// <param name="captions">A collection of <see cref="Caption"/>s for the video, if any.</param>
        /// <param name="altText">The alternative text for the video, if any.</param>
        /// <param name="aspectRatio">The <see cref="AspectRatio"/> of the video, if any.</param>
        [JsonConstructor]
        public EmbeddedVideo(Blob video, ICollection<Caption>? captions, string? altText, AspectRatio? aspectRatio)
        {
            Video = video;
            Captions = captions;
            AltText = altText;
            AspectRatio = aspectRatio;
        }

        /// <summary>
        /// Gets the <see cref="Blob"/> information for the video.
        /// </summary>
        [JsonInclude]
        public Blob Video { get; init; }

        /// <summary>
        /// Gets a collection of <see cref="Caption"/>s for the video, if any.
        /// </summary>
        [JsonInclude]
        public ICollection<Caption>? Captions { get; init; }

        /// <summary>
        /// Gets the alternative text for the video, if any.
        /// </summary>
        [JsonInclude]
        [JsonPropertyName("alt")]
        public string? AltText { get; init; }

        /// <summary>
        /// Gets the <see cref="AspectRatio"/> of the video, if any.
        /// </summary>
        [JsonInclude]
        public AspectRatio? AspectRatio { get; init; }
    }

    /// <summary>
    /// Holds caption information for a video.
    /// </summary>
    public record Caption
    {
        /// <summary>
        /// Creates a new instance of <see cref="Caption"/>.
        /// </summary>
        /// <param name="lang">The language for the caption file.</param>
        /// <param name="file">The blob containing the caption file.</param>
        [JsonConstructor]
        public Caption(string lang, Blob file)
        {
            Lang = lang;
            File = file;
        }

        /// <summary>
        /// Gets the language for the caption file.
        /// </summary>
        [JsonInclude]
        public string Lang { get; init; }

        /// <summary>
        /// Gets the blob containing the caption file.
        /// </summary>
        [JsonInclude]
        public Blob File { get; init; }
    }
}
