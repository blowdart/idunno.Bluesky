// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;

namespace idunno.Bluesky.RichText
{
    /// <summary>
    /// Provides an base object representation of a facet feature in a Bluesky post usable by a <see cref="PostBuilder"/>.
    /// </summary>
    public abstract record PostBuilderFacetFeature
    {
        /// <summary>
        /// Constructs a new instance of the <see cref="PostBuilderFacetFeature"/> class.
        /// </summary>
        protected PostBuilderFacetFeature()
        {
        }

        /// <summary>
        /// Constructs a new instance of the <see cref="PostBuilderFacetFeature"/> class with the specified text.
        /// </summary>
        /// <param name="text">The text to wrap the facet feature around.</param>
        protected PostBuilderFacetFeature(string text)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(text);
            Text = text;
        }

        /// <summary>
        /// Gets the text to wrap the facet feature around.
        /// </summary>
        [NotNull]
        public string? Text { get; protected set; }

        /// <summary>
        /// Returns a string that represents the current object.
        /// </summary>
        /// <returns>The text of the facet.</returns>
        public override string ToString() => Text;
    }
}
