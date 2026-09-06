// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace idunno.Bluesky.Actor;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Encapsulates feed preferences for an actor
/// </summary>
public sealed record SavedFeed
{
    /// <summary>
    /// Creates a new instance of <see cref="SavedFeed"/>
    /// </summary>
    /// <param name="id">The identifier of the feed preference.</param>
    /// <param name="type">The type of the feed preference.</param>
    /// <param name="value">The value of the feed preference.</param>
    /// <param name="pinned">A flag indicating whether the feed is pinned.</param>
    [JsonConstructor]
    public SavedFeed(string id, SavedFeedPreferenceType type, string value, bool pinned)
    {
        Id = id;
        Type = type;
        Value = value;
        Pinned = pinned;
    }

    /// <summary>
    /// Gets the identifier of the feed preference.
    /// </summary>
    [JsonRequired]
    public string Id { get; init; }

    /// <summary>
    /// Gets the type of the feed preference.
    /// </summary>
    [JsonRequired]
    public SavedFeedPreferenceType Type { get; init; }

    /// <summary>
    /// Gets the value of the feed preference.
    /// </summary>
    [JsonRequired]
    public string Value { get; init; }

    /// <summary>
    /// Gets a flag indicating whether the feed is pinned.
    /// </summary>
    [JsonRequired]
    public bool Pinned { get; init; }
}

/// <summary>
/// Values indicating what type of feed the preference applies to.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<SavedFeedPreferenceType>))]
public enum SavedFeedPreferenceType
{
    /// <summary>
    /// The preference applies to a feed.
    /// </summary>
    Feed,

    /// <summary>
    /// The preference applies to a list.
    /// </summary>
    List,

    /// <summary>
    /// The preference applies to the user's timeline.
    /// </summary>
    Timeline
}