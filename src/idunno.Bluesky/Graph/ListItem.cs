// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

using idunno.AtProto;
using idunno.Bluesky.Record;

namespace idunno.Bluesky.Graph;

[JsonPolymorphic(
    IgnoreUnrecognizedTypeDiscriminators = true,
    UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FallBackToBaseType)]
[JsonDerivedType(typeof(ListItem), typeDiscriminator: RecordType.ListItem)]
internal record ListItem : BlueskyTimestampedRecord
{
    public required AtUri List { get; init; }

    public required Did Subject { get; init; }
}