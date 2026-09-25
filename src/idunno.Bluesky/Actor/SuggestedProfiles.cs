// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.Bluesky.Actor;

/// <summary>
/// A page of suggested profiles, together with the identifier of the recommendation which produced them.
/// </summary>
/// <param name="collection">A collection of <see cref="ProfileView"/>s to create this instance from.</param>
/// <param name="cursor">An optional cursor for pagination.</param>
/// <param name="recIdStr">An optional snowflake for the recommendation which produced the suggestions.</param>
public sealed class SuggestedProfiles(
    ICollection<ProfileView> collection,
    string? cursor = null,
    string? recIdStr = null) : PagedViewReadOnlyCollection<ProfileView>(collection, cursor)
{
    /// <summary>
    /// Gets the snowflake for the recommendation which produced these suggestions, if the service supplied one.
    /// </summary>
    /// <remarks>
    /// <para>Use this value when submitting recommendation events.</para>
    /// </remarks>
    public string? RecIdStr { get; } = recIdStr;
}
