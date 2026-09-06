// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

using idunno.Bluesky.Actor;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace idunno.Bluesky.Feed;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Encapsulates a post's likers who the authenticated user also follows.
/// </summary>
/// <param name="Count">The overall count of known likers.</param>
/// <param name="Actors">A collection of known likers. The number of likers in this collection may be less than the overall <see cref="Count"/>.</param>
public sealed record KnownLikers(
    [property: JsonRequired] int Count,
    [property: JsonRequired] IReadOnlyCollection<ProfileViewBasic> Actors)
{
}
