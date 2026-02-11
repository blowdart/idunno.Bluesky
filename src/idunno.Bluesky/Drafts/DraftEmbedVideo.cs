// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

using idunno.AtProto;

namespace idunno.Bluesky.Drafts
{
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
        /// <param name="altText">The alt text for the image, if any. Maximum 2000 grapheme clusters.</param>
        /// <param name="captions">A collection of <see cref="DraftEmbedCaption"/> associated with the video embed. Maximum 20 captions.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="localRef"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">
        ///     Thrown when <paramref name="altText"/> length is greater than 2000 grapheme clusters or <paramref name="captions"/> has more than 20 entries.
        /// </exception>
        [JsonConstructor]
        public DraftEmbedVideo(DraftEmbedLocalRef localRef, string? altText = null, IList<DraftEmbedCaption>? captions = null)
        {
            ArgumentNullException.ThrowIfNull(localRef);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(
                altText?.GetGraphemeLength() ?? 0,
                2000);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(
                captions?.Count ?? 0,
                20);
            LocalRef = localRef;
            AltText = altText;
            Captions = captions;
        }

        /// <summary>
        /// Get the device local reference to an video.
        /// </summary>
        [JsonRequired]
        public DraftEmbedLocalRef LocalRef { get; init; }

        /// <summary>
        /// Gets or sets the alt text for the image, if any. Maximum 2000 grapheme clusters.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when setting if <paramref name="value"/> length is greater than 2000 grapheme clusters.</exception>
        [JsonPropertyName("alt")]
        public string? AltText
        {
            get;

            set
            {
                ArgumentOutOfRangeException.ThrowIfGreaterThan(
                    value?.GetGraphemeLength() ?? 0,
                    2000);

                field = value;
            }
        }

        /// <summary>
        /// A collection of <see cref="DraftEmbedCaption"/> associated with the video embed.
        /// </summary>
        public IList<DraftEmbedCaption>? Captions { get; init; }
    }
}
