// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.AtProto.Bluesky.Feed
{
    [JsonDerivedType(typeof(ThreadViewPost), typeDiscriminator: RecordTypes.ThreadViewPost)]
    [JsonDerivedType(typeof(NotFoundPost), typeDiscriminator: RecordTypes.NotFoundPost)]
    [JsonDerivedType(typeof(BlockedPost), typeDiscriminator: RecordTypes.BlockedPost)]
    public abstract record ThreadViewPostBase { }
}
