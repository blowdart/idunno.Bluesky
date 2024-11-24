// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;
using idunno.Bluesky.Actor;

namespace idunno.Bluesky.Graph.Model
{
    internal sealed record GetMutesResponse
    {
        [JsonConstructor]
        public GetMutesResponse(ICollection<ProfileView> mutes, string? cursor)
        {
            Mutes = mutes;
            Cursor = cursor;
        }

        [JsonInclude]
        [JsonRequired]
        public ICollection<ProfileView> Mutes { get; init; }

        [JsonInclude]
        public string? Cursor { get; init; }
    }
}
