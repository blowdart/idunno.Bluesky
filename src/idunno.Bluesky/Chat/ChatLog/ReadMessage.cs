// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

#pragma warning disable IDE0130
namespace idunno.Bluesky.Chat;
#pragma warning restore IDE0130

/// <summary>
/// A log entry indicating a message was read in a chat.
/// </summary>
#pragma warning disable S1133 // Deprecated code should be removed - kept for compatibility with older log entries.
[Obsolete("This class is obsolete and will be removed in a future version. Use ReadConversation instead.")]
#pragma warning restore S1133 // Deprecated code should be removed
public sealed record ReadMessage : MessageLogBase
{
    /// <summary>
    /// Constructs a new instance of <see cref="ReadMessage"/>.
    /// </summary>
    /// <param name="conversationId">The conversation identifier.</param>
    /// <param name="revision">The conversation revision.</param>
    /// <param name="message">A <see cref="MessageViewBase">view</see> over the message the log entry refers to.</param>
    [JsonConstructor]
    internal ReadMessage(string conversationId, string revision, MessageViewBase message) : base(conversationId, revision, message)
    {
    }
}