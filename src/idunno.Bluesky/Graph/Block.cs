// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.


using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using idunno.AtProto;

namespace idunno.Bluesky.Graph
{
    /// <summary>
    /// Record declaring a 'block' relationship against another account.
    /// </summary>
    /// <remarks>
    ///<para>NOTE: blocks are public in Bluesky.</para>
    /// </remarks>
    [SuppressMessage("Performance", "CA1812", Justification = "Used in creating a block or reading the blocks")]
    internal sealed record Block : BlueskyRecord
    {
        /// <summary>
        /// Creates a new instance of <see cref="Block"/>.
        /// </summary>
        /// <param name="subject"><see cref="Did"/> of the account to be blocked.</param>
        /// <param name="createdAt">The date and time the record was created at.</param>
        [JsonConstructor]
        public Block(Did subject, DateTimeOffset createdAt) : base(createdAt)
        {
            ArgumentNullException.ThrowIfNull(subject);
            Subject = subject;
            CreatedAt = createdAt;
        }

        /// <summary>
        /// <see cref="Did"/> of the account to be blocked.
        /// </summary>
        [JsonInclude]
        [JsonRequired]
        public Did Subject { get; init; }
    }
}
