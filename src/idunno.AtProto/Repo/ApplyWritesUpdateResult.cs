// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto.Repo.Models;

namespace idunno.AtProto.Repo
{
    /// <summary>
    /// Encapsulates the result from a update operation to the applyWrites API.
    /// </summary>
    public sealed record ApplyWritesUpdateResult : IApplyWritesResult
    {
        internal ApplyWritesUpdateResult(ApplyWritesUpdateResponse applyWritesUpdateResponse)
        {
            Uri = applyWritesUpdateResponse.Uri;
            Cid = applyWritesUpdateResponse.Cid;
            StrongReference = new(Uri, Cid);
        }

        /// <summary>
        /// Gets the <see cref="AtUri"/> of the record the write operation updated.
        /// </summary>
        public AtUri Uri { get; init; }

        /// <summary>
        /// Gets the <see cref="Cid"/> of the record the write operation updated.
        /// </summary>
        public Cid Cid { get; init; }

        /// <summary>
        /// Gets the <see cref="StrongReference"/> of the record the write operation updated.
        /// </summary>
        public StrongReference StrongReference { get; }
    }
}
