// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.Bluesky.Feed.Model
{
    internal sealed record GetPostsResponse
    {
        [JsonConstructor]
        internal GetPostsResponse(IReadOnlyCollection<PostView> posts)
        {
            Posts = posts;
        }

        [JsonInclude]
        [JsonRequired]
        public IReadOnlyCollection<PostView> Posts { get; init; }
    }
}
