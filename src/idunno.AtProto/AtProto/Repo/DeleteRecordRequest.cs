// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;
using idunno.AtProto.Bluesky;

namespace idunno.AtProto.Repo
{
    /// <summary>
    /// Provides the information needed for an AT Proto delete record request.
    /// </summary>
    internal sealed record DeleteRecordRequest
    {
        /// <summary>
        /// Creates a new instance of <see cref="DeleteRecordRequest"/>.
        /// </summary>
        /// <param name="repo">The repo from which the specified record should be deleted. This is typically the DID of the current user.</param>
        /// <param name="collection">
        /// The collection within the repo from which the record should be deleted.
        /// <see cref="CollectionType"/> has some of the common collection NSIDs.</param>
        /// <param name="rkey"><The record key to be deleted./param>
        public DeleteRecordRequest(AtIdentifier repo, string collection, string rkey)
        {
            Repo = repo;
            Collection = collection;
            Rkey = rkey;
        }

        /// <summary>
        /// Gets the AT Identifier of the repo from which the specified record should be deleted. This is typically the DID of the current user.
        /// </summary>
        /// <value>The AT Identifier of the repo from which the specified record should be deleted.</value>
        [JsonInclude]
        [JsonRequired]
        public AtIdentifier Repo { get; internal set; }

        /// <summary>
        /// Gets the collection NSID of the collection from which the record should be deleted.
        /// </summary>
        /// <value>The collection NSID of the collection from which the record should be deleted.</value>
        /// <remarks><see cref="CollectionType"/> has some of the common BlueSky collection NSIDs</remarks>
        [JsonInclude]
        [JsonRequired]
        public string Collection { get; internal set; }

        /// <summary>
        /// Gets the record key of the record to be deleted.
        /// </summary>
        /// <value>
        /// The record key of the record to be deleted.
        /// </value>
        [JsonInclude]
        public string Rkey { get; internal set; }

        /// <summary>
        /// Gets an optional <see cref="AtCid"/> of a previous record that should be compared and swapped.
        /// </summary>
        /// <value>
        /// An optional <see cref="AtCid"/> of a previous record that should be compared and swapped.
        /// </value>
        [JsonInclude]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public AtCid? SwapRecord { get; internal set; }

        /// <summary>
        /// Gets an optional <see cref="AtCid"/> of a previous commit that should be compared and swapped.
        /// </summary>
        /// <value>
        /// An optional <see cref="AtCid"/> of a previous commit that should be compared and swapped.
        /// </value>
        [JsonInclude]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public AtCid? SwapCommit { get; internal set; }
    }
}
