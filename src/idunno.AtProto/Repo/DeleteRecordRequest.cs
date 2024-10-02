// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.AtProto.Repo
{
    internal sealed record DeleteRecordRequest
    {
        internal DeleteRecordRequest(AtIdentifier repo, Nsid collection, string rKey)
        {
            Repo = repo;
            Collection = collection;
            Rkey = rKey;
        }

        internal DeleteRecordRequest(AtIdentifier repo, string collection, string rKey)
        {
            Repo = repo;
            Collection = new Nsid(collection);
            Rkey = rKey;
        }

        [JsonInclude]
        [JsonRequired]
        public AtIdentifier Repo { get; internal set; }

        [JsonInclude]
        [JsonRequired]
        public Nsid Collection { get; internal set; }

        [JsonInclude]
        public string Rkey { get; internal set; }

        [JsonInclude]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public AtCid? SwapRecord { get; internal set; }

        [JsonInclude]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public AtCid? SwapCommit { get; internal set; }
    }
}
