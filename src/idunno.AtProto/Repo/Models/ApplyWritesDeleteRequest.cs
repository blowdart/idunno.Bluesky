﻿// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.AtProto.Repo.Models
{
    /// <summary>
    /// Encapsulates a delete operation for the repo.applyWrites api
    /// </summary>
    internal sealed record ApplyWritesDeleteRequest : ApplyWritesRequestValueBase
    {
        /// <summary>
        /// Creates a new instance of <see cref="ApplyWritesDeleteRequest"/>.
        /// </summary>
        /// <param name="collection">The collection to delete the record from.</param>
        /// <param name="rkey">The <see cref="RecordKey"/> of the record to be deleted.</param>
        public ApplyWritesDeleteRequest(Nsid collection, RecordKey rkey) : base(collection, rkey)
        {
        }
    }
}
