// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

using idunno.AtProto;

namespace idunno.Bluesky.Record
{
    [JsonPolymorphic(IgnoreUnrecognizedTypeDiscriminators = true,
                     UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FallBackToBaseType)]
    [JsonDerivedType(typeof(BlueskyListItem), typeDiscriminator: RecordType.ListItem)]
    internal record BlueskyListItem : BlueskyTimestampedRecord
    {
        public required AtUri List { get; init; }

        public required Did Subject { get; init; }
    }
}
