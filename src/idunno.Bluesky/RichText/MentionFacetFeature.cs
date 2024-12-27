// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

using idunno.AtProto;

namespace idunno.Bluesky.RichText
{
    /// <summary>
    /// Facet feature for mention of another account. The text is usually a handle, including a '@' prefix, but the facet reference is a DID.
    /// </summary>
    public sealed record MentionFacetFeature :FacetFeature
    {
        /// <summary>
        /// Creates a new instance of <see cref="MentionFacetFeature"/>.
        /// </summary>
        /// <param name="did">The <see cref="Did"/> of the account being mentioned.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="did"/> is null.</exception>
        [JsonConstructor]
        public MentionFacetFeature(Did did)
        {
            ArgumentNullException.ThrowIfNull(did);
            Did = did;
        }

        /// <summary>
        /// The <see cref="Did"/> of the account being mentioned.
        /// </summary>
        [JsonInclude]
        [JsonRequired]
        public Did Did { get; init; }
    }
}
