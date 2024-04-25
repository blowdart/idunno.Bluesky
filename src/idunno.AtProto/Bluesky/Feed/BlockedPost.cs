// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.AtProto.Bluesky.Feed
{
    public sealed record BlockedPost : ThreadViewPostBase
    {
        public BlockedPost(AtUri uri, bool blocked, Author author)
        {
            Uri = uri;
            Blocked = blocked;
            Author = author;
        }

        [JsonInclude]
        [JsonRequired]
        public AtUri Uri { get; internal set; }

        [JsonInclude]
        [JsonRequired]
        public bool Blocked { get; internal set; }

        [JsonInclude]
        [JsonRequired]
        public Author Author { get; internal set; }
    }
}
