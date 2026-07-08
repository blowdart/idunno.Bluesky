// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace idunno.Bluesky.Chat;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// A reference to another message within the same convo, used to indicate that a message is a reply to it.
/// </summary>
/// <param name="MessageId">The message ID of the message being replied to.</param>
public sealed record ReplyReference([field: JsonRequired] string MessageId)
{
}