// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;

namespace idunno.Bluesky.RichText
{
    /// <summary>
    /// Provides an object representation of a mention in a Bluesky post.
    /// </summary>
    public sealed record Mention : PostBuilderFacetFeature
    {
        /// <summary>
        /// Creates a new instance of <see cref="Mention"/>.
        /// </summary>
        /// <param name="did">The <see cref="Did"/> of the actor being mentioned.</param>
        /// <param name="text">The text to "wrap" the mention around.</param>
        /// <exception cref="ArgumentNullException">when <paramref name="did"/> is null.</exception>
        public Mention(Did did, string text) : base(text)
        {
            ArgumentNullException.ThrowIfNull(did);
            Did = did;
            Text = text;
        }

        /// <summary>
        /// Creates a new instance of <see cref="Mention"/>.
        /// </summary>
        /// <param name="did">The <see cref="Did"/> of the actor being mentioned.</param>
        /// <param name="handle">The <see cref="Handle"/> of the actor being mentioned, which will generate the text for the facet.</param>
        public Mention(Did did, Handle handle) : base()

        {
            ArgumentNullException.ThrowIfNull(did);
            ArgumentNullException.ThrowIfNull(handle);

            Did = did;
            Text = handle.ToString();
        }

        /// <summary>
        /// Gets the <see cref="Did"/> of the actor being mentioned in the facet feature..
        /// </summary>
        public Did Did { get; init; }
    }
}
