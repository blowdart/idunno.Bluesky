// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.AtProto.Repo.Models
{
    public sealed record ApplyWritesDelete : ApplyWritesRequestValueBase
    {
        /// <summary>
        /// Encapsulates a delete operation for the repo.applyWrites api
        /// </summary>

        public ApplyWritesDelete(Nsid collection, RecordKey rkey) : base(collection, rkey)
        {
        }
    }
}
