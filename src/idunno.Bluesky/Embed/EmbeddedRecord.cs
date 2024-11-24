// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto.Repo;

namespace idunno.Bluesky.Embed
{
    public record EmbeddedRecord : EmbeddedBase
    {
        public EmbeddedRecord(StrongReference record)
        {
            Record = record;
        }

        public StrongReference Record { get; init; }
    }
}
