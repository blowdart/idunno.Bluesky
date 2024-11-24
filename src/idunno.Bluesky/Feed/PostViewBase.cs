// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.Bluesky.Feed
{
    /// <summary>
    /// Base class for PostViews.
    /// </summary>
    [JsonPolymorphic(IgnoreUnrecognizedTypeDiscriminators = true, UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FallBackToNearestAncestor)]
    [JsonDerivedType(typeof(PostView), typeDiscriminator: "app.bsky.feed.defs#postView")]
    [JsonDerivedType(typeof(NotFoundPost), typeDiscriminator: "app.bsky.feed.defs#notFoundPost")]
    [JsonDerivedType(typeof(BlockedPost), typeDiscriminator: "app.bsky.feed.defs#blockedPost")]
    [JsonDerivedType(typeof(ThreadViewPost), typeDiscriminator: "app.bsky.feed.defs#threadViewPost")]
    public record PostViewBase : View
    {
    }
}
