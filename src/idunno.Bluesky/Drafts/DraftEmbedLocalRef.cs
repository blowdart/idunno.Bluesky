// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.Bluesky.Drafts
{
    /// <summary>
    /// Encapsulates a local reference to a file to be embedded in a draft post.
    /// </summary>
    public sealed record DraftEmbedLocalRef
    {
        /// <summary>
        /// Creates a new instance of <see cref="DraftEmbedLocalRef"/> with the specified local path.
        /// </summary>
        /// <param name="path">The local path to the file to be embedded.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="path"/> is null/</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="path"/>is empty or whitespace.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when path length &gt; 1024 or &lt; 1.</exception>
        public DraftEmbedLocalRef(string path)
        {
            ArgumentNullException.ThrowIfNull(path);
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("Value cannot be empty or whitespace.", nameof(path));
            }

            ArgumentOutOfRangeException.ThrowIfGreaterThan(
                path.Length,
                1024);

            ArgumentOutOfRangeException.ThrowIfLessThan(
                path.Length,
                1);

            Path = path;
        }

        /// <summary>
        /// Gets the local, on-device ref to file to be embedded. Embeds are currently device-bound for drafts.
        /// </summary>
        [JsonRequired]
        public string Path { get; init; }
    }
}
