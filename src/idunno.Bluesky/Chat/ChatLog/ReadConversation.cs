// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

#pragma warning disable IDE0130
namespace idunno.Bluesky.Chat;
#pragma warning restore IDE0130

/// <summary>
/// A log entry indicating a message was read in a chat.
/// </summary>
public sealed record ReadConversation : MessageLogBase
{
    /// <summary>
    /// Constructs a new instance of <see cref="ReadConversation"/>.
    /// </summary>
    /// <param name="conversationId">The conversation identifier.</param>
    /// <param name="revision">The conversation revision.</param>
    /// <param name="message">A <see cref="MessageViewBase">view</see> over the message the log entry refers to.</param>
    [JsonConstructor]
    internal ReadConversation(string conversationId, string revision, MessageViewBase message) : base(conversationId, revision, message)
    {
    }
}