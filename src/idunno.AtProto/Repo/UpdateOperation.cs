// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.AtProto.Repo
{
    // We don't strongly type this as we can't perform a switch on a generic type, which we do inside
    // AtProtoServer.ApplyWrites().

    /// <summary>
    /// Encapsulates a record put operation, performed in a transaction through the <see href="https://docs.bsky.app/docs/api/com-atproto-repo-apply-writes">ApplyWrites</see> AtProto API.
    /// If a record with the specifed <see cref="RecordKey"/> exists its value will be updated to the specified value, otherwise it will be created.
    /// </summary>
    public sealed record UpdateOperation: WriteOperation
    {
        /// <summary>
        /// Creates a new instance of <see cref="UpdateOperation"/>
        /// </summary>
        /// <param name="collection">The <see cref="Nsid"/> identifying the collection the record will be updated or created in.</param>
        /// <param name="recordKey">The <see cref="RecordKey"/> the record will be updated or created with.</param>
        /// <param name="recordValue">The value of the record to be updated or created.</param>
        public UpdateOperation(Nsid collection, RecordKey recordKey, object recordValue) : base(collection, recordKey)
        {
            ArgumentNullException.ThrowIfNull(recordKey);
            ArgumentNullException.ThrowIfNull(recordValue);

            RecordValue = recordValue;
        }

        /// <summary>
        /// Gets the value of the record to get updated or created.
        /// </summary>
        public object RecordValue { get; init; }
    }
}

