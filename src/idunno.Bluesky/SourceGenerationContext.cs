// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;
using idunno.AtProto.Labels;
using idunno.AtProto.Repo;
using idunno.Bluesky.Actions;
using idunno.Bluesky.Actor;
using idunno.Bluesky.Actor.Model;
using idunno.Bluesky.Chat;
using idunno.Bluesky.Chat.Model;
using idunno.Bluesky.Embed;
using idunno.Bluesky.Feed;
using idunno.Bluesky.Feed.Gates;
using idunno.Bluesky.Feed.Model;
using idunno.Bluesky.Graph;
using idunno.Bluesky.Graph.Model;
using idunno.Bluesky.Labeler;
using idunno.Bluesky.Labeler.Model;
using idunno.Bluesky.Notifications;
using idunno.Bluesky.Notifications.Model;
using idunno.Bluesky.Record;
using idunno.Bluesky.RichText;
using idunno.Bluesky.Video;
using idunno.Bluesky.Video.Model;

namespace idunno.Bluesky
{
    /// <exclude />
    [JsonSourceGenerationOptions(
        AllowOutOfOrderMetadataProperties = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        IgnoreReadOnlyProperties = false,
        GenerationMode = JsonSourceGenerationMode.Metadata,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
        WriteIndented = false)]

    [JsonSerializable(typeof(Block))]
    [JsonSerializable(typeof(Follow))]
    [JsonSerializable(typeof(Actions.Like))]
    [JsonSerializable(typeof(Repost))]

    [JsonSerializable(typeof(GetPreferencesResponse))]
    [JsonSerializable(typeof(GetProfilesResponse))]
    [JsonSerializable(typeof(GetSuggestionsResponse))]
    [JsonSerializable(typeof(PutPreferencesRequest))]
    [JsonSerializable(typeof(SearchActorsResponse))]
    [JsonSerializable(typeof(SearchActorsTypeAheadResponse))]
    [JsonSerializable(typeof(ActorViewerState))]
    [JsonSerializable(typeof(AdultContentPreference))]
    [JsonSerializable(typeof(BlueskyAppStatePreference))]
    [JsonSerializable(typeof(ContentLabelPreference))]
    [JsonSerializable(typeof(FeedViewPreference))]
    [JsonSerializable(typeof(HiddenPostsPreferences))]
    [JsonSerializable(typeof(InteractionPreferences))]
    [JsonSerializable(typeof(InterestsPreference))]
    [JsonSerializable(typeof(KnownFollowers))]
    [JsonSerializable(typeof(LabelersPreference))]
    [JsonSerializable(typeof(MutedWord))]
    [JsonSerializable(typeof(MutedWordPreferences))]
    [JsonSerializable(typeof(PersonalDetailsPreference))]
    [JsonSerializable(typeof(Preference))]
    [JsonSerializable(typeof(Preferences))]
    [JsonSerializable(typeof(ProfileAssociated))]
    [JsonSerializable(typeof(ProfileAssociatedChat))]
    [JsonSerializable(typeof(ProfileView))]
    [JsonSerializable(typeof(ProfileViewBasic))]
    [JsonSerializable(typeof(ProfileViewDetailed))]
    [JsonSerializable(typeof(SavedFeedPreference))]
    [JsonSerializable(typeof(SavedFeedPreference2))]
    [JsonSerializable(typeof(ThreadViewPreference))]
    [JsonSerializable(typeof(VerificationState))]
    [JsonSerializable(typeof(VerificationView))]
    [JsonSerializable(typeof(StatusView))]

    [JsonSerializable(typeof(BeginConversation))]
    [JsonSerializable(typeof(CreateMessage))]
    [JsonSerializable(typeof(DeleteMessage))]
    [JsonSerializable(typeof(LeaveConversation))]
    [JsonSerializable(typeof(LogBase))]
    [JsonSerializable(typeof(Logs))]
    [JsonSerializable(typeof(MessageLogBase))]
    [JsonSerializable(typeof(LogBase))]
    [JsonSerializable(typeof(ConversationIdPostRequest))]
    [JsonSerializable(typeof(ConversationResponse))]
    [JsonSerializable(typeof(DeleteMessageRequest))]
    [JsonSerializable(typeof(GetLogResponse))]
    [JsonSerializable(typeof(GetMessagesResponse))]
    [JsonSerializable(typeof(ListConversationsResponse))]
    [JsonSerializable(typeof(SendMessageRequest))]
    [JsonSerializable(typeof(SendMessageBatchRequest))]
    [JsonSerializable(typeof(SendMessageBatchResponse))]
    [JsonSerializable(typeof(UpdateReadRequest))]
    [JsonSerializable(typeof(BatchedMessage))]
    [JsonSerializable(typeof(ConversationReference))]
    [JsonSerializable(typeof(Conversations))]
    [JsonSerializable(typeof(ConversationView))]
    [JsonSerializable(typeof(DeletedMessageView))]
    [JsonSerializable(typeof(MessageInput))]
    [JsonSerializable(typeof(MessageReference))]
    [JsonSerializable(typeof(Messages))]
    [JsonSerializable(typeof(MessageView))]
    [JsonSerializable(typeof(MessageViewBase))]
    [JsonSerializable(typeof(MessageViewSender))]
    [JsonSerializable(typeof(ReactionView))]
    [JsonSerializable(typeof(ReactionViewSender))]
    [JsonSerializable(typeof(MessageAndReactionView))]
    [JsonSerializable(typeof(ConversationStatus))]
    [JsonSerializable(typeof(AddReactionRequest))]
    [JsonSerializable(typeof(AddReactionResponse))]
    [JsonSerializable(typeof(RemoveReactionRequest))]
    [JsonSerializable(typeof(RemoveReactionResponse))]
    [JsonSerializable(typeof(UpdateAllReadRequest))]
    [JsonSerializable(typeof(UpdateAllReadResponse))]

    [JsonSerializable(typeof(AspectRatio))]
    [JsonSerializable(typeof(EmbeddedBase))]
    [JsonSerializable(typeof(EmbeddedExternal))]
    [JsonSerializable(typeof(EmbeddedExternalView))]
    [JsonSerializable(typeof(EmbeddedImage))]
    [JsonSerializable(typeof(EmbeddedImages))]
    [JsonSerializable(typeof(EmbeddedImageView))]
    [JsonSerializable(typeof(EmbeddedImagesView))]
    [JsonSerializable(typeof(EmbeddedMediaBase))]
    [JsonSerializable(typeof(EmbeddedRecord))]
    [JsonSerializable(typeof(EmbeddedRecordView))]
    [JsonSerializable(typeof(EmbeddedRecordWithMedia))]
    [JsonSerializable(typeof(EmbeddedRecordWithMediaView))]
    [JsonSerializable(typeof(EmbeddedVideo))]
    [JsonSerializable(typeof(EmbeddedVideoView))]
    [JsonSerializable(typeof(EmbeddedView))]
    [JsonSerializable(typeof(Embed.View), TypeInfoPropertyName = "EmbedView")]
    [JsonSerializable(typeof(ViewBlocked))]
    [JsonSerializable(typeof(ViewDetached))]
    [JsonSerializable(typeof(ViewNotFound))]
    [JsonSerializable(typeof(ViewRecord))]

    [JsonSerializable(typeof(DisableEmbeddingRule))]
    [JsonSerializable(typeof(FollowerRule))]
    [JsonSerializable(typeof(FollowingRule))]
    [JsonSerializable(typeof(ListRule))]
    [JsonSerializable(typeof(MentionRule))]
    [JsonSerializable(typeof(PostGate))]
    [JsonSerializable(typeof(PostGateRule))]
    [JsonSerializable(typeof(ThreadGate))]
    [JsonSerializable(typeof(ThreadGateRule))]
    [JsonSerializable(typeof(GetActorFeedsResponse))]
    [JsonSerializable(typeof(GetActorLikesResponse))]
    [JsonSerializable(typeof(GetAuthorFeedResponse))]
    [JsonSerializable(typeof(GetFeedGeneratorsResponse))]
    [JsonSerializable(typeof(GetFeedResponse))]
    [JsonSerializable(typeof(GetLikesResponse))]
    [JsonSerializable(typeof(GetListFeedResponse))]
    [JsonSerializable(typeof(GetPostsResponse))]
    [JsonSerializable(typeof(GetQuotesResponse))]
    [JsonSerializable(typeof(GetRepostedByResponse))]
    [JsonSerializable(typeof(GetSuggestedFeedsResponse))]
    [JsonSerializable(typeof(GetTimelineResponse))]
    [JsonSerializable(typeof(SearchPostsResponse))]
    [JsonSerializable(typeof(BlockedAuthor))]
    [JsonSerializable(typeof(BlockedPost))]
    [JsonSerializable(typeof(FeedGenerator))]
    [JsonSerializable(typeof(FeedGeneratorDescription))]
    [JsonSerializable(typeof(FeedViewerState))]
    [JsonSerializable(typeof(FeedViewPost))]
    [JsonSerializable(typeof(GeneratorView))]
    [JsonSerializable(typeof(GeneratorViewerState))]
    [JsonSerializable(typeof(Feed.Like), TypeInfoPropertyName ="FeedLike")]
    [JsonSerializable(typeof(Likes))]
    [JsonSerializable(typeof(NotFoundPost))]
    [JsonSerializable(typeof(PostThread))]
    [JsonSerializable(typeof(PostView))]
    [JsonSerializable(typeof(PostViewBase))]
    [JsonSerializable(typeof(QuotesCollection))]
    [JsonSerializable(typeof(ReasonBase))]
    [JsonSerializable(typeof(ReasonPin))]
    [JsonSerializable(typeof(ReasonRepost))]
    [JsonSerializable(typeof(ReplyReference))]
    [JsonSerializable(typeof(RepostedBy))]
    [JsonSerializable(typeof(RepostedBy))]
    [JsonSerializable(typeof(SearchOrder))]
    [JsonSerializable(typeof(SearchResults))]
    [JsonSerializable(typeof(SuggestedFeeds))]
    [JsonSerializable(typeof(ThreadContext))]
    [JsonSerializable(typeof(ThreadGateView))]
    [JsonSerializable(typeof(ThreadViewPost))]
    [JsonSerializable(typeof(Timeline))]

    [JsonSerializable(typeof(GetBlocksResponse))]
    [JsonSerializable(typeof(GetFollowersResponse))]
    [JsonSerializable(typeof(GetFollowsResponse))]
    [JsonSerializable(typeof(GetListBlocksResponse))]
    [JsonSerializable(typeof(GetListResponse))]
    [JsonSerializable(typeof(GetListsResponse))]
    [JsonSerializable(typeof(GetMutesResponse))]
    [JsonSerializable(typeof(GetRelationshipsResponse))]
    [JsonSerializable(typeof(GetStarterPackResponse))]
    [JsonSerializable(typeof(GetStarterPacksResponse))]
    [JsonSerializable(typeof(MuteActorListRequest))]
    [JsonSerializable(typeof(MuteActorRequest))]
    [JsonSerializable(typeof(MuteThreadRequest))]
    [JsonSerializable(typeof(ActorRelationships))]
    [JsonSerializable(typeof(Followers))]
    [JsonSerializable(typeof(Follows))]
    [JsonSerializable(typeof(ListItemView))]
    [JsonSerializable(typeof(ListPurpose))]
    [JsonSerializable(typeof(ListView))]
    [JsonSerializable(typeof(ListViewBasic))]
    [JsonSerializable(typeof(ListViewerState))]
    [JsonSerializable(typeof(ListViewWithItems))]
    [JsonSerializable(typeof(NotFoundActor))]
    [JsonSerializable(typeof(Relationship))]
    [JsonSerializable(typeof(RelationshipType))]
    [JsonSerializable(typeof(StarterPackView))]
    [JsonSerializable(typeof(StarterPackViewBasic))]
    [JsonSerializable(typeof(SuggestedActors))]

    [JsonSerializable(typeof(ListNotificationsResponse))]
    [JsonSerializable(typeof(UnreadCountResponse))]
    [JsonSerializable(typeof(UpdateSeenRequest))]
    [JsonSerializable(typeof(Notification))]
    [JsonSerializable(typeof(NotificationCollection))]
    [JsonSerializable(typeof(NotificationReason))]

    [JsonSerializable(typeof(BlueskyRecord))]
    [JsonSerializable(typeof(BlueskyTimestampedRecord))]
    [JsonSerializable(typeof(Profile))]
    [JsonSerializable(typeof(AtProtoRepositoryRecord<Profile>))]
    [JsonSerializable(typeof(SelfLabels))]
    [JsonSerializable(typeof(StarterPack))]
    [JsonSerializable(typeof(Verification))]
    [JsonSerializable(typeof(AtProtoRepositoryRecord<Verification>))]

    [JsonSerializable(typeof(ByteSlice))]
    [JsonSerializable(typeof(Facet))]
    [JsonSerializable(typeof(FacetFeature))]
    [JsonSerializable(typeof(HashTag))]
    [JsonSerializable(typeof(Link))]
    [JsonSerializable(typeof(LinkFacetFeature))]
    [JsonSerializable(typeof(Mention))]
    [JsonSerializable(typeof(MentionFacetFeature))]
    [JsonSerializable(typeof(TagFacetFeature))]

    [JsonSerializable(typeof(JobStatusResponse))]
    [JsonSerializable(typeof(JobStatusWireFormat))]
    [JsonSerializable(typeof(UploadLimits))]

    [JsonSerializable(typeof(Post))]
    [JsonSerializable(typeof(AtProtoRepositoryRecord<Post>))]
    [JsonSerializable(typeof(Bluesky.View), TypeInfoPropertyName = "BaseView")]

    [JsonSerializable(typeof(LabelVisibility))]
    [JsonSerializable(typeof(MutedWordTarget))]
    [JsonSerializable(typeof(AllowIncomingChat))]
    [JsonSerializable(typeof(SavedFeedPreferenceType))]
    [JsonSerializable(typeof(ThreadSortingMode))]
    [JsonSerializable(typeof(ListPurpose))]

    [JsonSerializable(typeof(GetServicesResponse))]
    [JsonSerializable(typeof(LabelerView))]
    [JsonSerializable(typeof(LabelerViewDetailed))]
    [JsonSerializable(typeof(LabelerViewerState))]
    [JsonSerializable(typeof(LabelerDeclaration))]
    [JsonSerializable(typeof(AtProtoRepositoryRecord<LabelerDeclaration>))]

    [JsonSerializable(typeof(BlueskyList))]
    [JsonSerializable(typeof(BlueskyListItem))]

    [JsonSerializable(typeof(DateTimeOffset))]
    internal partial class SourceGenerationContext : JsonSerializerContext
    {
    }
}
