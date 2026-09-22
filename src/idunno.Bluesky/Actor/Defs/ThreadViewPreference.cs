// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace idunno.Bluesky.Actor;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Preferences for displaying how threads are viewed.
/// </summary>
public record ThreadViewPreference : Preference
{
    /// <summary>
    /// Creates a new instance of <see cref="ThreadViewPreference"/>.
    /// </summary>
    /// <param name="sortingMode">The user's preferred sorting mode for threads.</param>
    /// <param name="prioritizeFollowedUsers">Flag indicating whether to show followed users at the top of all replies.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="sortingMode"/> is <see cref="ThreadSortingMode.Unknown"/>.</exception>
    public ThreadViewPreference(ThreadSortingMode? sortingMode = null, bool? prioritizeFollowedUsers = null)
        : this(
            sortingMode is null ? null : ActorWireValues.FromThreadSortingMode(sortingMode.Value, nameof(sortingMode)),
            prioritizeFollowedUsers)
    {
    }

    /// <summary>
    /// Creates a new instance of <see cref="ThreadViewPreference"/> from the values the service sent.
    /// </summary>
    /// <param name="sort">The user's preferred sorting mode for threads, as the service expresses it.</param>
    /// <param name="prioritizeFollowedUsers">Flag indicating whether to show followed users at the top of all replies.</param>
    [JsonConstructor]
    internal ThreadViewPreference(string? sort, bool? prioritizeFollowedUsers)
    {
        Sort = sort;
        PrioritizeFollowedUsers = prioritizeFollowedUsers;
    }

    /// <summary>
    /// Gets the user's preferred sorting mode for threads, as the service expresses it.
    /// </summary>
    /// <remarks>
    /// <para>The sorting mode is an open union, so the value the service sent is kept verbatim and
    /// <see cref="SortingMode"/> is projected from it. A mode this library does not recognize survives a read,
    /// modify and write cycle rather than being discarded.</para>
    /// </remarks>
    [JsonInclude]
    [JsonPropertyName("sort")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    internal string? Sort { get; init; }

    /// <summary>
    /// The user's preferred sorting mode for threads.
    /// </summary>
    /// <remarks>
    /// <para>A sorting mode this library does not recognize is reported as <see cref="ThreadSortingMode.Unknown"/>.</para>
    /// </remarks>
    [JsonIgnore]
    public ThreadSortingMode? SortingMode => Sort is null ? null : ActorWireValues.ToThreadSortingMode(Sort);

    /// <summary>
    /// Flag indicating whether to show followed users at the top of all replies.
    /// </summary>
    [JsonInclude]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? PrioritizeFollowedUsers { get; init; }
}

/// <summary>
/// The sorting mode for the replies in a thread.
/// </summary>
public enum ThreadSortingMode
{
    /// <summary>
    /// Show oldest replies first.
    /// </summary>
    Oldest,

    /// <summary>
    /// Show newest replies first.
    /// </summary>
    Newest,

    /// <summary>
    /// Show the replies with the most likes first.
    /// </summary>
    MostLikes,

    /// <summary>
    /// Randomize the thread replies.
    /// </summary>
    Random,

    /// <summary>
    /// Show the replies the service considers hottest first.
    /// </summary>
    Hotness,

    /// <summary>
    /// The sorting mode is one this library does not recognize.
    /// </summary>
    /// <remarks>
    /// <para>This value only ever comes from the service. It cannot be used to build a
    /// <see cref="ThreadViewPreference"/>, as it carries no mode the service would understand.</para>
    /// </remarks>
    Unknown
}
