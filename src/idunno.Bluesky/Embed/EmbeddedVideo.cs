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
        /// <param name="presentation">A hint to the client about how to present the video.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="video"/> is null.</exception>
        [JsonConstructor]
        public EmbeddedVideo(
            Blob video,
            ICollection<Caption>? captions = null,
            string? altText = null,
            AspectRatio? aspectRatio = null,
            string? presentation = null)
        {
            ArgumentNullException.ThrowIfNull(video);

            Video = video;
            Captions = captions;
            AltText = altText;
            AspectRatio = aspectRatio;
            Presentation = presentation;
        }

        /// <summary>
        /// Constructs a new instance of <see cref="EmbeddedVideo"/>
        /// </summary>
        /// <param name="video">The <see cref="Blob"/> containing the video.</param>
        /// <param name="captions">A collection of <see cref="Caption"/>s for the video, if any.</param>
        /// <param name="altText">The alternative text for the video, if any.</param>
        /// <param name="aspectRatio">The <see cref="AspectRatio"/> of the video, if any.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="video"/> is null.</exception>
        // 1.4.0 BACKCOMPAT OVERLOAD -- DO NOT TOUCH
        public EmbeddedVideo(
            Blob video,
            ICollection<Caption>? captions,
            string? altText,
            AspectRatio? aspectRatio) : this(video, captions, altText, aspectRatio, presentation: null)
        {
            ArgumentNullException.ThrowIfNull(video);
        }

        /// <summary>
        /// Constructs a new instance of <see cref="EmbeddedVideo"/>
        /// </summary>
        /// <param name="video">The <see cref="Blob"/> containing the video.</param>
        /// <param name="caption">A <see cref="Caption"/> for the video, if any.</param>
        /// <param name="altText">The alternative text for the video, if any.</param>
        /// <param name="aspectRatio">The <see cref="AspectRatio"/> of the video, if any.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="video"/> or <paramref name="caption"/> is null.</exception>
        // 1.4.0 BACKCOMPAT OVERLOAD -- DO NOT TOUCH
        public EmbeddedVideo(
            Blob video,
            Caption caption,
            string? altText,
            AspectRatio? aspectRatio) : this(video, [caption], altText, aspectRatio, presentation: null)
        {
            ArgumentNullException.ThrowIfNull(video);
            ArgumentNullException.ThrowIfNull(caption);
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

        /// <summary>
        /// Gets a hint to the client about how to present the video.
        /// Known values are provided by <see cref="VideoPresentationKnownValues"/>, but may contain any value.
        /// </summary>
        [JsonInclude]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Presentation { get; init; }
    }

    /// <summary>
    /// Known values for the <see cref="EmbeddedVideo.Presentation"/> property.
    /// </summary>
    public static class VideoPresentationKnownValues
    {
        /// <summary>
        /// The default presentation hint for an embedded video.
        /// </summary>
        public static string Default => "default";

        /// <summary>
        /// Hint to the client the presentation should like a GIF.
        /// </summary>
        public static string Gif => "gif";
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
