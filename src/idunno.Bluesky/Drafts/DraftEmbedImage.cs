// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

using idunno.AtProto;

namespace idunno.Bluesky.Drafts
{
    /// <summary>
    /// Encapsulates a local embedded image in a draft post.
    /// </summary>
    [JsonPolymorphic(UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FallBackToNearestAncestor)]
    [JsonDerivedType(typeof(DraftEmbedImage), typeDiscriminator: "app.bsky.draft.defs#draftEmbedImage")]
    public record DraftEmbedImage
    {
        /// <summary>
        /// Creates a new instance of <see cref="DraftEmbedImage"/> with the specified local reference and optional alt text.
        /// </summary>
        /// <param name="localRef">The device local reference to an image.</param>
        /// <param name="altText">The alt text for the image, if any. Maximum 2000 grapheme clusters.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="localRef"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="altText"/> length is greater than 2000 grapheme clusters.</exception>
        [JsonConstructor]
        public DraftEmbedImage(DraftEmbedLocalRef localRef, string? altText = null)
        {
            ArgumentNullException.ThrowIfNull(localRef);

            ArgumentOutOfRangeException.ThrowIfGreaterThan(
                altText?.GetGraphemeLength() ?? 0,
                2000);

            LocalRef = localRef;
            AltText = altText;
        }

        /// <summary>
        /// Get the device local reference to an image.
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
    }
}
