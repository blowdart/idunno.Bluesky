// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

using idunno.Bluesky.Actor;

namespace idunno.Bluesky.Unspecced;

/// <summary>
/// Represents a view model for a trending <paramref name="Topic" />, including its metadata, associated <paramref name="Actors" />, and <paramref name="Status" />.
/// </summary>
/// <param name="Topic">The topic for the trend.</param>
/// <param name="DisplayName">The display name for the trend.</param>
/// <param name="Link">A link to the trend.</param>
/// <param name="StartedAt">The <see cref="DateTimeOffset"/> the trend started at.</param>
/// <param name="PostCount">The post count for the <paramref name="Topic" />.</param>
/// <param name="Status">The status of <paramref name="Topic" />.</param>
/// <param name="Category">An optional category for the <paramref name="Topic" /></param>
/// <param name="Actors">A collection of actors contributing to the <paramref name="Topic" />.</param>
/// <param name="Description">An optional description of the trend.</param>`
public sealed record TrendView(
    [field: JsonRequired] string Topic,
    [field: JsonRequired] string DisplayName,
    [field: JsonRequired] string Link,
    [field: JsonRequired] DateTimeOffset StartedAt,
    [field: JsonRequired] int PostCount,
    string? Status,
    string? Category,
    IReadOnlyCollection<ProfileViewBasic> Actors,
    string? Description) : View
{
}