// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.AtProto.Repo
{
    // We don't strongly type this as we can't perform a switch on a generic type, which we do inside
    // AtProtoServer.ApplyWrites().

    /// <summary>
    /// Base class for operations for the ApplyWrites AtProto API
    /// </summary>
    public abstract record WriteOperation
    {
        /// <summary>
        /// Creates a new instance of <see cref="WriteOperation"/>
        /// </summary>
        /// <param name="collection">The <see cref="Nsid"/> of the collection the operation will apply to.</param>
        /// <param name="recordKey">The <see cref="RecordKey"/> for the record the operation will apply to.</param>
        protected WriteOperation(Nsid collection, RecordKey? recordKey)
        {
            ArgumentNullException.ThrowIfNull(collection);

            Collection = collection;
            RecordKey = recordKey;
        }

        /// <summary>
        /// Gets the <see cref="Nsid"/> of the collection the operation will apply to.
        /// </summary>
        public Nsid Collection { get; init; }

        /// <summary>
        /// Gets the <see cref="RecordKey"/> the the operation will apply to.
        /// </summary>
        public RecordKey? RecordKey { get; init; }
    }
    }
