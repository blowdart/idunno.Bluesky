// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;
using idunno.AtProto;
using idunno.AtProto.Repo;
using idunno.Bluesky.Feed.Gates;

namespace idunno.Bluesky.Feed.Gates.Model
{
    internal record GetThreadGateResponse : AtProtoReferencedObject
    {
        [JsonConstructor]
        public GetThreadGateResponse(ThreadGate value, AtUri uri, Cid cid) : base(uri, cid)
        {
            Value = value;
        }

        [JsonInclude]
        [JsonRequired]
        public ThreadGate Value { get; set; }
    }
}
