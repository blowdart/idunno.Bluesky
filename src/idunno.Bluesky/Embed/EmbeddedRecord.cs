// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto.Repo;

namespace idunno.Bluesky.Embed
{
    /// <summary>
    /// Represents an embedded record pointing to a strongly referenced record, such as another post.
    /// </summary>
    public record EmbeddedRecord : EmbeddedBase
    {
        /// <summary>
        /// Creates a new instance of <see cref="EmbeddedRecord"/>.
        /// </summary>
        /// <param name="record">A <see cref="StrongReference"/> to the record to embed.</param>
        public EmbeddedRecord(StrongReference record)
        {
            Record = record;
        }

        /// <summary>
        /// Gets a <see cref="StrongReference"/> to the record to embed.
        /// </summary>
        public StrongReference Record { get; init; }
    }
}
