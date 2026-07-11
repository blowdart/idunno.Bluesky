// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.Bluesky.Chat.Notifications;

/// <summary>
/// Encapsulates notification preferences for chats
/// </summary>
public sealed record Preferences
{
    /// <summary>
    /// Creates a new instance of <see cref="Preferences"/>.
    /// </summary>
    /// <param name="chat">The chat notification preference.</param>
    /// <param name="chatRequest">The chat request notification preference.</param>
    [JsonConstructor]
    public Preferences(Preference chat, Preference chatRequest)
    {
        Chat = chat;
        ChatRequest = chatRequest;
    }

    /// <summary>
    /// Gets or sets the chat notification preference.
    /// </summary>
    [JsonRequired]
    public Preference Chat { get; set; }

    /// <summary>
    /// Gets or sets the chat request notification preference.
    /// </summary>
    [JsonRequired]
    public Preference ChatRequest { get; set; }
}