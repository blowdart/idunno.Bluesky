// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;
using idunno.AtProto;
using idunno.AtProto.Repo;

namespace idunno.Bluesky.Feed.Gates.Model
{
    internal record GetPostGateResponse : AtProtoReferencedObject
    {
        [JsonConstructor]
        public GetPostGateResponse(PostGate value, AtUri uri, Cid cid) : base(uri, cid)
        {
            Value = value;
        }

        [JsonInclude]
        [JsonRequired]
        public PostGate Value { get; set; }
    }
}
