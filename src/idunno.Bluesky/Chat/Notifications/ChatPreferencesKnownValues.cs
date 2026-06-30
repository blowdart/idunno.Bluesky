// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.Bluesky.Chat.Notifications;

/// <summary>
/// Known values for chat notification preferences.
/// </summary>
public static class ChatPreferencesKnownValues
{
    /// <summary>
    /// Allow notifications from all actors.
    /// </summary>
    public const string All = "all";

    /// <summary>
    /// Allow notifications only from actors the user follows.
    /// </summary>
    public const string Follows = "follows";
}
