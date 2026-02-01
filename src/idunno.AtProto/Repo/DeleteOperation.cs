// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.AtProto.Repo
{
    /// <summary>
    /// Encapsulates a record deleted operation, performed in a transaction through the <see href="https://docs.bsky.app/docs/api/com-atproto-repo-apply-writes">ApplyWrites</see> AtProto API.
    /// </summary>
    public sealed record DeleteOperation : WriteOperation
    {
        /// <summary>
        /// Creates a new instance of <see cref="DeleteOperation"/>
        /// </summary>
        /// <param name="collection">The <see cref="Nsid"/> identifying the collection the record will be deleted from.</param>
        /// <param name="recordKey">The value of the record to be deleted.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="recordKey"/> is <see langword="null"/>.</exception>
        public DeleteOperation(Nsid collection, RecordKey recordKey) : base(collection, recordKey)
        {
            ArgumentNullException.ThrowIfNull(recordKey);
        }
    }
}
