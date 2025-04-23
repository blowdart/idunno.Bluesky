// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.AtProto.Repo.Models
{
    internal sealed record CreateRecordResponse(AtUri Uri, Cid Cid)
    {
        public Commit? Commit { get; init; }

        public string? ValidationStatus { get; init; }
    }
}
