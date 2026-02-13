// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.Bluesky.Drafts
{
    /// <summary>
    /// Encapsulates a embedded URI in a draft post.
    /// </summary>
    [JsonPolymorphic(UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FallBackToNearestAncestor)]
    [JsonDerivedType(typeof(DraftEmbedExternal), typeDiscriminator: "app.bsky.draft.defs#draftEmbedExternal")]
    public record DraftEmbedExternal
    {
        /// <summary>
        /// Creates a new instance of <see cref="DraftEmbedExternal"/> with the specified external <see cref="Uri"/>.
        /// </summary>
        /// <param name="uri">The <see cref="Uri"/> to embed.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="uri"/> is <see langword="null"/>.</exception>
        [JsonConstructor]
        public DraftEmbedExternal(Uri uri)
        {
            ArgumentNullException.ThrowIfNull(uri);
            Uri = uri;
        }

        /// <summary>
        /// Gets the embedded external <see cref="Uri"/>.
        /// </summary>
        [JsonRequired]
        public Uri Uri { get; init; }
    }
}
