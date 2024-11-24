// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.AtProto.Repo.Models
{
    public class ApplyWritesRequest
    {
        public ApplyWritesRequest(Did repo, bool? validate, ICollection<ApplyWritesRequestValueBase> writes, Cid? swapCommit)
        {
            ArgumentNullException.ThrowIfNull(repo);
            ArgumentNullException.ThrowIfNull(writes);

            if (writes.Count == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(writes), "cannot be empty.");
            }

            Repo = repo;
            Validate = validate;
            Writes = writes;
            SwapCommit = swapCommit;
        }

        [JsonInclude]
        [JsonRequired]
        public Did Repo { get; init; }

        [JsonInclude]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool? Validate { get; init; }

        [JsonInclude]
        public ICollection<ApplyWritesRequestValueBase> Writes { get; init; }

        [JsonInclude]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public Cid? SwapCommit { get; init; }
    }
}
