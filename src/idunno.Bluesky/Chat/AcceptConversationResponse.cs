// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.Bluesky.Chat
{
    /// <summary>
    /// Encapsulates the response from an accept conversation request.
    /// </summary>
    public record AcceptConversationResponse
    {
        /// <summary>
        /// Creates a new instance of <see cref="AcceptConversationResponse"/>.
        /// </summary>
        /// <param name="revision">Revision of when the conversation was accepted. If <see langword="null"/>, the conversation was already accepted.</param>
        public AcceptConversationResponse(string? revision)
        {
            Revision = revision;
        }

        /// <summary>
        /// Revision of when the conversation was accepted. If <see langword="null"/>, the conversation was already accepted.
        /// </summary>
        [JsonPropertyName("rev")]
        public string? Revision { get; init; }
    }
}
