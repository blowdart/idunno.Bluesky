// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

using idunno.Bluesky.Actor;

namespace idunno.Bluesky.Graph.Model
{
    [SuppressMessage("Performance", "CA1812", Justification = "Used in GetFollows.")]
    internal sealed record GetFollowsResponse
    {
        [JsonConstructor]
        public GetFollowsResponse(ProfileView subject, ICollection<ProfileView> follows, string? cursor)
        {
            Subject = subject;
            Follows = follows;
            Cursor = cursor;
        }

        [JsonInclude]
        [JsonRequired]
        public ProfileView Subject { get; init; }

        [JsonInclude]
        [JsonRequired]
        public ICollection<ProfileView> Follows { get; init; }

        [JsonInclude]
        public string? Cursor { get; init; }
    }
}
