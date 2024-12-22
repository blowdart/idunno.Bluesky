// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.AtProto.Repo.Models
{
    /// <summary>
    /// Encapsulates the information needed to make a create record request.
    /// </summary>
    /// <typeparam name="TRecord">The type of the record to be created.</typeparam>
    public sealed record CreateRecordRequest<TRecord>
    {
        // https://github.com/bluesky-social/atproto/blob/main/lexicons/com/atproto/repo/createRecord.json

        /// <summary>
        /// Creates a new instance of <see cref="CreateRecordRequest{TRecord}"/>
        /// </summary>
        /// <param name="record">The record to create.</param>
        /// <param name="collection">The <see cref="Nsid"/> of the collection the record will be created in.</param>
        /// <param name="repo">The <see cref="Did"/> of the repo the record will be created in. This would be the DID of the current authenticated user.</param>
        /// <param name="validate">Gets a flag indicating what validation will be performed, if any.</param>
        /// <param name="rKey">The record key, if any, of the record to be created.</param>
        /// <param name="swapCommit">The <see cref="Cid"/>, if any, to compare and swap with.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="record"/>, <paramref name="collection"/> or <paramref name="repo"/> is null.</exception>
        public CreateRecordRequest(
            TRecord record,
            Nsid collection,
            Did repo,
            bool? validate = true,
            RecordKey? rKey = null,
            Cid? swapCommit = null)
        {
            ArgumentNullException.ThrowIfNull(record);
            ArgumentNullException.ThrowIfNull(collection);
            ArgumentNullException.ThrowIfNull(repo);

            Record = record;

            Collection = collection;
            Repo = repo;

            Validate = validate;
            RecordKey = rKey;
            SwapCommit = swapCommit;
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
        /// Gets the <see cref="Cid"/>, if any, to compare and swap with.
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
