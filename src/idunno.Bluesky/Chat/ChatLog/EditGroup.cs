// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

#pragma warning disable IDE0130
namespace idunno.Bluesky.Chat;
#pragma warning restore IDE0130

/// <summary>
/// A log entry indicating information about a group conversation was edited.
/// </summary>
public sealed record EditGroup : MessageLogBase
{
    /// <summary>
    /// Constructs a new instance of <see cref="EditGroup"/>.
    /// </summary>
    /// <param name="conversationId">The conversation identifier.</param>
    /// <param name="revision">The conversation revision.</param>
    /// <param name="message">A <see cref="MessageViewBase">view</see> over the message the log entry refers to.</param>
    [JsonConstructor]
    internal EditGroup(string conversationId, string revision, MessageViewBase message) : base(conversationId, revision, message)
    {
    }
}