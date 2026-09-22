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
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="id"/> or <paramref name="value"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="type"/> is <see cref="SavedFeedPreferenceType.Unknown"/>.</exception>
    public SavedFeed(string id, SavedFeedPreferenceType type, string value, bool pinned)
        : this(id, ActorWireValues.FromSavedFeedPreferenceType(type, nameof(type)), value, pinned)
    {
    }

    /// <summary>
    /// Creates a new instance of <see cref="SavedFeed"/> from the values the service sent.
    /// </summary>
    /// <param name="id">The identifier of the feed preference.</param>
    /// <param name="typeValue">The type of the feed preference, as the service expresses it.</param>
    /// <param name="value">The value of the feed preference.</param>
    /// <param name="pinned">A flag indicating whether the feed is pinned.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="id"/>, <paramref name="typeValue"/> or <paramref name="value"/> is <see langword="null"/>.</exception>
    [JsonConstructor]
    internal SavedFeed(string id, string typeValue, string value, bool pinned)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(typeValue);
        ArgumentNullException.ThrowIfNull(value);

        Id = id;
        TypeValue = typeValue;
        Value = value;
        Pinned = pinned;
    }

    /// <summary>
    /// Gets the identifier of the feed preference.
    /// </summary>
    [JsonRequired]
    public string Id { get; init; }

    /// <summary>
    /// Gets the type of the feed preference, as the service expresses it.
    /// </summary>
    /// <remarks>
    /// <para>The feed type is an open union, so the value the service sent is kept verbatim and <see cref="Type"/>
    /// is projected from it. A type this library does not recognize survives a read, modify and write cycle rather
    /// than being discarded.</para>
    /// </remarks>
    [JsonInclude]
    [JsonRequired]
    [JsonPropertyName("type")]
    internal string TypeValue { get; init; }

    /// <summary>
    /// Gets the type of the feed preference.
    /// </summary>
    /// <remarks>
    /// <para>A type this library does not recognize is reported as <see cref="SavedFeedPreferenceType.Unknown"/>.</para>
    /// </remarks>
    [JsonIgnore]
    public SavedFeedPreferenceType Type => ActorWireValues.ToSavedFeedPreferenceType(TypeValue);

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
    Timeline,

    /// <summary>
    /// The feed type is one this library does not recognize.
    /// </summary>
    /// <remarks>
    /// <para>This value only ever comes from the service. It cannot be used to build a <see cref="SavedFeed"/>,
    /// as it carries no type the service would understand.</para>
    /// </remarks>
    Unknown
}