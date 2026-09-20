// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

using idunno.Bluesky.Actor;

namespace idunno.Bluesky.Unspecced.Model;

internal sealed record GetSuggestedUsersResponse
{
    public GetSuggestedUsersResponse(ICollection<ProfileView> actors, string? recId, string? recIdStr)
    {
        Actors = actors;
        RecId = recId;
        RecIdStr = recIdStr;
    }

    [JsonRequired]
    public ICollection<ProfileView> Actors { get; init; }

    /// <summary>
    /// Gets the recommendation identifier, if the service supplied one.
    /// </summary>
    /// <remarks>
    /// <para><c>recId</c> is optional in <see href="https://github.com/bluesky-social/atproto/blob/main/lexicons/app/bsky/unspecced/getSuggestedUsers.json">app.bsky.unspecced.getSuggestedUsers</see>.</para>
    /// </remarks>
    public string? RecId { get; init; }

    /// <summary>
    /// Gets the snowflake identifier for this recommendation, if the service supplied one.
    /// </summary>
    public string? RecIdStr { get; init; }
}