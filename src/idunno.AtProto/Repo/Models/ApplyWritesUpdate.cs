// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.AtProto.Repo.Models
{
    /// <summary>
    /// Encapsulates an update operation for the repo.applyWrites api
    /// </summary>
    public sealed record ApplyWritesUpdate : ApplyWritesRequestValueBase
    {
        public ApplyWritesUpdate(Nsid collection, RecordKey rkey, object value) : base(collection, rkey)
        {
            Value = value;
        }

        [JsonInclude]
        [JsonRequired]
        public object Value { get; init; }
    }
}
