// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.Bluesky.Notifications.PreferenceTypes;

namespace idunno.Bluesky.Notifications;

internal static class NotificationWireValues
{
    internal static NotificationAllowedFrom ToNotificationAllowedFrom(string value) => value switch
    {
        "none" => NotificationAllowedFrom.None,
        "followers" => NotificationAllowedFrom.Followers,
        "mutuals" => NotificationAllowedFrom.Mutuals,
        _ => NotificationAllowedFrom.Unknown
    };

    internal static string FromNotificationAllowedFrom(NotificationAllowedFrom value, string paramName) => value switch
    {
        NotificationAllowedFrom.None => "none",
        NotificationAllowedFrom.Followers => "followers",
        NotificationAllowedFrom.Mutuals => "mutuals",
        _ => throw UnknownValue(nameof(NotificationAllowedFrom), value, paramName)
    };

    internal static LimitTo ToLimitTo(string value) => value switch
    {
        "all" => LimitTo.All,
        "follows" => LimitTo.Follows,
        _ => LimitTo.Unknown
    };

    internal static string FromLimitTo(LimitTo value, string paramName) => value switch
    {
        LimitTo.All => "all",
        LimitTo.Follows => "follows",
        _ => throw UnknownValue(nameof(LimitTo), value, paramName)
    };

    internal static ChatNotificationsFrom ToChatNotificationsFrom(string value) => value switch
    {
        "all" => ChatNotificationsFrom.All,
        "accepted" => ChatNotificationsFrom.Accepted,
        _ => ChatNotificationsFrom.Unknown
    };

    internal static string FromChatNotificationsFrom(ChatNotificationsFrom value, string paramName) => value switch
    {
        ChatNotificationsFrom.All => "all",
        ChatNotificationsFrom.Accepted => "accepted",
        _ => throw UnknownValue(nameof(ChatNotificationsFrom), value, paramName)
    };

    private static ArgumentOutOfRangeException UnknownValue<TValue>(string typeName, TValue value, string paramName) =>
        new(paramName, value, $"{typeName}.{value} cannot be sent to the service. It only exists to carry an unrecognized value.");
}
