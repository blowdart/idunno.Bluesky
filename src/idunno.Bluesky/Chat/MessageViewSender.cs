// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

using idunno.AtProto;

namespace idunno.Bluesky.Chat
{
    /// <summary>
    /// Encapsulates a view over the sender of a message.
    /// </summary>
    public sealed record MessageViewSender
    {
        /// <summary>
        /// Creates a new instance of <see cref="MessageViewSender"/>.
        /// </summary>
        /// <param name="did">The <see cref="AtProto.Did"/> of the message author.</param>
        [JsonConstructor]
        public MessageViewSender(Did did)
        {
            ArgumentNullException.ThrowIfNull(did);
            Did = did;
        }

        /// <summary>
        /// Gets the <see cref="AtProto.Did"/> of the message author.
        /// </summary>
        [JsonInclude]
        [JsonRequired]
        public Did Did { get; init; }
    }
}
