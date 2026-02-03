// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;
using idunno.Bluesky.Actor;

namespace idunno.Bluesky.Unspecced.Model
{
    internal sealed record GetSuggestedUsersResponse
    {
        public GetSuggestedUsersResponse(ICollection<ProfileView> actors, string recId)
        {
            Actors = actors;
            RecId = recId;
        }

        [JsonRequired]
        public ICollection<ProfileView> Actors { get; init; }

        public string RecId { get; init; }
    }
}
