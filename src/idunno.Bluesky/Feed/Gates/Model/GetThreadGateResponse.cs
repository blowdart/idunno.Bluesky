// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using idunno.AtProto;
using idunno.AtProto.Repo;

namespace idunno.Bluesky.Feed.Gates.Model
{
    [SuppressMessage("Performance", "CA1812", Justification = "Used in GetThreadGate.")]
    internal sealed record GetThreadGateResponse : AtProtoReferencedObject
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
