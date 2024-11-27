// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.ComponentModel;
using idunno.AtProto;

namespace idunno.Bluesky
{
    /// <summary>
    /// Default URIs for Bluesky Services.
    /// </summary>
    public static class DefaultServiceUris
    {
        /// <summary>
        /// The default read/write Bluesky API URI.
        /// </summary>
        /// <remarks>
        /// <para>See <see href="https://docs.bsky.app/docs/advanced-guides/api-directory#bluesky-services">Bluesky Services</see>.</para>
        /// </remarks>
        public static readonly Uri BlueskyApiUri = new("https://api.bsky.app");

        /// <summary>
        /// The default read only Bluesky API URI.
        /// </summary>
        /// <para>See <see href="https://docs.bsky.app/docs/advanced-guides/api-directory#bluesky-services">Bluesky Services</see>.</para>
        public static readonly Uri PublicAppViewUri = new("https://public.api.bsky.app");
    }

    /// <summary>
    /// Various maximum constants
    /// </summary>
    public static class Maximum
    {
        /// <summary>
        /// The maximum length for a post, in characters.
        /// </summary>
        public static readonly int PostLengthInCharacters = 3000;

        /// <summary>
        /// The maximum length for a post, in graphemes.
        /// </summary>
        public static readonly int PostLengthInGraphemes = 300;

        /// <summary>
        /// The maximum number of images a post can contain.
        /// </summary>
        public static readonly int ImagesInPost = 4;

        /// <summary>
        /// The number of external tags that a post can contain.
        /// </summary>
        public static readonly int ExternalTagsInPost = 8;

        /// <summary>
        /// The maximum length of an tag. in characters.
        /// </summary>
        public static readonly int TagLengthInCharacters = 640;

        /// <summary>
        /// The maximum length of an tag. in graphemes.
        /// </summary>
        public static readonly int TagLengthInGraphemes = 64;

        /// <summary>
        /// The maximum length of the text for a link in a post. in characters.
        /// </summary>
        public static readonly int LinkTextLengthInCharacters = 640;

        /// <summary>
        /// The maximum length of the text for a link in a post. in graphemes.
        /// </summary>
        public static readonly int LinkTextLengthInGraphemes = 64;

        /// <summary>
        /// The maximum number of rules a thread gate can contain.
        /// </summary>
        public static readonly int ThreadGateRules = 5;

        /// <summary>
        /// The maximum number of replies that can be hidden in a thread gate.
        /// </summary>
        public static readonly int ThreadGateHiddenReplies = 50;

        /// <summary>
        /// The maximum number of rules a post gate can contain.
        /// </summary>
        public static readonly int PostGateRules = 5;

        /// <summary>
        /// The maximum number of embedding posts that can be detached in a post gate.
        /// </summary>
        public static readonly int PostGateDetachedEmbeddingPosts = 50;
    }

    /// <summary>
    /// Well known <see cref="Did"/>s for various actors.
    /// </summary>
    public static class WellKnownDistributedIdentifiers
    {
        /// <summary>
        /// The <see cref="Did"/> for at://moderation.bsky.app.
        /// </summary>
        public static Did ModerationLabeler => new(@"did:plc:ar7c4by46qjdydhdevvrndac");
    }

    /// <summary>
    /// Well-known NSIDs of Bluesky record collection used in record creation, deletion and updating.
    /// </summary>
    /// <remarks>
    /// <para>See https://atproto.com/specs/nsid for a description of NSIDs.</para>
    /// </remarks>
    public static class CollectionNsid
    {
        /// <summary>
        /// The NSID for a user's post collection.
        /// </summary>
        public static Nsid Post { get; } = new Nsid("app.bsky.feed.post");

        /// <summary>
        /// The NSID for a user's likes collection.
        /// </summary>
        public static Nsid Like { get; } = new Nsid("app.bsky.feed.like");

        /// <summary>
        /// The NSID for a user's reposts collection.
        /// </summary>
        public static Nsid Repost { get; } = new Nsid("app.bsky.feed.repost");

        /// <summary>
        /// The NSID for a user's follows collection.
        /// </summary>
        public static Nsid Follow { get; } = new Nsid("app.bsky.graph.follow");

        /// <summary>
        /// The NSID for a user's block collection.
        /// </summary>
        public static Nsid Block { get; } = new Nsid("app.bsky.graph.block");

        /// <summary>
        /// The NSID for a user's thread gates collection.
        /// </summary>
        public static Nsid ThreadGate { get; } = new Nsid("app.bsky.feed.threadgate");

        /// <summary>
        /// The NSID for a user's post gates collection.
        /// </summary>
        public static Nsid PostGate { get; } = new Nsid("app.bsky.feed.postgate");
    }

    /// <summary>
    /// The type discriminators used for various post records in a feed or thread.
    /// </summary>
    public static class RecordType
    {
        /// <summary>
        /// Indicates a post in a thread view.
        /// </summary>
        public const string ThreadViewPost = "app.bsky.feed.defs#threadViewPost";

        /// <summary>
        /// Indicates an post in a feed view.
        /// </summary>
        public const string PostView = "app.bsky.feed.defs#postView";

        /// <summary>
        /// Indicates a not found post in a feed view.
        /// </summary>
        public const string NotFoundPost = "app.bsky.feed.defs#notFoundPost";

        /// <summary>
        /// Indicates a post in a feed view that is blocked to or by the current user.
        /// </summary>
        public const string BlockedPost = "app.bsky.feed.defs#blockedPost";

        /// <summary>
        /// Indicates a post record.
        /// </summary>
        public const string Post = "app.bsky.feed.post";

        /// <summary>
        /// Indicates a like record.
        /// </summary>
        public const string Like = "app.bsky.feed.like";

        /// <summary>
        /// Indicates a repost record.
        /// </summary>
        public const string Repost = "app.bsky.feed.repost";

        /// <summary>
        /// Indicates a thread gate record
        /// </summary>
        public const string ThreadGate = "app.bsky.feed.threadgate";

        /// <summary>
        /// Indicates a post gate record
        /// </summary>
        public const string PostGate = "app.bsky.feed.postgate";

        /// <summary>
        /// Indicates a follow record.
        /// </summary>
        public const string Follow = "app.bsky.graph.follow";

        /// <summary>
        /// Indicates a block record.
        /// </summary>
        public const string Block = "app.bsky.graph.block";

        /// <summary>
        /// Indicates a profile record.
        /// </summary>
        public const string Profile = "app.bsky.actor.profile";
    }

    /// <summary>
    /// The type discriminators used for rich text facets.
    /// </summary>
    public static class FacetTypeDiscriminators
    {
        /// <summary>
        /// Indicates the facet feature for a poll
        /// </summary>
        public const string Poll = "blue.poll.post.facet#option";
    }

    /// <summary>
    /// The type discriminators used for embedded records in posts.
    /// </summary>
    public static class EmbeddedRecordTypeDiscriminators
    {
        /// <summary>
        /// The json type discriminator for an embedded record.
        /// </summary>
        public const string Record = "app.bsky.embed.record";

        /// <summary>
        /// The json type discriminator for an embedded record with media attachments
        /// </summary>
        public const string RecordWithMedia = "app.bsky.embed.recordWithMedia";

        /// <summary>
        /// The json type discriminator for embedded images.
        /// </summary>
        public const string Images = "app.bsky.embed.images";

        /// <summary>
        /// The json type discriminator for embedded external references.
        /// </summary>
        public const string External = "app.bsky.embed.external";

        /// <summary>
        /// The json type discriminator for embedded video.
        /// </summary>
        public const string Video = "app.bsky.embed.video";
    }

    /// <summary>
    /// Type discriminators for thread gate rules.
    /// </summary>
    public static class ThreadGateTypeDiscriminators
    {
        /// <summary>
        /// Allow mentioned users to reply.
        /// </summary>
        public const string MentionedUsers = "app.bsky.feed.threadgate#mentionRule";

        /// <summary>
        /// Allow followed users to reply.
        /// </summary>
        public const string FollowedUsers = "app.bsky.feed.threadgate#followingRule";

        /// <summary>
        /// Allow list members to reply.
        /// </summary>
        public const string ListMembers = "app.bsky.feed.threadgate#listRule";
    }
}
