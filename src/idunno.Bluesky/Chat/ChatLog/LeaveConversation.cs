// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

#pragma warning disable IDE0130
namespace idunno.Bluesky.Chat;
#pragma warning restore IDE0130

/// <summary>
/// A log entry indicating the start of a conversation.
/// </summary>
public sealed record LeaveConversation : LogBase
{
    /// <summary>
    /// Constructs a new instance of <see cref="LeaveConversation"/>.
    /// </summary>
    /// <param name="conversationId">The conversation identifier.</param>
    /// <param name="revision">The conversation revision.</param>
    [JsonConstructor]
    internal LeaveConversation(string conversationId, string revision) : base(conversationId, revision)
    {
    }
}