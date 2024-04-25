// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.AtProto.Bluesky.Feed
{
    public record ThreadView
    {
        public ThreadView(ThreadViewPostBase thread) => Thread = thread;

        [JsonInclude]
        [JsonRequired]
        public ThreadViewPostBase Thread { get; internal set; }
    }
}
