// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;

namespace idunno.Bluesky.Record
{
    internal sealed record BlueskyListItem : BlueskyTimestampedRecord
    {
        public required AtUri List { get; init; }

        public required Did Subject { get; init; }
    }
}
