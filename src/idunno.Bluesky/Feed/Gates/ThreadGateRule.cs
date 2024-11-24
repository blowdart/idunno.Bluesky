// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.Bluesky.Feed.Gates
{
    /// <summary>
    /// Base record for threadgate rules
    /// </summary>
    [JsonPolymorphic(IgnoreUnrecognizedTypeDiscriminators = false, UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization)]
    [JsonDerivedType(typeof(MentionRule), typeDiscriminator: "app.bsky.feed.threadgate#mentionRule")]
    [JsonDerivedType(typeof(FollowingRule), typeDiscriminator: "app.bsky.feed.threadgate#followingRule")]
    [JsonDerivedType(typeof(ListRule), typeDiscriminator: "app.bsky.feed.threadgate#listRule")]
    public record ThreadGateRule
    {
    }
}
