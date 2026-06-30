// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace idunno.Bluesky.Notifications.PreferenceTypes;

/// <summary>
/// Encapsulates notification preferences for chats
/// </summary>
[Obsolete("Deprecated, use ChatNotificationPreferences instead.")]
[SuppressMessage("Info Code Smell", "S1133:Deprecated code should be removed", Justification = "Kept for compatibility")]
public sealed record ChatPreference
{
    /// <summary>
    /// Creates a new instance of <see cref="ChatPreference"/>.
    /// </summary>
    public ChatPreference()
    {
    }

    /// <summary>
    /// Creates a new instance of <see cref="ChatPreference"/>.
    /// </summary>
    /// <param name="include">The type of chats to be notified for.</param>
    /// <param name="push">A flag indicating whether chat notifications should be pushed.</param>
    [JsonConstructor]
    public ChatPreference(ChatNotificationsFrom include, bool push)
    {
        Include = include;
        Push = push;
    }

    /// <summary>
    /// Gets or sets the type of chats to be notified for.
    /// </summary>
    public ChatNotificationsFrom Include { get; set; }

    /// <summary>
    /// Gets or sets a flag indicating whether chat notifications should be pushed.
    /// </summary>
    public bool Push { get; set; }

}

/// <summary>
/// The type of chats that should be included in notifications
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<ChatNotificationsFrom>))]
public enum ChatNotificationsFrom
{
    /// <summary>
    /// All chats
    /// </summary>
    All,

    /// <summary>
    /// Only accepted chats
    /// </summary>
    Accepted
}