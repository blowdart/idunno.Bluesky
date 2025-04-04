// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.Bluesky.Chat
{
    /// <summary>
    /// Indicates the status of a conversation.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter<ConversationStatus>))]
    public enum ConversationStatus
    {
        /// <summary>
        /// The conversation has been requested.
        /// </summary>
        [JsonStringEnumMemberName("request")]
        Requested,

        /// <summary>
        /// The conversation is accepted.
        /// </summary>
        Accepted
    }
}
