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
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="video"/> is null.</exception>
        [JsonConstructor]
        public EmbeddedVideo(
            Blob video,
            ICollection<Caption>? captions = null,
            string? altText = null,
            AspectRatio? aspectRatio = null)
        {
            ArgumentNullException.ThrowIfNull(video);

            Video = video;
            Captions = captions;
            AltText = altText;
            AspectRatio = aspectRatio;
        }

        /// <summary>
        /// Constructs a new instance of <see cref="EmbeddedVideo"/>
        /// </summary>
        /// <param name="video">The <see cref="Blob"/> containing the video.</param>
        /// <param name="captions">A <see cref="Caption"/> for the video, if any.</param>
        /// <param name="altText">The alternative text for the video, if any.</param>
        /// <param name="aspectRatio">The <see cref="AspectRatio"/> of the video, if any.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="video"/> or <paramref name="captions"/> is null.</exception>
        public EmbeddedVideo(
            Blob video,
            Caption captions,
            string? altText = null,
            AspectRatio? aspectRatio = null) : this(video, new List<Caption>() { captions }, altText, aspectRatio)
        {
            ArgumentNullException.ThrowIfNull(video);
            ArgumentNullException.ThrowIfNull(captions);
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
    /// <remarks>
    /// <para>Captions are in the <see href="https://www.w3.org/TR/webvtt1/">VTT</see> format.</para>
    /// </remarks>
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
