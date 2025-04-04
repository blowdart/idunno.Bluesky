// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.Bluesky.Chat
{
    /// <summary>
    /// Gets a view of the message and a reaction to it.
    /// </summary>
    public record MessageAndReactionView
    {
        /// <summary>
        /// Creates a new instance of <see cref="MessageAndReactionView"/>
        /// </summary>
        /// <param name="message">The <see cref="MessageView"/> of the message.</param>
        /// <param name="reaction">The <see cref="Reaction"/> of the reaction.</param>
        public MessageAndReactionView(MessageView message, ReactionView reaction)
        {
            ArgumentNullException.ThrowIfNull(message);
            ArgumentNullException.ThrowIfNull(reaction);

            Message = message;
            Reaction = reaction;
        }

        /// <summary>
        /// Gets the <see cref="MessageView"/> of the message.
        /// </summary>
        [JsonInclude]
        [JsonRequired]
        public MessageView Message { get; init; }

        /// <summary>
        /// Gets the <see cref="Reaction"/> of the reaction.
        /// </summary>
        [JsonInclude]
        [JsonRequired]
        public ReactionView Reaction { get; init; }
    }
}
