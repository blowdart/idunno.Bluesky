// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

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
        /// The maximum length of an tag, in characters.
        /// </summary>
        public static readonly int TagLengthInCharacters = 640;

        /// <summary>
        /// The maximum length of an tag, in graphemes.
        /// </summary>
        public static readonly int TagLengthInGraphemes = 64;

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

        /// <summary>
        /// The maximum number of actors that can suggested 
        /// </summary>
        public static readonly int SuggestedActors = 100;

        /// <summary>
        /// The maximum number of conversations to list.
        /// </summary>
        public static readonly int ConversationsToList = 100;

        /// <summary>
        /// The maximum number of members in a conversation.
        /// </summary>
        public static readonly int ConversationMembers = 10;

        /// <summary>
        /// The maximum number of messages to list.
        /// </summary>
        public static readonly int MessagesToList = 100;

        /// <summary>
        /// The maximum number of characters in a direct message.
        /// </summary>
        public static readonly int MessageLengthInCharacters = 10000;

        /// <summary>
        /// The maximum number of messages in a message batch.
        /// </summary>
        public static readonly int BatchedMessages = 100;

        /// <summary>
        /// The maximum number of bookmarks that can be returned from GetBookmarks.
        /// </summary>
        public static readonly int Bookmarks = 100;

        /// <summary>
        /// The maximum number of tags that a post can contain.
        /// </summary>
        public static readonly int TagsInPost = 8;
    }

    /// <summary>
    /// Well known <see cref="Did"/>s for various actors.
    /// </summary>
    public static class WellKnownDistributedIdentifiers
    {
        /// <summary>
        /// The <see cref="Did"/> for at://moderation.bsky.app.
        /// </summary>
        public static Did BlueskyModerationLabeler => new(@"did:plc:ar7c4by46qjdydhdevvrndac");

        /// <summary>
        /// The <see cref="Did"/> for the video processing system.
        /// </summary>
        public static Did Video => new("did:web:video.bsky.app");
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
        public static Nsid Post => new("app.bsky.feed.post");

        /// <summary>
        /// The NSID for a user's likes collection.
        /// </summary>
        public static Nsid Like => new("app.bsky.feed.like");

        /// <summary>
        /// The NSID for a user's reposts collection.
        /// </summary>
        public static Nsid Repost => new("app.bsky.feed.repost");

        /// <summary>
        /// The NSID for a user's follows collection.
        /// </summary>
        public static Nsid Follow => new("app.bsky.graph.follow");

        /// <summary>
        /// The NSID for a user's block collection.
        /// </summary>
        public static Nsid Block => new("app.bsky.graph.block");

        /// <summary>
        /// The NSID for a user's thread gates collection.
        /// </summary>
        public static Nsid ThreadGate => new("app.bsky.feed.threadgate");

        /// <summary>
        /// The NSID for a user's post gates collection.
        /// </summary>
        public static Nsid PostGate => new("app.bsky.feed.postgate");

        /// <summary>
        /// The NSID for an actor's profile.
        /// </summary>
        public static Nsid Profile => new("app.bsky.actor.profile");

        /// <summary>
        /// The NSID for a user's verification collection.
        /// </summary>
        public static Nsid Verification => new("app.bsky.graph.verification");

        /// <summary>
        /// The NSID for a user's list collection.
        /// </summary>
        public static Nsid List => new("app.bsky.graph.list");

        /// <summary>
        /// The NSID for the user's collection of users added to their own lists.
        /// </summary>
        public static Nsid ListItem => new("app.bsky.graph.listitem");

        /// <summary>
        /// The NSID for an actor's status record.
        /// </summary>
        public static Nsid Status => new("app.bsky.actor.status");

        /// <summary>
        /// The NSID for an actor's notification declaration record.
        /// </summary>
        public static Nsid NotificationDeclaration => new("app.bsky.notification.declaration");
    }

    /// <summary>
    /// The type discriminators used for various records in a feed or thread.
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

        /// <summary>
        /// Indicates a starter pack record.
        /// </summary>
        public const string StarterPack = "app.bsky.graph.starterpack";

        /// <summary>
        /// Indicates the declaration report for a labeler.
        /// </summary>
        public const string LabelerDeclaration = "app.bsky.labeler.service";

        /// <summary>
        /// Indicates a verification record.
        /// </summary>
        public const string Verification = "app.bsky.graph.verification";

        /// <summary>
        /// Indicates a list record.
        /// </summary>
        public const string List = "app.bsky.graph.list";

        /// <summary>
        /// Indicates a record of an item in a list.
        /// </summary>
        public const string ListItem = "app.bsky.graph.listitem";

        /// <summary>
        /// Indicates a declaration of notification preferences.
        /// </summary>
        public const string NotificationDeclaration = "app.bsky.notification.declaration";
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
    /// Type discriminators for views over embedded records.
    /// </summary>
    public static class EmbeddedViewTypeDiscriminators
    {
        /// <summary>
        /// The json type discriminator for a view record.
        /// </summary>
        public const string ViewRecord = "app.bsky.embed.record#viewRecord";

        /// <summary>
        /// The json type discriminator for an view of an embedded record.
        /// </summary>
        public const string EmbedView = "app.bsky.embed.record#view";

        /// <summary>
        /// The json type discriminator for an view of an embedded record that cannot be found.
        /// </summary>
        public const string EmbedViewNotFound = "app.bsky.embed.record#viewNotFound";

        /// <summary>
        /// The json type discriminator for a view over a record that is blocked
        /// </summary>
        public const string EmbedViewBlocked = "app.bsky.embed.record#Blocked";

        /// <summary>
        /// The json type discriminator for a view over a record that is detached
        /// </summary>
        public const string EmbedViewDetached = "app.bsky.embed.record#Detached";

        /// <summary>
        /// The json type discriminator for a view over a feed generator
        /// </summary>
        public const string GeneratorView = "app.bsky.feed.defs#generatorView";

        /// <summary>
        /// The json type discriminator for a view over a list
        /// </summary>
        public const string ListView = "app.bsky.graph.defs#listView";

        /// <summary>
        /// The json type discriminator for a view over a list
        /// </summary>
        public const string LabelerView = "app.bsky.labeler.defs#labelerView";

        /// <summary>
        /// The json type discriminator for a basic view over a starter pack
        /// </summary>
        public const string StarterPackViewBasic = "app.bsky.graph.defs#starterPackViewBasic";

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

    /// <summary>
    /// Type discriminators for message objects
    /// </summary>
    public static class MessageTypeDiscriminators
    {
        /// <summary>
        /// A view over a message.
        /// </summary>
        public const string MessageView = "chat.bsky.convo.defs#messageView";

        /// <summary>
        /// A view over a deleted message.
        /// </summary>
        public const string DeletedMessageView = "chat.bsky.convo.defs#deletedMessageView";
    }

    /// <summary>
    /// Type discriminators for conversation logging.
    /// </summary>
    public static class ConversationLogTypeDiscriminators
    {
        /// <summary>
        /// A log entry indicating the beginning of a conversation.
        /// </summary>
        public const string BeginConversation = "chat.bsky.convo.defs#logBeginConvo";

        /// <summary>
        /// A log entry indicating leaving a conversation.
        /// </summary>
        public const string LeaveConversation = "chat.bsky.convo.defs#logLeaveConvo";

        /// <summary>
        /// A log entry indicating a message was created in a conversation.
        /// </summary>
        public const string CreateMessage = "chat.bsky.convo.defs#logCreateMessage";

        /// <summary>
        /// A log entry indicating a message was deleted in a conversation.
        /// </summary>
        public const string DeleteMessage = "chat.bsky.convo.defs#logDeleteMessage";

    }
}
