// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.Bluesky.Drafts
{
    /// <summary>
    /// Represents an embedded caption associated with a draft.
    /// </summary>
    [JsonPolymorphic(UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FallBackToNearestAncestor)]
    [JsonDerivedType(typeof(DraftEmbedCaption), typeDiscriminator: "app.bsky.draft.defs#draftEmbedCaption")]
    public record DraftEmbedCaption
    {
        /// <summary>
        /// Creates a new instance of <see cref="DraftEmbedCaption"/> with the specified content and language.
        /// </summary>
        /// <param name="content">The caption content.</param>
        /// <param name="lang">The caption language.</param>
        /// <exception cref="ArgumentException">Thrown when <paramref name="content"/> or <paramref name="lang"/> is null or whitespace.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="content"/> length is greater than 10000.</exception>
        [JsonConstructor]
        public DraftEmbedCaption(string content, string lang)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(content);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(content.Length, 10000);
            ArgumentException.ThrowIfNullOrWhiteSpace(lang);

            Content = content;
            Lang = lang;
        }

        /// <summary>
        /// Gets or sets the language of the caption in RFC5646 format.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown when setting if the value is null or whitespace.</exception>
        [JsonRequired]
        public string Lang
        {
            get;

            set
            {
                ArgumentException.ThrowIfNullOrWhiteSpace(value);

                field = value;
            }
        }

        /// <summary>
        /// Gets or sets the caption content.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown when setting if the value is null or whitespace.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when setting if the value length is greater than 10000.</exception>
        [JsonRequired]
        public string Content
        {
            get;

            set
            {
                ArgumentException.ThrowIfNullOrWhiteSpace(value);
                ArgumentOutOfRangeException.ThrowIfGreaterThan(value.Length, 10000);

                field = value;
            }
        }
    }
}
