﻿// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;
using idunno.Bluesky.Actor;

namespace idunno.Bluesky.Graph.Model
{
    internal sealed record GetFollowersResponse
    {
        [JsonConstructor]
        public GetFollowersResponse(ProfileView subject, ICollection<ProfileView> followers, string? cursor)
        {
            Subject = subject;
            Followers = followers;
            Cursor = cursor;
        }

        [JsonInclude]
        [JsonRequired]
        public ProfileView Subject { get; init; }

        [JsonInclude]
        [JsonRequired]
        public ICollection<ProfileView> Followers { get; init; }

        [JsonInclude]
        public string? Cursor { get; init; }
    }
}