// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

using idunno.AtProto;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace idunno.Bluesky.Actor;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Encapsulates feed preferences for an actor
/// </summary>
public record SavedFeedsPreference : Preference
{
    /// <summary>
    /// Creates a new instance of <see cref="SavedFeedsPreference"/>.
    /// </summary>
    /// <param name="saved">A list of <see cref="AtUri"/>s of feeds that the actor has saved.</param>
    /// <param name="pinned">A list of <see cref="AtUri"/>s of feeds that the actor has pinned.</param>
    /// <param name="timelineIndex">The user's timeline index, if any.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="saved"/> or <paramref name="pinned"/> is <see langword="null"/>.</exception>
    [JsonConstructor]
    public SavedFeedsPreference(ICollection<AtUri> saved, ICollection<AtUri> pinned, int? timelineIndex)
    {
        Saved = saved;
        Pinned = pinned;
        TimelineIndex = timelineIndex;
    }

    /// <summary>
    /// A read only list of <see cref="AtUri"/>s of feeds that the actor has saved.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown when the value set is <see langword="null"/>.</exception>
    [JsonRequired]
    [SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "Declared as a collection by the lexicon and copied defensively on assignment.")]
    public ICollection<AtUri> Saved
    {
        get;

        init
        {
            ArgumentNullException.ThrowIfNull(value);

            field = new List<AtUri>(value).AsReadOnly();
        }
    }

    /// <summary>
    /// A read only list of <see cref="AtUri"/>s of feeds that the actor has pinned.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown when the value set is <see langword="null"/>.</exception>
    [JsonRequired]
    [SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "Declared as a collection by the lexicon and copied defensively on assignment.")]
    public ICollection<AtUri> Pinned
    {
        get;

        init
        {
            ArgumentNullException.ThrowIfNull(value);

            field = new List<AtUri>(value).AsReadOnly();
        }
    }

    /// <summary>
    /// The user's timeline index, if any.
    /// </summary>
    public int? TimelineIndex { get; init; }
}