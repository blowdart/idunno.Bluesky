// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.AtProto.Repo
{
    internal sealed record CreateRecordRequest
    {
        // https://docs.bsky.app/docs/api/com-atproto-repo-create-record
        public CreateRecordRequest(
            Nsid collection,
            Did creatorDecentralizedIdentifier,
            AtProtoRecordValue record)
        {
            Collection = collection;
            Repo = creatorDecentralizedIdentifier;
            Record = record;
        }

        public CreateRecordRequest(
            string collection,
            Did creatorDecentralizedIdentifier,
            AtProtoRecordValue record)
        {
            Collection = new Nsid(collection);
            Repo = creatorDecentralizedIdentifier;
            Record = record;
        }

        [JsonInclude]
        public Nsid? Collection { get; set; }

        [JsonRequired]
        public Did Repo { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? RecordKey { get; set; }

        public bool Validate { get; set; } = true;

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public Cid? SwapCID { get; set; }

        [JsonRequired]
        public AtProtoRecordValue Record { get; set; }
    }
}
