// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.AtProto.Repo.Models
{
    /// <summary>
    /// Encapsulates the information needed to make a delete record request.
    /// </summary>
    public sealed record DeleteRecordRequest
    {
        /// <summary>
        /// Creates a new instance of <see cref="DeleteRecordRequest"/>.
        /// </summary>
        /// <param name="collection">The <see cref="Nsid"/> of the collection the record will be created in.</param>
        /// <param name="repo">The <see cref="Did"/> of the repo the record will be created in. This would be the DID of the current authenticated user.</param>
        /// <param name="rKey">The record key, if any, of the record to be created.</param>
        /// <param name="swapRecord">The record <see cref="Cid"/>, if any, to compare and swap with.</param>
        /// <param name="swapCommit">The commit <see cref="Cid"/>, if any, to compare and swap with.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="repo"/>, <paramref name="collection"/> or <paramref name="rKey"/> is null.</exception>
        public DeleteRecordRequest(AtIdentifier repo, Nsid collection, RecordKey rKey, Cid? swapRecord = null, Cid? swapCommit = null)
        {
            ArgumentNullException.ThrowIfNull(repo);
            ArgumentNullException.ThrowIfNull(collection);
            ArgumentNullException.ThrowIfNull(rKey);

            Repo = repo;
            Collection = collection;
            Rkey = rKey;

            SwapRecord = swapRecord;
            SwapCommit = swapCommit;
        }

        /// <summary>
        /// Gets the handle or DID of the repo to delete the record from.
        /// </summary>
        [JsonInclude]
        [JsonRequired]
        public AtIdentifier Repo { get; internal set; }

        /// <summary>
        /// The <see cref="Nsid" /> of the collection to delete the record from.
        /// </summary>
        [JsonInclude]
        [JsonRequired]
        public Nsid Collection { get; internal set; }

        /// <summary>
        /// Gets the record key for the record to delete.
        /// </summary>
        [JsonInclude]
        [JsonRequired]
        public RecordKey Rkey { get; internal set; }

        /// <summary>
        /// The record <see cref="Cid"/>, if any, to compare and swap with.
        /// </summary>
        [JsonInclude]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public Cid? SwapRecord { get; internal set; }

        /// <summary>
        /// The commit <see cref="Cid"/>, if any, to compare and swap with.
        /// </summary>
        [JsonInclude]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public Cid? SwapCommit { get; internal set; }
    }
}
