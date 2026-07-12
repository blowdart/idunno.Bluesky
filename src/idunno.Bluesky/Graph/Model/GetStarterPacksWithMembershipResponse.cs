// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.Bluesky.Graph.Model;

internal sealed record GetStarterPacksWithMembershipResponse(ICollection<StarterPackWithMembership> StarterPacksWithMembership, string? Cursor)
{
}