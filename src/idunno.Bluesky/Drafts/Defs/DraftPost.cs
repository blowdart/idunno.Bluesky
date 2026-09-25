// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

using idunno.AtProto;
using idunno.AtProto.Labels;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace idunno.Bluesky.Drafts;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Encapsulates the content of a draft post, including text and any embedded media or records. This is used to create a new draft or update an existing draft.
/// </summary>
/// <remarks>
///<para>See <see hcref="https://github.com/bluesky-social/atproto/blob/main/lexicons/app/bsky/draft/defs.json"/>draft/defs.json</para>
/// </remarks>
[JsonPolymorphic(UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FallBackToNearestAncestor)]
[JsonDerivedType(typeof(DraftPost), typeDiscriminator: "app.bsky.draft.defs#draftPost")]
public record DraftPost
{
    /// <summary>
    /// Creates a new instance of <see cref="DraftPost"/> with the specified content and optional embedded media and labels.
    /// </summary>
    /// <param name="text">The primary post content. It has a higher limit than post contents to allow storing a larger text that can later be refined into smaller posts.</param>
    /// <param name="labels">The labels to apply to the post.</param>
    /// <param name="embedImages">The images to embed in the post. (Maximum <see cref="Maximum.DraftEmbedImages"/>)</param>
    /// <param name="embedGallery">The gallery to embed in the post.</param>
    /// <param name="embedVideos">The videos to embed in the post. (Maximum <see cref="Maximum.DraftEmbedVideos"/>)</param>
    /// <param name="embedExternals">The external content to embed in the post. (Maximum <see cref="Maximum.DraftEmbedExternals"/>)</param>
    /// <param name="embedRecords">The records to embed in the post. (Maximum <see cref="Maximum.DraftEmbedRecords"/>)</param>
    /// <exception cref="ArgumentException">Thrown when the text is <see langword="null"/> or empty.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the text length is greater than <see cref="Maximum.DraftTextLengthInBytes"/> UTF-8 bytes or <see cref="Maximum.DraftTextLengthInGraphemes"/> graphemes, or
    /// when the number of embedded media or records exceeds the specified limits.
    /// </exception>
    [JsonConstructor]
    public DraftPost(
        string text,
        SelfLabels? labels,
        IReadOnlyList<DraftEmbedImage>? embedImages,
        DraftEmbedGallery? embedGallery,
        IReadOnlyList<DraftEmbedVideo>? embedVideos,
        IReadOnlyList<DraftEmbedExternal>? embedExternals,
        IReadOnlyList<DraftEmbedRecord>? embedRecords)
    {
        ArgumentException.ThrowIfNullOrEmpty(text);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(
            text.GetUtf8Length(),
            Maximum.DraftTextLengthInBytes);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(
            text.GetGraphemeLength(),
            Maximum.DraftTextLengthInGraphemes);

        ArgumentOutOfRangeException.ThrowIfGreaterThan(
            embedImages?.Count ?? 0,
            Maximum.DraftEmbedImages);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(
            embedVideos?.Count ?? 0,
            Maximum.DraftEmbedVideos);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(
            embedExternals?.Count ?? 0,
            Maximum.DraftEmbedExternals);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(
            embedRecords?.Count ?? 0,
            Maximum.DraftEmbedRecords);

        Text = text;
        Labels = labels;
        EmbedImages = embedImages is null ? null : new List<DraftEmbedImage>(embedImages).AsReadOnly();
        EmbedGallery = embedGallery;
        EmbedVideos = embedVideos is null ? null : new List<DraftEmbedVideo>(embedVideos).AsReadOnly();
        EmbedExternals = embedExternals is null ? null : new List<DraftEmbedExternal>(embedExternals).AsReadOnly();
        EmbedRecords = embedRecords is null ? null : new List<DraftEmbedRecord>(embedRecords).AsReadOnly();
    }

    /// <summary>
    /// Creates a new instance of <see cref="DraftPost"/> with the specified content.
    /// </summary>
    /// <param name="text">The primary post content. It has a higher limit than post contents to allow storing a larger text that can later be refined into smaller posts.</param>
    /// <exception cref="ArgumentException">Thrown when the text is <see langword="null"/> or empty.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the text length is greater than <see cref="Maximum.DraftTextLengthInBytes"/> UTF-8 bytes or <see cref="Maximum.DraftTextLengthInGraphemes"/> graphemes.</exception>
    public DraftPost(string text) : this(
        text: text,
        labels: null,
        embedImages: null,
        embedGallery: null,
        embedVideos: null,
        embedExternals: null,
        embedRecords: null)
    {
        ArgumentException.ThrowIfNullOrEmpty(text);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(
            text.GetUtf8Length(),
            Maximum.DraftTextLengthInBytes);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(
            text.GetGraphemeLength(),
            Maximum.DraftTextLengthInGraphemes);
    }

    /// <summary>
    /// Creates a new instance of <see cref="DraftPost"/> with the specified content and embedded images.
    /// </summary>
    /// <param name="text">The primary post content. It has a higher limit than post contents to allow storing a larger text that can later be refined into smaller posts.</param>
    /// <param name="embedImages">The images to embed in the post. (Maximum <see cref="Maximum.DraftEmbedImages"/>)</param>
    /// <exception cref="ArgumentException">Thrown when the text is <see langword="null"/> or empty.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the text length is greater than <see cref="Maximum.DraftTextLengthInBytes"/> UTF-8 bytes or <see cref="Maximum.DraftTextLengthInGraphemes"/> graphemes, or when the number of embedded images exceeds <see cref="Maximum.DraftEmbedImages"/>.</exception>
    public DraftPost(string text, IReadOnlyList<DraftEmbedImage> embedImages) : this(
        text: text,
        labels: null,
        embedImages: embedImages,
        embedGallery: null,
        embedVideos: null,
        embedExternals: null,
        embedRecords: null)
    {
        ArgumentException.ThrowIfNullOrEmpty(text);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(
            text.GetUtf8Length(),
            Maximum.DraftTextLengthInBytes);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(
            text.GetGraphemeLength(),
            Maximum.DraftTextLengthInGraphemes);

        ArgumentOutOfRangeException.ThrowIfGreaterThan(
            embedImages?.Count ?? 0,
            Maximum.DraftEmbedImages);
    }

    /// <summary>
    /// Creates a new instance of <see cref="DraftPost"/> with the specified content and embedded image.
    /// </summary>
    /// <param name="text">The primary post content. It has a higher limit than post contents to allow storing a larger text that can later be refined into smaller posts.</param>
    /// <param name="embedImage">The image to embed in the post.</param>
    /// <exception cref="ArgumentException">Thrown when the text is <see langword="null"/> or empty.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the text length is greater than <see cref="Maximum.DraftTextLengthInBytes"/> UTF-8 bytes or <see cref="Maximum.DraftTextLengthInGraphemes"/> graphemes.</exception>
    public DraftPost(string text, DraftEmbedImage embedImage) : this(
        text: text,
        labels: null,
        embedImages: [embedImage],
        embedGallery: null,
        embedVideos: null,
        embedExternals: null,
        embedRecords: null)
    {
        ArgumentException.ThrowIfNullOrEmpty(text);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(
            text.GetUtf8Length(),
            Maximum.DraftTextLengthInBytes);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(
            text.GetGraphemeLength(),
            Maximum.DraftTextLengthInGraphemes);
    }

    /// <summary>
    /// Creates a new instance of <see cref="DraftPost"/> with the specified content and labels.
    /// </summary>
    /// <param name="text">The primary post content. It has a higher limit than post contents to allow storing a larger text that can later be refined into smaller posts.</param>
    /// <param name="labels">The labels to apply to the post.</param>
    /// <exception cref="ArgumentException">Thrown when the text is <see langword="null"/> or empty.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the text length is greater than <see cref="Maximum.DraftTextLengthInBytes"/> UTF-8 bytes or <see cref="Maximum.DraftTextLengthInGraphemes"/> graphemes.</exception>
    public DraftPost(string text, SelfLabels labels) : this(
        text: text,
        labels: labels,
        embedImages: null,
        embedGallery: null,
        embedVideos: null,
        embedExternals: null,
        embedRecords: null)
    {
        ArgumentException.ThrowIfNullOrEmpty(text);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(
            text.GetUtf8Length(),
            Maximum.DraftTextLengthInBytes);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(
            text.GetGraphemeLength(),
            Maximum.DraftTextLengthInGraphemes);
    }

    /// <summary>
    /// Creates a new instance of <see cref="DraftPost"/> with the specified content and embedded video.
    /// </summary>
    /// <param name="text">The primary post content. It has a higher limit than post contents to allow storing a larger text that can later be refined into smaller posts.</param>
    /// <param name="embedVideo">The video to embed in the post.</param>
    /// <exception cref="ArgumentException">Thrown when the text is <see langword="null"/> or empty.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the text length is greater than <see cref="Maximum.DraftTextLengthInBytes"/> UTF-8 bytes or <see cref="Maximum.DraftTextLengthInGraphemes"/> graphemes.</exception>
    public DraftPost(string text, DraftEmbedVideo embedVideo) : this(
        text: text,
        labels: null,
        embedImages: null,
        embedGallery: null,
        embedVideos: [embedVideo],
        embedExternals: null,
        embedRecords: null)
    {
        ArgumentException.ThrowIfNullOrEmpty(text);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(
            text.GetUtf8Length(),
            Maximum.DraftTextLengthInBytes);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(
            text.GetGraphemeLength(),
            Maximum.DraftTextLengthInGraphemes);
    }

    /// <summary>
    /// Creates a new instance of <see cref="DraftPost"/> with the specified content and optional embedded media and labels.
    /// </summary>
    /// <param name="text">The primary post content. It has a higher limit than post contents to allow storing a larger text that can later be refined into smaller posts.</param>
    /// <param name="postSelfLabels">The labels to apply to the post.</param>
    /// <param name="embedImages">The images to embed in the post. (Maximum 4)</param>
    /// <param name="embedGallery">The gallery to embed in the post.</param>
    /// <param name="embedVideos">The videos to embed in the post. (Maximum 1)</param>
    /// <param name="embedExternals">The external content to embed in the post. (Maximum 1)</param>
    /// <param name="embedRecords">The records to embed in the post. (Maximum 1)</param>
    /// <exception cref="ArgumentException">Thrown when the text is <see langword="null"/> or empty.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the text length is greater than <see cref="Maximum.DraftTextLengthInBytes"/> UTF-8 bytes or <see cref="Maximum.DraftTextLengthInGraphemes"/> graphemes.</exception>.
    public DraftPost(
        string text,
        PostSelfLabels? postSelfLabels,
        IReadOnlyList<DraftEmbedImage>? embedImages,
        DraftEmbedGallery? embedGallery,
        IReadOnlyList<DraftEmbedVideo>? embedVideos,
        IReadOnlyList<DraftEmbedExternal>? embedExternals,
        IReadOnlyList<DraftEmbedRecord>? embedRecords) : this(
            text: text,
            labels: null,
            embedImages: embedImages,
            embedGallery: embedGallery,
            embedVideos: embedVideos,
            embedExternals: embedExternals,
            embedRecords: embedRecords)
    {
        ArgumentException.ThrowIfNullOrEmpty(text);

        ArgumentOutOfRangeException.ThrowIfGreaterThan(
            text.GetUtf8Length(),
            Maximum.DraftTextLengthInBytes);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(
            text.GetGraphemeLength(),
            Maximum.DraftTextLengthInGraphemes);

        if (postSelfLabels is not null)
        {
            Labels = (SelfLabels)postSelfLabels;
        }
    }

    /// <summary>
    /// Gets or sets the the primary post content.  It has a higher limit than post contents to allow storing a larger text that can later be refined into smaller posts.
    /// Cannot be <see langword="null"/> or empty, cannot be larger than <see cref="Maximum.DraftTextLengthInBytes"/> UTF-8 bytes or <see cref="Maximum.DraftTextLengthInGraphemes"/> graphemes.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when setting if the value is <see langword="null"/> or empty.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when setting if the value length is greater than <see cref="Maximum.DraftTextLengthInBytes"/> UTF-8 bytes or <see cref="Maximum.DraftTextLengthInGraphemes"/> graphemes.</exception>.
    [JsonRequired]
    public string Text
    {
        get;

        set
        {
            ArgumentException.ThrowIfNullOrEmpty(value);

            ArgumentOutOfRangeException.ThrowIfGreaterThan(
                value.GetUtf8Length(),
                Maximum.DraftTextLengthInBytes);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(
                value.GetGraphemeLength(),
                Maximum.DraftTextLengthInGraphemes);

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
    public IReadOnlyList<DraftEmbedImage>? EmbedImages { get; }

    /// <summary>
    /// Gets the embedded gallery for this draft.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DraftEmbedGallery? EmbedGallery { get; }

    /// <summary>
    /// Gets the embedded videos for this draft.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<DraftEmbedVideo>? EmbedVideos { get; }

    /// <summary>
    /// Gets the embedded external content for this draft.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<DraftEmbedExternal>? EmbedExternals { get; }

    /// <summary>
    /// Gets the embedded records for this draft.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<DraftEmbedRecord>? EmbedRecords { get; }

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
                return Labels.Contains(SelfLabelValues.Porn);
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
                Labels.AddLabel(SelfLabelValues.Porn);
            }
            else
            {
                Labels.RemoveLabel(SelfLabelValues.Porn);
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
                return Labels.Contains(SelfLabelValues.Sexual);
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
                Labels.AddLabel(SelfLabelValues.Sexual);
            }
            else
            {
                Labels.RemoveLabel(SelfLabelValues.Sexual);
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

            return Labels.Contains(SelfLabelValues.GraphicMedia);
        }

        set
        {
            Labels ??= new SelfLabels();
            if (value)
            {
                Labels.AddLabel(SelfLabelValues.GraphicMedia);
            }
            else
            {
                Labels.RemoveLabel(SelfLabelValues.GraphicMedia);
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

            return Labels.Contains(SelfLabelValues.Nudity);
        }

        set
        {
            Labels ??= new SelfLabels();

            if (value)
            {
                Labels.AddLabel(SelfLabelValues.Nudity);
            }
            else
            {
                Labels.RemoveLabel(SelfLabelValues.Nudity);
            }
        }
    }

    /// <summary>
    /// Sets the self labels for the post to the values specified in <paramref name="labels"/>.
    /// </summary>
    /// <param name="labels">The self label values to set.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="labels"/> is <see langword="null"/>.</exception>
    public void SetSelfLabels(PostSelfLabels labels)
    {
        ArgumentNullException.ThrowIfNull(labels);

        ContainsPorn = labels.Porn;
        ContainsSexualContent = labels.SexualContent;
        ContainsGraphicMedia = labels.GraphicMedia;
        ContainsNudity = labels.Nudity;
    }
}