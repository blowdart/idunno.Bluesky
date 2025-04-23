// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.AtProto.Repo.Models
{
    internal sealed record ApplyWritesResponse(Commit Commit, IReadOnlyCollection<ApplyWritesResponseBase> Results)
    {
    }
}
