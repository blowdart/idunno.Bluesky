// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.Bluesky.Chat
{
    /// <summary>
    /// A view over a reaction to a message.
    /// </summary>
    public record ReactionView : View
    {
        /// <summary>
        /// Creates a new instance of <see cref="ReactionView"/>
        /// </summary>
        /// <param name="value">The value of the reaction.</param>
        /// <param name="sender">The <see cref="ReactionViewSender"/> over the sender.</param>
        /// <param name="createdAt">The <see cref="DateTimeOffset"/> the reaction was created at.</param>
        public ReactionView(string value, ReactionViewSender sender, DateTimeOffset createdAt)
        {
            Value = value;
            Sender = sender;
            CreatedAt = createdAt;
        }

        /// <summary>
        /// Gets the value of the reaction as a string.
        /// </summary>
        [JsonInclude]
        [JsonRequired]
        public string Value { get; init; }

        /// <summary>
        /// Gets the <see cref="ReactionViewSender"/> of the sender of the reaction.
        /// </summary>
        [JsonInclude]
        [JsonRequired]
        public ReactionViewSender Sender { get; init; }

        /// <summary>
        /// Gets the <see cref="DateTimeOffset"/> the reaction was created at.
        /// </summary>
        [JsonInclude]
        [JsonRequired]
        public DateTimeOffset CreatedAt { get; init; }
    }
}
