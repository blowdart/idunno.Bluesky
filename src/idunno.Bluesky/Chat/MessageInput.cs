// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

using idunno.Bluesky.Embed;
using idunno.Bluesky.RichText;

namespace idunno.Bluesky.Chat
{
    /// <summary>
    /// Represents a message in a chat conversation.
    /// </summary>
    public sealed record MessageInput
    {
        /// <summary>
        /// Creates a new instance of <see cref="MessageInput"/>.
        /// </summary>
        /// <param name="text">The text for the message.</param>
        /// <param name="facets">The rich text <see cref="Facet"/>s for the message, if any.</param>
        /// <param name="embed">The <see cref="EmbeddedRecord"/> for the message, if any.</param>
        public MessageInput(string text, ICollection<Facet>? facets = null, EmbeddedRecord? embed = null)
        {
            ArgumentNullException.ThrowIfNull(text);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(text.Length, Maximum.MessageLengthInCharacters);

            Text = text;

            Facets = facets;
            Embed = embed;
        }

        /// <summary>
        /// Gets the text of the message
        /// </summary>
        [JsonInclude]
        [JsonRequired]
        public string Text { get; init; }

        /// <summary>
        /// Gets the rich text <see cref="Facet"/>s of the message, if any.
        /// </summary>
        [JsonInclude]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public ICollection<Facet>? Facets { get; init; }

        /// <summary>
        /// Gets the embedded record of the message, if any.
        /// </summary>
        [JsonInclude]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public EmbeddedRecord? Embed { get; init; }
    }
}
