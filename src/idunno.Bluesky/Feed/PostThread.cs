// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.Bluesky.Feed
{
    /// <summary>
    /// Encapsulates a thread of a post.
    /// </summary>
    public sealed record PostThread
    {
        [JsonConstructor]
        internal PostThread(PostViewBase thread, ThreadGateView? threadGate)
        {
            Thread = thread;
            ThreadGate = threadGate;
        }

        /// <summary>
        /// Gets the <see cref="PostViewBase"/> of the thread.
        /// </summary>
        [JsonInclude]
        [JsonRequired]
        public PostViewBase Thread { get; init; }

        /// <summary>
        /// Gets the thread gate applied to the thread, if any.
        /// </summary>
        [JsonInclude]
        [JsonPropertyName("threadgate")]
        public ThreadGateView? ThreadGate { get; init; }
    }
}
