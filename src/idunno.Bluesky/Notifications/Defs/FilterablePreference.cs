// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.Bluesky.Notifications.PreferenceTypes;

/// <summary>
/// Encapsulates a filterable notification preference
/// </summary>
public sealed record FilterablePreference
{
    /// <summary>
    /// Creates a new instance of <see cref="FilterablePreference"/>.
    /// </summary>
    public FilterablePreference()
    {
    }

    /// <summary>
    /// Creates a new instance of <see cref="FilterablePreference"/>.
    /// </summary>
    /// <param name="include">The type of users that notifications should be sent for.</param>
    /// <param name="list">Flag indicating whether the notification type should be shown in the user's notifications list.</param>
    /// <param name="push">Flag indicating whether the notification type should be pushed.</param>
    [JsonConstructor]
    public FilterablePreference(LimitTo include, bool list, bool push)
    {
        Include = include;
        List = list;
        Push = push;
    }

    /// <summary>
    /// Gets or sets the type of users that notifications should be sent for.
    /// </summary>
    public LimitTo Include { get; set; }

    /// <summary>
    /// Gets or sets a flag indicating whether the notification type should be shown in the user's notifications list.
    /// </summary>
    public bool List { get; set; }

    /// <summary>
    /// Gets or sets a flag indicating whether the notification type should be pushed.
    /// </summary>
    public bool Push { get; set; }
}

/// <summary>
/// The type of users that should be included in a filterable notifications
/// </summary>
[JsonConverter(typeof(LimitToConverter))]
public enum LimitTo
{
    /// <summary>
    /// All accounts
    /// </summary>
    [JsonStringEnumMemberName("all")]
    All,

    /// <summary>
    /// Only accounts you follow
    /// </summary>
    [JsonStringEnumMemberName("follows")]
    Follows,

    /// <summary>
    /// The value is not one this library recognizes.
    /// </summary>
    /// <remarks>
    /// <para>The set of values is decided by the service, not by this library, so a value added upstream is
    /// surfaced as <see cref="Unknown"/> rather than causing the preferences to fail to deserialize. It cannot
    /// be sent to the service.</para>
    /// </remarks>
    Unknown
}