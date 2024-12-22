// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.AtProto.Repo.Models
{
    /// <summary>
    /// Encapsulates the information needed to make a put record request.
    /// </summary>
    /// <typeparam name="TRecord">The type of the record to update.</typeparam>
    public sealed record PutRecordRequest<TRecord>
    {
        /// <summary>
        /// Creates a new instance of <see cref="PutRecordRequest{TRecord}"/>
        /// </summary>
        /// <param name="record">The record to update or create.</param>
        /// <param name="collection">The <see cref="Nsid"/> of the collection the record will be created in.</param>
        /// <param name="repo">The <see cref="Did"/> of the repo the record will be created in. This would be the DID of the current authenticated user.</param>
        /// <param name="validate">Gets a flag indicating what validation will be performed, if any.</param>
        /// <param name="rKey">The record key, if any, of the record to be created.</param>
        /// <param name="swapCommit">The <see cref="Cid"/> of the commit, if any, to compare and swap with.</param>
        /// <param name="swapRecord">The <see cref="Cid"/> of the record, if any, to compare and swap with.</param>
        public PutRecordRequest(
            TRecord record,
            Nsid collection,
            Did repo,
            RecordKey rKey,
            bool? validate = true,
            Cid? swapCommit = null,
            Cid? swapRecord = null)
        {
            ArgumentNullException.ThrowIfNull(record);
            ArgumentNullException.ThrowIfNull(collection);
            ArgumentNullException.ThrowIfNull(repo);
            ArgumentNullException.ThrowIfNull(rKey);

            Record = record;

            Collection = collection;
            Repo = repo;

            Validate = validate;
            RecordKey = rKey;
            SwapCommit = swapCommit;
            SwapRecord = swapRecord;
        }

        /// <summary>
        /// Gets the <see cref="Nsid"/> of the collection the record will be created in.
        /// </summary>
        [JsonInclude]
        public Nsid? Collection { get; init; }

        /// <summary>
        /// Gets the <see cref="Did"/> of the repo the record will be created in. This would be the DID of the current authenticated user.
        /// </summary>
        [JsonRequired]
        public Did Repo { get; init; }

        /// <summary>
        /// Gets the record key, if any, of the record to be created.
        /// </summary>
        [JsonPropertyName("rkey")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public RecordKey? RecordKey { get; init; }

        /// <summary>
        /// Gets a flag indicating what validation will be performed.
        /// A value of false skips Lexicon schema validation of record data,
        /// A value of true requires lexicon schema validation of record data,
        /// null indicates validation of the record data only occurs only for known lexicons.
        /// </summary>
        public bool? Validate { get; init; } = true;

        /// <summary>
        /// Gets the <see cref="Cid"/> of the record, if any, to compare and swap with.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public Cid? SwapRecord { get; init; }

        /// <summary>
        /// Gets the <see cref="Cid"/> of the commit, if any, to compare and swap with.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public Cid? SwapCommit { get; init; }

        /// <summary>
        /// Gets the record to create.
        /// </summary>
        [JsonRequired]
        public TRecord Record { get; init; }
    }
}
