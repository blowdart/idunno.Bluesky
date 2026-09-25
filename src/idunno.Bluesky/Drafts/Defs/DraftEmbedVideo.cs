// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

using idunno.AtProto;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace idunno.Bluesky.Drafts;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Encapsulates a local embedded video, and captions if any, in a draft post.
/// </summary>
[JsonPolymorphic(UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FallBackToNearestAncestor)]
[JsonDerivedType(typeof(DraftEmbedVideo), typeDiscriminator: "app.bsky.draft.defs#draftEmbedVideo")]
public record DraftEmbedVideo
{
    /// <summary>
    /// Constructs a new instance of <see cref="DraftEmbedVideo"/> with the specified local reference, optional alt text, and optional captions.
    /// </summary>
    /// <param name="localRef">The device local reference to an image.</param>
    /// <param name="altText">The alt text for the image, if any. Maximum <see cref="Maximum.DraftEmbedAltTextLengthInGraphemes"/> grapheme clusters.</param>
    /// <param name="captions">A collection of <see cref="DraftEmbedCaption"/> associated with the video embed. Maximum <see cref="Maximum.DraftEmbedVideoCaptions"/> captions.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="localRef"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     Thrown when <paramref name="altText"/> length is greater than <see cref="Maximum.DraftEmbedAltTextLengthInGraphemes"/> grapheme clusters,
    ///     or <paramref name="captions"/> has more than <see cref="Maximum.DraftEmbedVideoCaptions"/> entries.
    /// </exception>
    [JsonConstructor]
    public DraftEmbedVideo(DraftEmbedLocalRef localRef, string? altText = null, IReadOnlyList<DraftEmbedCaption>? captions = null)
    {
        ArgumentNullException.ThrowIfNull(localRef);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(
            altText?.GetGraphemeLength() ?? 0,
            Maximum.DraftEmbedAltTextLengthInGraphemes);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(
            captions?.Count ?? 0,
            Maximum.DraftEmbedVideoCaptions);
        LocalRef = localRef;
        AltText = altText;
        Captions = captions is null ? null : new List<DraftEmbedCaption>(captions).AsReadOnly();
    }

    /// <summary>
    /// Get the device local reference to an video.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown when setting to <see langword="null"/>.</exception>
    [JsonRequired]
    public DraftEmbedLocalRef LocalRef
    {
        get;

        init
        {
            ArgumentNullException.ThrowIfNull(value);

            field = value;
        }
    }

    /// <summary>
    /// Gets or sets the alt text for the image, if any. Maximum <see cref="Maximum.DraftEmbedAltTextLengthInGraphemes"/> grapheme clusters.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when setting if the value length is greater than <see cref="Maximum.DraftEmbedAltTextLengthInGraphemes"/> grapheme clusters.</exception>
    [JsonPropertyName("alt")]
    public string? AltText
    {
        get;

        set
        {
            ArgumentOutOfRangeException.ThrowIfGreaterThan(
                value?.GetGraphemeLength() ?? 0,
                Maximum.DraftEmbedAltTextLengthInGraphemes);

            field = value;
        }
    }

    /// <summary>
    /// A collection of <see cref="DraftEmbedCaption"/> associated with the video embed.
    /// </summary>
    public IReadOnlyList<DraftEmbedCaption>? Captions { get; }
}