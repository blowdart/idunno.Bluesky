// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.Bluesky.Feed
{
    public record PostThread
    {
        [JsonConstructor]
        internal PostThread(PostViewBase thread, ThreadGateView? threadGate)
        {
            Thread = thread;
            ThreadGate = threadGate;
        }

        [JsonInclude]
        [JsonRequired]
        public PostViewBase Thread { get; init; }

        [JsonInclude]
        [JsonPropertyName("threadgate")]
        public ThreadGateView? ThreadGate { get; init; }
    }
}
