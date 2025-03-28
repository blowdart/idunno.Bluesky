// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.AtProto.Repo
{
    // We don't strongly type this as we can't perform a switch on a generic type, which we do inside
    // AtProtoServer.ApplyWrites().

    /// <summary>
    /// Encapsulates a record creation operation, performed in a transaction through the <see href="https://docs.bsky.app/docs/api/com-atproto-repo-apply-writes">ApplyWrites</see> AtProto API.
    /// </summary>
    public record CreateOperation : WriteOperation
    {
        /// <summary>
        /// Creates a new instance of <see cref="CreateOperation"/>
        /// </summary>
        /// <param name="collection">The <see cref="Nsid"/> identifying the collection the record will be created in.</param>
        /// <param name="recordValue">The value of the record to be created.</param>
        public CreateOperation(Nsid collection, object recordValue) : base(collection, null)
        {
            ArgumentNullException.ThrowIfNull(collection);
            ArgumentNullException.ThrowIfNull(recordValue);

            RecordValue = recordValue;
        }

        /// <summary>
        /// Creates a new instance of <see cref="CreateOperation"/>
        /// </summary>
        /// <param name="collection">The <see cref="Nsid"/> identifying the collection the record will be created in.</param>
        /// <param name="recordKey">The <see cref="RecordKey"/> the record will be created with.</param>
        /// <param name="recordValue">The value of the record to be created.</param>
        public CreateOperation(Nsid collection, RecordKey recordKey, object recordValue) : base(collection, recordKey)
        {
            ArgumentNullException.ThrowIfNull(collection);
            ArgumentNullException.ThrowIfNull(recordKey);
            ArgumentNullException.ThrowIfNull(recordValue);

            RecordValue = recordValue;
        }

        /// <summary>
        /// Gets the value of the record to get created.
        /// </summary>
        public object RecordValue { get; init; }
    }
}
