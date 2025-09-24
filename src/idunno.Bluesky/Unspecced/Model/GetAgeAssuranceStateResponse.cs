// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.Bluesky.Unspecced.Model
{
    internal sealed record GetAgeAssuranceStateResponse(DateTimeOffset? LastInitiatedAt, AgeAssuranceStatus Status)
    {
    }
}
