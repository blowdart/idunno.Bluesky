// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

using idunno.AtProto;
using idunno.AtProto.Repo;

namespace idunno.Bluesky.Record
{
    public sealed record PostRecord : AtProtoRecord
    {
        public PostRecord(AtUri uri, Cid cid, Feed.PostRecord value) : base(uri, cid)
        {
            Value = value;
        }

        /// <summary>
        /// Gets the value of the record.
        /// </summary>
        [JsonInclude]
        public new Feed.PostRecord Value { get; init; }
    }
}
