// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.Bluesky.Chat.Notifications;

/// <summary>
/// Encapsulates notification preferences for chats and chat requests.
/// </summary>
public sealed record Preference
{
    /// <summary>
    /// Creates a new instance of <see cref="Preference"/>.
    /// </summary>
    /// <param name="include">A string indicating what type of chat notifications should be sent. Known values are held in <see cref="ChatPreferencesKnownValues"/></param>
    /// <param name="push">A flag indicating whether chat notifications should be pushed.</param>
    [JsonConstructor]
    public Preference(string include, bool push)
    {
        Include = include;
        Push = push;
    }

    /// <summary>
    /// Gets or sets a string indicating what type of chat notifications should be sent.
    /// Known values are held in <see cref="ChatPreferencesKnownValues"/>. Unknown values will be returned as-is.
    /// </summary>
    [JsonRequired]
    public string Include { get; set; }

    /// <summary>
    /// Gets or sets a flag indicating whether chat notifications should be pushed.
    /// </summary>
    [JsonRequired]
    public bool Push { get; set; }
}