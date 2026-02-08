// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics;
using System.Text.Json.Serialization;

using idunno.AtProto;
using idunno.AtProto.Labels;

namespace idunno.Bluesky.Drafts
{
    /// <summary>
    /// Encapsulates the content of a draft post, including text and any embedded media or records. This is used to create a new draft or update an existing draft.
    /// </summary>
    /// <remarks>
    ///<para>See <see hcref="https://github.com/bluesky-social/atproto/blob/main/lexicons/app/bsky/draft/defs.json"/>draft/defs.json</para>
    /// </remarks>
    public record DraftPost
    {
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private const string PornLabelName = "porn";

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private const string SexualLabelName = "sexual";

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private const string GraphicMediaLabelName = "graphic-media";

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private const string NudityLabelName = "nudity";

        /// <summary>
        /// Creates a new instance of <see cref="DraftPost"/> with the specified content and optional embedded media and labels.
        /// </summary>
        /// <param name="text">The primary post content.</param>
        /// <param name="labels">The labels to apply to the post.</param>
        /// <param name="embedImages">The images to embed in the post. (Maximum 4)</param>
        /// <param name="embedVideos">The videos to embed in the post. (Maximum 1)</param>
        /// <param name="embedExternals">The external content to embed in the post. (Maximum 1)</param>
        /// <param name="embedRecords">The records to embed in the post. (Maximum 1)</param>
        /// <exception cref="ArgumentException">Thrown when the text is null or empty.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the text length is greater than 3000 characters or 300 graphemes, or when the number of embedded media or records exceeds the specified limits.</exception>
        [JsonConstructor]
        public DraftPost(
            string text,
            SelfLabels? labels,
            ICollection<DraftEmbedImage>? embedImages,
            ICollection<DraftEmbedVideo>? embedVideos,
            ICollection<DraftEmbedExternal>? embedExternals,
            ICollection<DraftEmbedRecord>? embedRecords)
        {
            ArgumentException.ThrowIfNullOrEmpty(text);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(
                text.Length,
                3000);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(
                text.GetGraphemeLength(),
                300);

            ArgumentOutOfRangeException.ThrowIfGreaterThan(
                embedImages?.Count ?? 0,
                4);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(
                embedVideos?.Count ?? 0,
                1);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(
                embedExternals?.Count ?? 0,
                1);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(
                embedRecords?.Count ?? 0,
                1);

            Text = text;
            Labels = labels;
            EmbedImages = embedImages;
            EmbedVideos = embedVideos;
            EmbedExternals = embedExternals;
            EmbedRecords = embedRecords;
        }

        /// <summary>
        /// Creates a new instance of <see cref="DraftPost"/> with the specified content.
        /// </summary>
        /// <param name="text">The primary post content.</param>
        /// <exception cref="ArgumentException">Thrown when the text is null or empty.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the text length is greater than 3000 characters or 300 graphemes.</exception>
        public DraftPost(string text) : this(
            text: text,
            labels: null,
            embedImages: null,
            embedVideos: null,
            embedExternals: null,
            embedRecords: null)
        {
            ArgumentException.ThrowIfNullOrEmpty(text);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(
                text.Length,
                3000);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(
                text.GetGraphemeLength(),
                300);
        }

        /// <summary>
        /// Creates a new instance of <see cref="DraftPost"/> with the specified content and embedded images.
        /// </summary>
        /// <param name="text">The primary post content.</param>
        /// <param name="embedImages">The images to embed in the post. (Maximum 4)</param>
        /// <exception cref="ArgumentException">Thrown when the text is null or empty.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the text length is greater than 3000 characters or 300 graphemes, or when the number of embedded images exceeds 4.</exception>
        public DraftPost(string text, ICollection<DraftEmbedImage> embedImages) : this(
            text: text,
            labels: null,
            embedImages: embedImages,
            embedVideos: null,
            embedExternals: null,
            embedRecords: null)
        {
            ArgumentException.ThrowIfNullOrEmpty(text);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(
                text.Length,
                3000);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(
                text.GetGraphemeLength(),
                300);

            ArgumentOutOfRangeException.ThrowIfGreaterThan(
                embedImages?.Count ?? 0,
                4);
        }

        /// <summary>
        /// Creates a new instance of <see cref="DraftPost"/> with the specified content and embedded image.
        /// </summary>
        /// <param name="text">The primary post content.</param>
        /// <param name="embedImage">The image to embed in the post.</param>
        /// <exception cref="ArgumentException">Thrown when the text is null or empty.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the text length is greater than 3000 characters or 300 graphemes.</exception>
        public DraftPost(string text, DraftEmbedImage embedImage) : this(
            text: text,
            labels: null,
            embedImages: [embedImage],
            embedVideos: null,
            embedExternals: null,
            embedRecords: null)
        {
            ArgumentException.ThrowIfNullOrEmpty(text);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(
                text.Length,
                3000);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(
                text.GetGraphemeLength(),
                300);
        }

        /// <summary>
        /// Creates a new instance of <see cref="DraftPost"/> with the specified content and labels.
        /// </summary>
        /// <param name="text">The primary post content.</param>
        /// <param name="labels">The labels to apply to the post.</param>
        /// <exception cref="ArgumentException">Thrown when the text is null or empty.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the text length is greater than 3000 characters or 300 graphemes.</exception>
        public DraftPost(string text, SelfLabels labels) : this(
            text: text,
            labels: labels,
            embedImages: null,
            embedVideos: null,
            embedExternals: null,
            embedRecords: null)
        {
            ArgumentException.ThrowIfNullOrEmpty(text);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(
                text.Length,
                3000);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(
                text.GetGraphemeLength(),
                300);
        }

        /// <summary>
        /// Creates a new instance of <see cref="DraftPost"/> with the specified content and embedded video.
        /// </summary>
        /// <param name="text">The primary post content.</param>
        /// <param name="embedVideo">The video to embed in the post.</param>
        /// <exception cref="ArgumentException">Thrown when the text is null or empty.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the text length is greater than 3000 characters or 300 graphemes.</exception>
        public DraftPost(string text, DraftEmbedVideo embedVideo) : this(
            text: text,
            labels: null,
            embedImages: null,
            embedVideos: [embedVideo],
            embedExternals: null,
            embedRecords: null)
        {
            ArgumentException.ThrowIfNullOrEmpty(text);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(
                text.Length,
                3000);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(
                text.GetGraphemeLength(),
                300);
        }

        /// <summary>
        /// Gets or sets the the primary post content. Maximum 3000 characters and 300 graphemes. Cannot be null or empty.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown when setting if the value is null or empty.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when setting if the value length is greater than 3000 characters or 300 graphemes.</exception>
        [JsonRequired]
        public string Text
        {
            get;

            set
            {
                ArgumentException.ThrowIfNullOrEmpty(value);
                ArgumentOutOfRangeException.ThrowIfGreaterThan(
                    value.Length,
                    3000);
                ArgumentOutOfRangeException.ThrowIfGreaterThan(
                    value.GetGraphemeLength(),
                    300);
                field = value;
            }
        }

        /// <summary>
        /// Gets the self-label values for this draft. Effectively content warnings.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public SelfLabels? Labels { get; internal set; }

        /// <summary>
        /// Gets the embedded images for this draft.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public ICollection<DraftEmbedImage>? EmbedImages { get; init; }

        /// <summary>
        /// Gets the embedded videos for this draft.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public ICollection<DraftEmbedVideo>? EmbedVideos { get; init; }

        /// <summary>
        /// Gets the embedded external content for this draft.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public ICollection<DraftEmbedExternal>? EmbedExternals { get; init; }

        /// <summary>
        /// Gets the embedded records for this draft.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public ICollection<DraftEmbedRecord>? EmbedRecords { get; init; }

        /// <summary>
        /// Gets or sets a flag indicating the post media contains porn.
        /// This puts a warning on images and can only be clicked through if the user is 18+ and has enabled adult content.
        /// </summary>
        [JsonIgnore]
        public bool ContainsPorn
        {
            get
            {
                if (Labels is not null)
                {
                    return Labels.Contains(PornLabelName);
                }
                else
                {
                    return false;
                }
            }

            set
            {
                Labels ??= new SelfLabels();
                if (value)
                {
                    Labels.AddLabel(PornLabelName);
                }
                else
                {
                    Labels.RemoveLabel(PornLabelName);
                }
            }
        }

        /// <summary>
        /// Gets or sets a flag indicating the post media contains sexual content.
        /// This behaves like <see cref="ContainsPorn"/> but is meant to handle less intense sexual content.
        /// </summary>
        [JsonIgnore]
        public bool ContainsSexualContent
        {
            get
            {
                if (Labels is not null)
                {
                    return Labels.Contains(SexualLabelName);
                }
                else
                {
                    return false;
                }
            }

            set
            {
                Labels ??= new SelfLabels();
                if (value)
                {
                    Labels.AddLabel(SexualLabelName);
                }
                else
                {
                    Labels.RemoveLabel(SexualLabelName);
                }
            }
        }

        /// <summary>
        /// Gets or sets a flag indicating the post media contains graphic media.
        /// This behaves like <see cref="ContainsPorn"/> but is for violence or gore.
        /// </summary>
        [JsonIgnore]
        public bool ContainsGraphicMedia
        {
            get
            {
                if (Labels is null)
                {
                    return false;
                }

                return Labels.Contains(GraphicMediaLabelName);
            }

            set
            {
                Labels ??= new SelfLabels();
                if (value)
                {
                    Labels.AddLabel(GraphicMediaLabelName);
                }
                else
                {
                    Labels.RemoveLabel(GraphicMediaLabelName);
                }
            }
        }

        /// <summary>
        /// Gets or sets a flag indicating the post media contains nudity.
        /// This puts a warning on images but isn't 18+ and defaults to ignore.
        /// </summary>
        [JsonIgnore]
        public bool ContainsNudity
        {
            get
            {
                if (Labels is null)
                {
                    return false;
                }

                return Labels.Contains(NudityLabelName);
            }

            set
            {
                Labels ??= new SelfLabels();

                if (value)
                {
                    Labels.AddLabel(NudityLabelName);
                }
                else
                {
                    Labels.RemoveLabel(NudityLabelName);
                }
            }
        }

        /// <summary>
        /// Sets the self labels for the post to the values specified in <paramref name="labels"/>.
        /// </summary>
        /// <param name="labels">The self label values to set.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="labels"/> is null.</exception>
        public void SetSelfLabels(PostSelfLabels labels)
        {
            ArgumentNullException.ThrowIfNull(labels);

            ContainsPorn = labels.Porn;
            ContainsSexualContent = labels.SexualContent;
            ContainsGraphicMedia = labels.GraphicMedia;
            ContainsNudity = labels.Nudity;
        }
    }
}
