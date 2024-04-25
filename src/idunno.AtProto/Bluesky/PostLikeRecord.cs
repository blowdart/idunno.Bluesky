// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;
using idunno.AtProto.Repo;

namespace idunno.AtProto.Bluesky
{
    internal sealed class PostLikeRecord : AtProtoRecord
    {
        public PostLikeRecord(AtIdentifier repo) : base(CollectionType.Like)
        {
            Repo = repo;
        }

        [JsonInclude]
        public string Collection { get; private set; } = CollectionType.Like;

        [JsonInclude]
        public AtIdentifier Repo { get; internal set; }
    }
}
