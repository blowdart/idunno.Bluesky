// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.AtProto.Repo.Models
{
    public sealed record DeleteRecordRequest
    {
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

        [JsonInclude]
        [JsonRequired]
        public AtIdentifier Repo { get; internal set; }

        [JsonInclude]
        [JsonRequired]
        public Nsid Collection { get; internal set; }

        [JsonInclude]
        [JsonRequired]
        public RecordKey Rkey { get; internal set; }

        [JsonInclude]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public Cid? SwapRecord { get; internal set; }

        [JsonInclude]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public Cid? SwapCommit { get; internal set; }
    }
}
