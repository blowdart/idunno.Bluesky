// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

using idunno.Bluesky.Record;

namespace idunno.Bluesky.Notifications;

/// <summary>
/// A declaration of the user's choices for notification subscriptions from other actors.
/// </summary>
[JsonPolymorphic(IgnoreUnrecognizedTypeDiscriminators = true,
                 UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FallBackToBaseType)]
[JsonDerivedType(typeof(Declaration), typeDiscriminator: RecordType.NotificationDeclaration)]
public record Declaration : BlueskyRecord
{
    /// <summary>
    /// Creates a new instance of <see cref="Declaration"/> with <see cref="AllowSubscriptions"/> set to <see cref="NotificationAllowedFrom.Followers"/>.
    /// </summary>
    public Declaration() : this(declaration: NotificationAllowedFrom.Followers)
    {
    }

    /// <summary>
    /// Creates a new instance of <see cref="Declaration"/> with the specified <paramref name="declaration"/>.
    /// </summary>
    /// <param name="declaration">The declaration of the user's choices for notification subscriptions from other actors.</param>
    /// <remarks>
    /// <para>Specifying a <see langword="null"/> value for <paramref name="declaration"/> will result in the default value of <see cref="NotificationAllowedFrom.Followers"/> being used.</para>
    /// </remarks>
    public Declaration(NotificationAllowedFrom? declaration)
    {
        AllowSubscriptions = declaration;
    }

    /// <summary>
    /// Gets or sets the declaration of the user's choices for notification subscriptions from other actors.
    /// </summary>
    /// <remarks>
    /// <para>Specifying a <see langword="null"/> value will result in the default value of <see cref="NotificationAllowedFrom.Followers"/> being used.</para>
    /// </remarks>
    public NotificationAllowedFrom? AllowSubscriptions { get; set; }

}

/// <summary>
/// Who is allowed to subscribe to notifications about account posts and reposts.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<NotificationAllowedFrom>))]
public enum NotificationAllowedFrom
{
    /// <summary>
    /// No-one.
    /// </summary>
    [JsonStringEnumMemberName("none")]
    None,

    /// <summary>
    /// Users who follow the account.
    /// </summary>
    [JsonStringEnumMemberName("followers")]
    Followers,

    /// <summary>
    /// Users who follow the account and the account follows.
    /// </summary>
    [JsonStringEnumMemberName("mutuals")]
    Mutuals
}
