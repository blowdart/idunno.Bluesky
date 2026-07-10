# API Endpoint Implementation Status

## Bluesky Endpoints

| Group | Endpoint | Class / Method | Status |
| ----- | -------- | -------------- | ------ |
| **Actor** | [app.bsky.actor.getProfile](https://endpoints.bsky.app/#bluesky-app/tag/appbskyactor/GET/xrpc/app.bsky.actor.getProfile) | `BlueskyAgent.GetProfile()` | ✔ |
| | [app.bsky.actor.getProfiles](https://endpoints.bsky.app/#bluesky-app/tag/appbskyactor/GET/xrpc/app.bsky.actor.getProfiles) | `BlueskyAgent.GetProfiles()` | ✔ |
| | [app.bsky.actor.getPreferences](https://endpoints.bsky.app/#bluesky-app/tag/appbskyactor/GET/xrpc/app.bsky.actor.getPreferences) | `BlueskyAgent.GetPreferences()` | ✔ |
| | [app.bsky.actor.getSuggestions](https://endpoints.bsky.app/#bluesky-app/tag/appbskyactor/GET/xrpc/app.bsky.actor.getSuggestions) | `BlueskyAgent.GetSuggestions()` | ✔ |
| | [app.bsky.actor.putPreferences](https://endpoints.bsky.app/#bluesky-app/tag/appbskyactor/POST/xrpc/app.bsky.actor.putPreferences) | `BlueskyAgent.PutPreferences()` | ✔ |
| | [app.bsky.actor.searchActors](https://endpoints.bsky.app/#bluesky-app/tag/appbskyactor/GET/xrpc/app.bsky.actor.searchActors) | `BlueskyAgent.SearchActors()` | ✔ |
| | [app.bsky.actor.searchActorsTypeahead](https://endpoints.bsky.app/#bluesky-app/tag/appbskyactor/GET/xrpc/app.bsky.actor.searchActorsTypeahead) | `BlueskyAgent.SearchActorsTypeahead()` | ✔ |
| **Bookmarks** | [app.bsky.bookmark.createBookmark](https://endpoints.bsky.app/#bluesky-app/tag/appbskybookmark/POST/xrpc/app.bsky.bookmark.createBookmark) | `BlueskyAgent.CreateBookmark()` | ✔ |
| | [app.bsky.bookmark.deleteBookmark](https://endpoints.bsky.app/#bluesky-app/tag/appbskybookmark/POST/xrpc/app.bsky.bookmark.deleteBookmark) | `BlueskyAgent.DeleteBookmark()` | ✔ |
| | [app.bsky.bookmark.getBookmarks](https://endpoints.bsky.app/#bluesky-app/tag/appbskybookmark/GET/xrpc/app.bsky.bookmark.getBookmarks) | `BlueskyAgent.GetBookmarks()` | ✔ |
| **Drafts** | [app.bsky.draft.createDraft](https://endpoints.bsky.app/#bluesky-app/tag/appbskydraft/POST/xrpc/app.bsky.draft.createDraft) | `BlueskyAgent.CreateDraft()` | ✔ |
| | [app.bsky.draft.deleteDraft](https://endpoints.bsky.app/#bluesky-app/tag/appbskydraft/POST/xrpc/app.bsky.draft.deleteDraft) | `BlueskyAgent.DeleteDraft()` | ✔ |
| | [app.bsky.draft.getDrafts](https://endpoints.bsky.app/#bluesky-app/tag/appbskydraft/GET/xrpc/app.bsky.draft.getDrafts) | `BlueskyAgent.GetDrafts()` | ✔ |
| | [app.bsky.draft.updateDraft](https://endpoints.bsky.app/#bluesky-app/tag/appbskydraft/POST/xrpc/app.bsky.draft.updateDraft) | `BlueskyAgent.UpdateDraft()` | ✔ |
| **Embed** | [app.bsky.embed.getEmbedExternalView](https://github.com/bluesky-social/atproto/blob/main/lexicons/app/bsky/embed/getEmbedExternalView.json) | `BlueskyAgent.GetEmbedExternalView()` | ✔ |
| **Feed** | [app.bsky.feed.describeFeedGenerator](https://endpoints.bsky.app/#bluesky-app/tag/appbskyfeed/GET/xrpc/app.bsky.feed.describeFeedGenerator) | `BlueskyAgent.DescribeFeedGenerator()` | ✔ |
| | [app.bsky.feed.getActorFeeds](https://endpoints.bsky.app/#bluesky-app/tag/appbskyfeed/GET/xrpc/app.bsky.feed.getActorFeeds) | `BlueskyAgent.GetActorFeeds()` | ✔ |
| | [app.bsky.feed.getActorLikes](https://endpoints.bsky.app/#bluesky-app/tag/appbskyfeed/GET/xrpc/app.bsky.feed.getActorLikes) | `BlueskyAgent.GetActorLikes()` | ✔ |
| | [app.bsky.feed.getAuthorFeed](https://endpoints.bsky.app/#bluesky-app/tag/appbskyfeed/GET/xrpc/app.bsky.feed.getAuthorFeed) | `BlueskyAgent.GetAuthorFeed()` | ✔ |
| | [app.bsky.feed.getFeed](https://endpoints.bsky.app/#bluesky-app/tag/appbskyfeed/GET/xrpc/app.bsky.feed.getFeed) | `BlueskyAgent.GetFeed()` | ✔ |
| | [app.bsky.feed.getFeedGenerator](https://endpoints.bsky.app/#bluesky-app/tag/appbskyfeed/GET/xrpc/app.bsky.feed.getFeedGenerator) | `BlueskyAgent.GetFeedGenerator`() | ✔ |
| | [app.bsky.feed.getFeedGenerators](https://endpoints.bsky.app/#bluesky-app/tag/appbskyfeed/GET/xrpc/app.bsky.feed.getFeedGenerators) | `BlueskyAgent.GetFeedGenerators`() | ✔ |
| | [app.bsky.feed.getFeedSkeleton](https://endpoints.bsky.app/#bluesky-app/tag/appbskyfeed/GET/xrpc/app.bsky.feed.getFeedSkeleton) | N/A - not for clients | ❌ |
| | [app.bsky.feed.getLikes](https://endpoints.bsky.app/#bluesky-app/tag/appbskyfeed/GET/xrpc/app.bsky.feed.getLikes) | `BlueskyAgent.GetLikes()` | ✔ |
| | [app.bsky.feed.getListFeed](https://endpoints.bsky.app/#bluesky-app/tag/appbskyfeed/GET/xrpc/app.bsky.feed.getListFeed) | `BlueskyAgent.GetListFeed()` | ✔ |
| | [app.bsky.feed.getPostThread](https://endpoints.bsky.app/#bluesky-app/tag/appbskyfeed/GET/xrpc/app.bsky.feed.getPostThread) | `BlueskyAgent.GetPostThread()` | ✔ |
| | [app.bsky.feed.getPosts](https://endpoints.bsky.app/#bluesky-app/tag/appbskyfeed/GET/xrpc/app.bsky.feed.getPosts) | `BlueskyAgent.GetPosts()` | ✔ |
| | [app.bsky.feed.getQuotes](https://endpoints.bsky.app/#bluesky-app/tag/appbskyfeed/GET/xrpc/app.bsky.feed.getQuotes) | `BlueskyAgent.GetQuotes()` | ✔ |
| | [app.bsky.feed.getRepostedBy](https://endpoints.bsky.app/#bluesky-app/tag/appbskyfeed/GET/xrpc/app.bsky.feed.getRepostedBy) | `BlueskyAgent.GetRepostedBy()` | ✔ |
| | [app.bsky.feed.getSuggestedFeeds](https://endpoints.bsky.app/#bluesky-app/tag/appbskyfeed/GET/xrpc/app.bsky.feed.getSuggestedFeeds) | `BlueskyAgent.GetSuggestedFeeds()` | ✔ |
| | [app.bsky.feed.getTimeline](https://endpoints.bsky.app/#bluesky-app/tag/appbskyfeed/GET/xrpc/app.bsky.feed.getTimeline) | `BlueskyAgent.GetTimeline()` | ✔ |
| | [app.bsky.feed.searchPosts](https://endpoints.bsky.app/#bluesky-app/tag/appbskyfeed/GET/xrpc/app.bsky.feed.searchPosts) | `BlueskyAgent.SearchPosts()` | ✔ |
| **Graph** | [app.bsky.graph.getActorStarterPacks](https://endpoints.bsky.app/#bluesky-app/tag/appbskygraph/GET/xrpc/app.bsky.graph.getActorStarterPacks) | `BlueskyAgent.GetActorStarterPacks()` | ✔ |
| | [app.bsky.graph.getBlocks](https://endpoints.bsky.app/#bluesky-app/tag/appbskygraph/GET/xrpc/app.bsky.graph.getBlocks) | `BlueskyAgent.GetBlocks()` | ✔ |
| | [app.bsky.graph.getFollowers](https://endpoints.bsky.app/#bluesky-app/tag/appbskygraph/GET/xrpc/app.bsky.graph.getFollowers) | `BlueskyAgent.GetFollowers()` | ✔ |
| | [app.bsky.graph.getFollows](https://endpoints.bsky.app/#bluesky-app/tag/appbskygraph/GET/xrpc/app.bsky.graph.getFollows) | `BlueskyAgent.GetFollows()` | ✔ |
| | [app.bsky.graph.getKnownFollowers](https://endpoints.bsky.app/#bluesky-app/tag/appbskygraph/GET/xrpc/app.bsky.graph.getKnownFollowers) | `BlueskyAgent.GetKnownFollowers()` | ✔ |
| | [app.bsky.graph.getList](https://endpoints.bsky.app/#bluesky-app/tag/appbskygraph/GET/xrpc/app.bsky.graph.getList) | `BlueskyAgent.GetList()` | ✔ |
| | [app.bsky.graph.getListBlocks](https://endpoints.bsky.app/#bluesky-app/tag/appbskygraph/GET/xrpc/app.bsky.graph.getListBlocks) | `BlueskyAgent.GetListBlocks()` | ✔ |
| | [app.bsky.graph.getListMutes](https://endpoints.bsky.app/#bluesky-app/tag/appbskygraph/GET/xrpc/app.bsky.graph.getListMutes) | `BlueskyAgent.GetListMutes()` | ✔ |
| | [app.bsky.graph.getLists](https://endpoints.bsky.app/#bluesky-app/tag/appbskygraph/GET/xrpc/app.bsky.graph.getLists) | `BlueskyAgent.GetLists()` | ✔ |
| | [app.bsky.graph.getListsWithMembership](https://endpoints.bsky.app/#bluesky-app/tag/appbskygraph/GET/xrpc/app.bsky.graph.getListsWithMembership) | `BlueskyAgent.GetListsWithMembership()` | ✔ |
| | [app.bsky.graph.getMutes](https://endpoints.bsky.app/#bluesky-app/tag/appbskygraph/GET/xrpc/app.bsky.graph.getMutes) | `BlueskyAgent.GetMutes()` | ✔ |
| | [app.bsky.graph.getRelationships](https://endpoints.bsky.app/#bluesky-app/tag/appbskygraph/GET/xrpc/app.bsky.graph.getRelationships) | `BlueskyAgent.GetRelationships()` [*](https://github.com/bluesky-social/atproto/issues/2919) | ✔ |
| | [app.bsky.graph.getStarterPack](https://endpoints.bsky.app/#bluesky-app/tag/appbskygraph/GET/xrpc/app.bsky.graph.getStarterPack) | `BlueskyAgent.GetStarterPack()` [*](https://github.com/bluesky-social/atproto/issues/2920) | ✔ |
| | [app.bsky.graph.getStarterPacks](https://endpoints.bsky.app/#bluesky-app/tag/appbskygraph/GET/xrpc/app.bsky.graph.getStarterPacks) | `BlueskyAgent.GetStarterPacks()` [*](https://github.com/bluesky-social/atproto/issues/2920) | ✔ |
| | [app.bsky.graph.getStarterPacksWithMembership](https://endpoints.bsky.app/#bluesky-app/tag/appbskygraph/GET/xrpc/app.bsky.graph.getStarterPacksWithMembership) | `BlueskyAgent.GetStarterPacksWithMembership()` | ✔ |
| | [app.bsky.graph.getSuggestedFollowsByActor](https://endpoints.bsky.app/#bluesky-app/tag/appbskygraph/GET/xrpc/app.bsky.graph.getSuggestedFollowsByActor`) | `BlueskyAgent.GetSuggestedFollowsByActor()` | ✔ |
| | [app.bsky.graph.muteActor](https://endpoints.bsky.app/#bluesky-app/tag/appbskygraph/POST/xrpc/app.bsky.graph.muteActor) | `BlueskyAgent.Mute()` | ✔ |
| | [app.bsky.graph.muteActorList](https://endpoints.bsky.app/#bluesky-app/tag/appbskygraph/POST/xrpc/app.bsky.graph.muteActorList) | `BlueskyAgent.MuteActorList()` | ✔ |
| | [app.bsky.graph.muteThread](https://endpoints.bsky.app/#bluesky-app/tag/appbskygraph/POST/xrpc/app.bsky.graph.muteThread) | `BlueskyAgent.MuteThread()` | ✔ |
| | [app.bsky.graph.searchStarterPacks](https://endpoints.bsky.app/#bluesky-app/tag/appbskygraph/GET/xrpc/app.bsky.graph.searchStarterPacks) | `BlueskyAgent.SearchStarterPacks()` | ✔ |
| | [app.bsky.graph.unmuteActor](https://endpoints.bsky.app/#bluesky-app/tag/appbskygraph/POST/xrpc/app.bsky.graph.unmuteActor) | `BlueskyAgent.Unmute()` | ✔ |
| | [app.bsky.graph.unmuteActorList](https://endpoints.bsky.app/#bluesky-app/tag/appbskygraph/POST/xrpc/app.bsky.graph.unmuteActorList) | `BlueskyAgent.UnmuteActorList()` | ✔ |
| | [app.bsky.graph.unmuteThread](https://endpoints.bsky.app/#bluesky-app/tag/appbskygraph/POST/xrpc/app.bsky.graph.unmuteThread) | `BlueskyAgent.UnmuteThread()` | ✔ |
| **Labelers** | [app.bsky.labeler.getServices](https://endpoints.bsky.app/#bluesky-app/tag/appbskylabeler/GET/xrpc/app.bsky.labeler.getServices) | `BlueskyAgent.GetLabelerServices()` | ✔ |
| **Notifications** | [app.bsky.notifications.getPreferences](https://endpoints.bsky.app/#bluesky-app/tag/appbskynotification/GET/xrpc/app.bsky.notification.getPreferences) | `BlueskyAgent.GetNotificationPreferences()` | ✔ |
| | [app.bsky.notification.getUnreadCount](https://endpoints.bsky.app/#bluesky-app/tag/appbskynotification/GET/xrpc/app.bsky.notification.getUnreadCount) | `BlueskyAgent.GetNotificationUnreadCount()` | ✔ |
| | [app.bsky.notification.listActivitySubscriptions](https://endpoints.bsky.app/#bluesky-app/tag/appbskynotification/GET/xrpc/app.bsky.notification.listActivitySubscriptions) | `BlueskyAgent.ListActivitySubscriptions()` | ✔ |
| | [app.bsky.notification.listNotifications](https://endpoints.bsky.app/#bluesky-app/tag/appbskynotification/GET/xrpc/app.bsky.notification.listNotifications) | `BlueskyAgent.ListNotifications()` | ✔ |
| | [app.bsky.notification.putActivitySubscription](https://endpoints.bsky.app/#bluesky-app/tag/appbskynotification/POST/xrpc/app.bsky.notification.putActivitySubscription) | `BlueskyAgent.SetActivitySubscription()` | ✔ |
| | [app.bsky.notifications.putPreferencesV2](https://endpoints.bsky.app/#bluesky-app/tag/appbskynotification/POST/xrpc/app.bsky.notification.putPreferencesV2) | `BlueskyAgent.SetNotificationPreferences()` | ✔ |
| | [app.bsky.notification.updateSeen](https://endpoints.bsky.app/#bluesky-app/tag/appbskynotification/POST/xrpc/app.bsky.notification.updateSeen) | `BlueskyAgent.UpdateNotificationSeenAt()` | ✔ |
| **Unspecced** | app.bsky.unspecced.getAgeAssuranceState | `BlueskyAgent.GetAgeAssuranceState()` | ✔ |
| | app.bsky.unspecced.getConfig | | ❌ |
| | app.bsky.unspecced.getPopularFeedGenerators | `BlueskyAgent.GetPopularFeedGenerators()` | ✔ |
| | app.bsky.unspecced.getPostThreadOtherV2 | | ❌ |
| | app.bsky.unspecced.getPostThreadV2 | | ❌ |
| | ~~app.bsky.unspecced.getSuggestedFeeds~~<br />[Promoted to a feed API](https://docs.bsky.app/docs/api/app-bsky-feed-get-suggested-feeds) | `BlueskyAgent.GetSuggestedFeeds()` | ❌ |
| | app.bsky.unspecced.getSuggestedFeedsSkeleton | | ❌ |
| | app.bsky.unspecced.getSuggestedStarterPacks | `BlueskyAgent.GetSuggestedStarterPacks()` | ✔ |
| | app.bsky.unspecced.getSuggestedStarterPacksSkeleton | | ❌ |
| | app.bsky.unspecced.getSuggestedUsers | `BlueskyAgent.GetSuggestedUsers()` | ✔ |
| | app.bsky.unspecced.getSuggestedUsersSkeleton | | ❌ |
| | app.bsky.unspecced.getSuggestionsSkeleton | | ❌ |
| | app.bsky.unspecced.getTaggedSuggestions | `BlueskyAgent.GetTaggedSuggestions()` | ✔ |
| | app.bsky.unspecced.getTrendingTopics | `BlueskyAgent.GetTrendingTopics()` | ✔ |
| | app.bsky.unspecced.getTrends | `BlueskyAgent.GetTrends()` | ✔ |
| | app.bsky.unspecced.getTrendsSkeleton | | ❌ |
| | app.bsky.unspecced.initAgeAssurance | | ❌ |
| | app.bsky.unspecced.searchActorsSkeleton | | ❌ |
| | app.bsky.unspecced.searchPostsSkeleton | | ❌ |
| | app.bsky.unspecced.searchStarterPacksSkeleton | | ❌ |
| **Video** | [app.bsky.video.GetJobStatus](https://endpoints.bsky.app/#bluesky-app/tag/appbskyvideo/GET/xrpc/app.bsky.video.getJobStatus) | `BlueskyAgent.GetVideoJobStatus()` | ✔ |
| | [app.bsky.video.GetUploadLimits](https://endpoints.bsky.app/#bluesky-app/tag/appbskyvideo/GET/xrpc/app.bsky.video.getUploadLimits) | `BlueskyAgent.GetVideoUploadLimits()` | ✔ |
| | [app.bsky.video.UploadVideo](https://endpoints.bsky.app/#bluesky-app/tag/appbskyvideo/POST/xrpc/app.bsky.video.uploadVideo) | `BlueskyAgent.UploadVideo()` | ✔ |

## Chat Endpoints
| Group | Endpoint | Class / Method | Status |
| ----- | -------- | -------------- | ------ |
| **Actor** | [chat.bsky.actor.getStatus](https://github.com/bluesky-social/atproto/blob/main/lexicons/chat/bsky/actor/getStatus.json) | `BlueskyAgent.GetActorChatStatus()` | ✔ |
| **Convo** | [chat.bsky.convo.acceptConvo](https://github.com/bluesky-social/atproto/blob/main/lexicons/chat/bsky/convo/acceptConvo.json) | `BlueskyAgent.AcceptConversation()` | ✔ |
| | [chat.bsky.convo.addReaction](https://github.com/bluesky-social/atproto/blob/main/lexicons/chat/bsky/convo/addReaction.json) | `BlueskyAgent.AddReaction()` | ✔ |
| | [chat.bsky.convo.deleteMessageForSelf](https://github.com/bluesky-social/atproto/blob/main/lexicons/chat/bsky/convo/deleteMessageForSelf.json) | `BlueskyAgent.DeleteMessageForSelf()` | ✔ |
| | [chat.bsky.convo.getConvo](https://github.com/bluesky-social/atproto/blob/main/lexicons/chat/bsky/convo/getConvo.json) | `BlueskyAgent.GetConversation()` | ✔ |
| | [chat.bsky.convo.getConvoAvailability](https://github.com/bluesky-social/atproto/blob/main/lexicons/chat/bsky/convo/getConvoAvailability.json) | `BlueskyAgent.GetConversationAvailability()` | ✔ |
| | [chat.bsky.convo.getConvoForMembers](https://github.com/bluesky-social/atproto/blob/main/lexicons/chat/bsky/convo/getConvoForMembers.json) | `BlueskyAgent.GetConversationForMembers()` | ✔ |
| | [chat.bsky.convo.getConvoMembers](https://github.com/bluesky-social/atproto/blob/main/lexicons/chat/bsky/convo/getConvoMembers.json) | `BlueskyAgent.GetConversationMembers()` | ✔ |
| | [chat.bsky.convo.getLog](https://github.com/bluesky-social/atproto/blob/main/lexicons/chat/bsky/convo/getLog.json) | `BlueskyAgent.GetConversationLog()` | ✔ |
| | [chat.bsky.convo.getMessages](https://github.com/bluesky-social/atproto/blob/main/lexicons/chat/bsky/convo/getMessages.json) | `BlueskyAgent.GetMessages()` | ✔ |
| | [chat.bsky.convo.getUnreadCounts](https://github.com/bluesky-social/atproto/blob/main/lexicons/chat/bsky/convo/getUnreadCounts.json) | `BlueskyAgent.GetUnreadCounts()` | ✔ |
| | [chat.bsky.convo.leaveConvo](https://github.com/bluesky-social/atproto/blob/main/lexicons/chat/bsky/convo/leaveConvo.json) | `BlueskyAgent.LeaveConversation()` | ✔ |
| | [chat.bsky.convo.listConvoRequests](https://github.com/bluesky-social/atproto/blob/main/lexicons/chat/bsky/convo/listConvoRequests.json) | `BlueskyAgent.ListConversationRequests()` [*](https://github.com/bluesky-social/atproto/issues/5175) | ✔ |
| | [chat.bsky.convo.listConvos](https://github.com/bluesky-social/atproto/blob/main/lexicons/chat/bsky/convo/listConvos.json) | `BlueskyAgent.ListConversations()` | ✔ |
| | [chat.bsky.convo.lockConvo](https://github.com/bluesky-social/atproto/blob/main/lexicons/chat/bsky/convo/lockConvo.json) | `BlueskyAgent.LockConversation()` | ✔ |
| | [chat.bsky.convo.muteConvo](https://github.com/bluesky-social/atproto/blob/main/lexicons/chat/bsky/convo/muteConvo.json) | `BlueskyAgent.MuteConversation()` | ✔ |
| | [chat.bsky.convo.removeReaction](https://github.com/bluesky-social/atproto/blob/main/lexicons/chat/bsky/convo/removeReaction.json) | `BlueskyAgent.RemoveReaction()` | ✔ |
| | [chat.bsky.convo.sendMessage](https://github.com/bluesky-social/atproto/blob/main/lexicons/chat/bsky/convo/sendMessage.json) | `BlueskyAgent.SendMessage()` | ✔ |
| | [chat.bsky.convo.sendMessageBatch](https://github.com/bluesky-social/atproto/blob/main/lexicons/chat/bsky/convo/sendMessageBatch.json) | `BlueskyAgent.SendMessageBatch()` | ✔ |
| | [chat.bsky.convo.unlockConvo](https://github.com/bluesky-social/atproto/blob/main/lexicons/chat/bsky/convo/unlockConvo.json) | `BlueskyAgent.UnlockConversation()` | ✔ |
| | [chat.bsky.convo.unmuteConvo](https://github.com/bluesky-social/atproto/blob/main/lexicons/chat/bsky/convo/unmuteConvo.json) | `BlueskyAgent.UnmuteConversation()` | ✔ |
| | [chat.bsky.convo.updateAllRead](https://github.com/bluesky-social/atproto/blob/main/lexicons/chat/bsky/convo/updateAllRead.json) | `BlueskyAgent.UpdateAllRead()` | ✔ |
| | [chat.bsky.convo.updateRead](https://github.com/bluesky-social/atproto/blob/main/lexicons/chat/bsky/convo/updateRead.json) | `BlueskyAgent.UpdateRead()` | ✔ |
| **Group** | [chat.bsky.group.addMembers](https://github.com/bluesky-social/atproto/blob/main/lexicons/chat/bsky/group/addMembers.json) | `BlueskyAgent.AddMembers()` | ✔ |
| | [chat.bsky.group.approveJoinRequest](https://github.com/bluesky-social/atproto/blob/main/lexicons/chat/bsky/group/approveJoinRequest.json) | `BlueskyAgent.ApproveJoinRequest()` | ✔ |
| | [chat.bsky.group.createGroup](https://github.com/bluesky-social/atproto/blob/main/lexicons/chat/bsky/group/createGroup.json) | `BlueskyAgent.CreateGroup()` | ✔ |
| | [chat.bsky.group.createJoinLink](https://github.com/bluesky-social/atproto/blob/main/lexicons/chat/bsky/group/createJoinLink.json) | `BlueskyAgent.CreateJoinLink()` | ✔ |
| | [chat.bsky.group.disableJoinLink](https://github.com/bluesky-social/atproto/blob/main/lexicons/chat/bsky/group/disableJoinLink.json) | `BlueskyAgent.DisableJoinLink()` | ✔ |
| | [chat.bsky.group.editGroup](https://github.com/bluesky-social/atproto/blob/main/lexicons/chat/bsky/group/createGroup.json) | `BlueskyAgent.EditGroup()` | ✔ |
| | [chat.bsky.group.editJoinLink](https://github.com/bluesky-social/atproto/blob/main/lexicons/chat/bsky/group/editJoinLink.json) | `BlueskyAgent.EditJoinLink()` | ✔ |
| | [chat.bsky.group.enableJoinLink](https://github.com/bluesky-social/atproto/blob/main/lexicons/chat/bsky/group/enableJoinLink.json) | `BlueskyAgent.EnableJoinLink()` | ✔ |
| | [chat.bsky.group.getJoinLinkPreviews](https://github.com/bluesky-social/atproto/blob/main/lexicons/chat/bsky/group/getJoinLinkPreviews.json) | `BlueskyAgent.GetJoinLinkPreviews()` | ✔ |
| | [chat.bsky.group.listJoinRequests](https://github.com/bluesky-social/atproto/blob/main/lexicons/chat/bsky/group/listJoinRequests.json) | `BlueskyAgent.ListJoinRequests()` | ✔ |
| | [chat.bsky.group.listMutualGroups](https://github.com/bluesky-social/atproto/blob/main/lexicons/chat/bsky/group/listMutualGroups.json) | `BlueskyAgent.ListMutualGroups()` | ✔ |
| | [chat.bsky.group.rejectJoinRequest](https://github.com/bluesky-social/atproto/blob/main/lexicons/chat/bsky/group/rejectJoinRequest.json) | `BlueskyAgent.RejectJoinRequest()` | ✔ |
| | [chat.bsky.group.removeMembers](https://github.com/bluesky-social/atproto/blob/main/lexicons/chat/bsky/group/removeMembers.json) | `BlueskyAgent.RemoveMembers()` | ✔ |
| | [chat.bsky.group.requestJoin](https://github.com/bluesky-social/atproto/blob/main/lexicons/chat/bsky/group/requestJoin.json) | `BlueskyAgent.RequestJoin()` | ✔ |
| | [chat.bsky.group.updateJoinRequestsRead](https://github.com/bluesky-social/atproto/blob/main/lexicons/chat/bsky/group/updateJoinRequestsRead.json) | `BlueskyAgent.UpdateJoinRequestsRead()` | ✔ |
| | [chat.bsky.group.withdrawJoinRequest](https://github.com/bluesky-social/atproto/blob/main/lexicons/chat/bsky/group/withdrawJoinRequest.json) | `BlueskyAgent.WithdrawJoinRequest()` | ✔ |
| **Notification** | [chat.bsky.notification.getPreferences](https://github.com/bluesky-social/atproto/blob/main/lexicons/chat/bsky/notification/getPreferences.json) | `BlueskyAgent.GetChatNotificationPreferences()` | ✔ |
| | [chat.bsky.notification.putPreferences](https://github.com/bluesky-social/atproto/blob/main/lexicons/chat/bsky/notification/putPreferences.json) | `BlueskyAgent.PutChatNotificationPreferences()` | ✔ |

## AT Protocol Endpoints
| Group        | Endpoint                                                     | Class / Method                | Status |
| ------------ | ------------------------------------------------------------ | ----------------------------- | ------ |
| **Identity** | * _Uses DNS and `.well-known/` endpoint resolution not the API  | `AtProtoAgent.ResolveHandle()` | ✔ |
| **Labels**   | [com.atproto.label.queryLabels](https://endpoints.bsky.app/#bluesky-app/tag/comatprotolabel/GET/xrpc/com.atproto.label.queryLabels) | `AtProtoAgent.QueryLabels()` | ✔ |
| **Moderation**   | [com.atproto.moderation.createReport](https://endpoints.bsky.app/#bluesky-app/tag/comatprotomoderation/POST/xrpc/com.atproto.moderation.createReport) | `AtProtoAgent.CreateModerationReport()` | ✔ |
| **Repo**     | [com.atproto.repo.applyWrites](https://endpoints.bsky.app/#bluesky-app/tag/comatprotorepo/POST/xrpc/com.atproto.repo.applyWrites) | `AtProtoAgent.ApplyWrites()` | ✔ |
| | [com.atproto.repo.createRecord](https://endpoints.bsky.app/#bluesky-app/tag/comatprotorepo/POST/xrpc/com.atproto.repo.createRecord) | `AtProtoAgent.CreateRecord()` | ✔ |
| | [com.atproto.repo.deleteRecord](https://endpoints.bsky.app/#bluesky-app/tag/comatprotorepo/POST/xrpc/com.atproto.repo.deleteRecord) | `AtProtoAgent.DeleteRecord()` | ✔ |
| | [com.atproto.repo.describeRepo](https://endpoints.bsky.app/#bluesky-app/tag/comatprotorepo/GET/xrpc/com.atproto.repo.describeRepo) | `AtProtoAgent.DescribeRepo()` | ✔ |
| | [com.atproto.repo.getRecord](https://endpoints.bsky.app/#bluesky-app/tag/comatprotorepo/GET/xrpc/com.atproto.repo.getRecord) | `AtProtoAgent.GetRecord()` | ✔ |
| | [com.atproto.repo.listRecords](https://endpoints.bsky.app/#bluesky-app/tag/comatprotorepo/GET/xrpc/com.atproto.repo.listRecords) | `AtProtoAgent.ListRecords()` | ✔ |
| | [com.atproto.repo.putRecord](https://endpoints.bsky.app/#bluesky-app/tag/comatprotorepo/POST/xrpc/com.atproto.repo.putRecord) | `AtProtoAgent.PutRecord()` | ✔ |
| | [com.atproto.repo.uploadBlob](https://endpoints.bsky.app/#bluesky-app/tag/comatprotorepo/POST/xrpc/com.atproto.repo.uploadBlob) | `AtProtoAgent.UploadBlob()` | ✔ |
| **Server** | [com.atproto.server.createSession](https://endpoints.bsky.app/#bluesky-app/tag/comatprotoserver/POST/xrpc/com.atproto.server.createSession) | `AtProtoAgent.Login()` | ✔ |
| | [com.atproto.server.deleteSession](https://endpoints.bsky.app/#bluesky-app/tag/comatprotoserver/POST/xrpc/com.atproto.server.deleteSession) | `AtProtoAgent.Logout()` | ✔ |
| | [com.atproto.server.describeServer](https://endpoints.bsky.app/#bluesky-app/tag/comatprotomoderation/POST/xrpc/com.atproto.moderation.createReport) | `AtProtoAgent.DescribeServer()` | ✔ |
| | [com.atproto.server.getServiceAuth](https://endpoints.bsky.app/#bluesky-app/tag/comatprotoserver/GET/xrpc/com.atproto.server.getServiceAuth) | `AtProtoAgent.GetServiceAuth()` | ✔ |
| | [com.atproto.server.getSession](https://endpoints.bsky.app/#bluesky-app/tag/comatprotoserver/GET/xrpc/com.atproto.server.getSession) | `AtProtoAgent.GetSession()` | ✔ |
| | [com.atproto.server.refreshSession](https://endpoints.bsky.app/#bluesky-app/tag/comatprotoserver/POST/xrpc/com.atproto.server.refreshSession) | `AtProtoAgent.RefreshSession()` | ✔ |
