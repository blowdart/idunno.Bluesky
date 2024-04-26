// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;
using idunno.AtProto.Repo;

namespace idunno.AtProto.Bluesky
{
    public record BlueskyPostRecordValue : AtProtoRecordValue
    {
        [JsonConstructor]
        internal BlueskyPostRecordValue(DateTime createdAt, string text) : base(createdAt)
        {
            Text = text;
        }

        [JsonInclude]
        [JsonRequired]
        public string Text { get; internal set; }
    }
}
