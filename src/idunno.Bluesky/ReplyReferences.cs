// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto.Repo;

namespace idunno.Bluesky
{
    /// <summary>
    /// Provides an object representation of the information needed to create a reply post.
    /// </summary>
    public sealed record ReplyReferences
    {
        /// <summary>
        /// Creates a new instance of <see cref="ReplyReferences"/>.
        /// </summary>
        /// <param name="parent">The parent <see cref="StrongReference"/></param>
        /// <param name="root">The root <see cref="StrongReference"/></param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="parent"/> or <paramref name="root"/> are <see langword="null"/>.</exception>
        public ReplyReferences(StrongReference parent, StrongReference root)
        {
            ArgumentNullException.ThrowIfNull(parent);
            ArgumentNullException.ThrowIfNull(root);

            Parent = parent;
            Root = root;
        }

        /// <summary>
        /// Gets the <see cref="StrongReference"/> to the immediate parent of the post being created.
        /// </summary>
        public StrongReference Parent { get; init; }

        /// <summary>
        /// Gets the <see cref="StrongReference"/> to the root of the create the current post will be created under.
        /// </summary>
        public StrongReference Root { get; init; }
    }
}
