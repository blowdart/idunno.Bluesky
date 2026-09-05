// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace idunno.Bluesky.Actor;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Encapsulates feed preferences for an actor
/// </summary>
/// <param name="Saved">A read only list of <see cref="AtUri"/>s of feeds that the actor has saved.</param>
/// <param name="Pinned">A read only list of <see cref="AtUri"/>s of feeds that the actor has pinned</param>
/// <param name="TimelineIndex">The user's timeline index, if any.</param>
public record SavedFeedsPreference(ICollection<AtUri> Saved, ICollection<AtUri> Pinned, int? TimelineIndex) : Preference
{
}