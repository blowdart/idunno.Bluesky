// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.Bluesky.Actor;

namespace idunno.Bluesky.Unspecced.Model
{
    internal sealed record GetSuggestedUsersResponse(ICollection<ProfileView> Actors)
    {
    }
}
