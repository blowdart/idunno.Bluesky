// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.AtProto.Repo.Models
{
    public record DeleteRecordResponse
    {
        [JsonConstructor]
        internal DeleteRecordResponse(Commit commit)
        {
            Commit = commit;
        }

        [JsonInclude]
        [JsonRequired]
        public Commit Commit { get; init; }
    }
}
