// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;
using idunno.AtProto.Repo;

namespace idunno.AtProto.Bluesky
{
    internal sealed class PostRepostRecord : AtProtoRecord
    {
        public PostRepostRecord(AtIdentifier repo) : base(CollectionType.Repost)
        {
            Repo = repo;
        }

        [JsonInclude]
        public string Collection { get; private set; } = CollectionType.Repost;

        [JsonInclude]
        public AtIdentifier Repo { get; internal set; }
    }
}
