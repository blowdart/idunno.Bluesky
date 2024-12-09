# API Endpoint Implementation Status

## Bluesky Endpoints

| Group | Endpoint | Class / Method | Status |
| ----- | -------- | -------------- | ------ |
| **Actor** | [app.bsky.actor.getProfile](https://docs.bsky.app/docs/api/app-bsky-actor-get-profile) | `BlueskyAgent.GetProfile()` | ✔ |
| | [app.bsky.actor.getProfiles](https://docs.bsky.app/docs/api/app-bsky-actor-get-profiles) | `BlueskyAgent.GetProfiles()` | ✔ |
| | [app.bsky.actor.getPreferences](https://docs.bsky.app/docs/api/app-bsky-actor-get-preferences) | `BlueskyAgent.GetPreferences()` | ✔ |
| | [app.bsky.actor.getSuggestions](https://docs.bsky.app/docs/api/app-bsky-actor-get-suggestions) | `BlueskyAgent.GetSuggestions()` | ✔ |
| | [app.bsky.actor.searchActors](https://docs.bsky.app/docs/api/app-bsky-actor-search-actors) | `BlueskyAgent.SearchActors()` | ✔ |
| | [app.bsky.actor.searchActorsTypeahead](https://docs.bsky.app/docs/api/app-bsky-actor-search-actors-typeahead) | `BlueskyAgent.SearchActorsTypeahead()` | ✔ |
| **Feed** | [app.bsky.feed.DescribeFeedGenerator](https://docs.bsky.app/docs/api/app-bsky-feed-describe-feed-generator) | `BlueskyAgent.DescribeFeedGenerator()` | ✔ |
| | [app.bsky.feed.getActorFeeds](https://docs.bsky.app/docs/api/app-bsky-feed-get-actor-feeds) | `BlueskyAgent.GetActorFeeds()` | ✔ |
| | [app.bsky.feed.getActorLikes](https://docs.bsky.app/docs/api/app-bsky-feed-get-actor-likes) | `BlueskyAgent.GetActorLikes()` | ✔ |
| | [app.bsky.feed.getAuthorFeed](https://docs.bsky.app/docs/api/app-bsky-feed-get-author-feed) | `BlueskyAgent.GetAuthorFeed()` | ✔ |
| | [app.bsky.feed.getFeedGenerator](https://docs.bsky.app/docs/api/app-bsky-feed-get-feed-generator) | `BlueskyAgent.GetFeedGenerator`() | ✔ |
| | [app.bsky.feed.getFeedGenerators](https://docs.bsky.app/docs/api/app-bsky-feed-get-feed-generators) | `BlueskyAgent.GetFeedGenerators`() | ✔ |
| | [app.bsky.feed.getFeedSkeleton](https://docs.bsky.app/docs/api/app-bsky-feed-get-feed-skeleton) | N/A - not for clients | ❌ |
| | [app.bsky.feed.getFeed](https://docs.bsky.app/docs/api/app-bsky-feed-get-feed) | `BlueskyAgent.GetFeed()` | ✔ |
| | [app.bsky.feed.getLikes](https://docs.bsky.app/docs/api/app-bsky-feed-get-likes) | `BlueskyAgent.GetLikes()` | ✔ |
| | [app.bsky.feed.getListFeed](https://docs.bsky.app/docs/api/app-bsky-feed-get-list-feed) | `BlueskyAgent.GetListFeed()` | ✔ |
| | [app.bsky.feed.getPostThread](https://docs.bsky.app/docs/api/app-bsky-feed-get-post-thread) | `BlueskyAgent.GetPostThread()` | ✔ |
| | [app.bsky.feed.getPosts](https://docs.bsky.app/docs/api/app-bsky-feed-get-posts) | `BlueskyAgent.GetPosts()` | ✔ |
| | [app.bsky.feed.getQuotes](https://docs.bsky.app/docs/api/app-bsky-feed-get-quotes) | `BlueskyAgent.GetQuotes()` | ✔ |
| | [app.bsky.feed.getRepostedBy](https://docs.bsky.app/docs/api/app-bsky-feed-get-reposted-by) | `BlueskyAgent.GetRepostedBy()` | ✔ |
| | [app.bsky.feed.getSuggestedFeeds](https://docs.bsky.app/docs/api/app-bsky-feed-get-suggested-feeds) | `BlueskyAgent.GetSuggestedFeeds()` | ✔ |
| | [app.bsky.feed.getTimeline](https://docs.bsky.app/docs/api/app-bsky-feed-get-timeline) | `BlueskyAgent.GetTimeline()` | ✔ |
| | [app.bsky.feed.searchPosts](https://docs.bsky.app/docs/api/app-bsky-feed-search-posts) | `BlueskyAgent.SearchPosts()` | ✔ |
| **Graph** | [app.bsky.graph.getBlocks](https://docs.bsky.app/docs/api/app-bsky-graph-get-blocks) | `BlueskyAgent.GetBlocks()` | ✔ |
| | [app.bsky.graph.getFollowers](https://docs.bsky.app/docs/api/app-bsky-graph-get-followers) | `BlueskyAgent.GetFollowers()` | ✔ |
| | [app.bsky.graph.getFollows](https://docs.bsky.app/docs/api/app-bsky-graph-get-follows) | `BlueskyAgent.GetFollows()` | ✔ |
| | [app.bsky.graph.getKnownFollowers](https://docs.bsky.app/docs/api/app-bsky-graph-get-known-followers) | `BlueskyAgent.GetKnownFollowers()` | ✔ |
| | [app.bsky.graph.getListBlocks](https://docs.bsky.app/docs/api/app-bsky-graph-get-list-blocks) | `BlueskyAgent.GetListBlocks()` | ✔ |
| | [app.bsky.graph.getListMutes](https://docs.bsky.app/docs/api/app-bsky-graph-get-list-mutes) | `BlueskyAgent.GetListMutes()` | ✔ |
| | [app.bsky.graph.getList](https://docs.bsky.app/docs/api/app-bsky-graph-get-list) | `BlueskyAgent.GetList()` | ✔ |
| | [app.bsky.graph.getLists](https://docs.bsky.app/docs/api/app-bsky-graph-get-lists) | `BlueskyAgent.GetLists()` | ✔ |
| | [app.bsky.graph.getMutes](https://docs.bsky.app/docs/api/app-bsky-graph-get-mutes) | `BlueskyAgent.GetMutes()` | ✔ |
| | [app.bsky.graph.getRelationships](https://docs.bsky.app/docs/api/app-bsky-graph-get-relationships) | ~~`BlueskyAgent.GetRelationships()`~~ [*](https://github.com/bluesky-social/atproto/issues/2919) | ❌ |
| | [app.bsky.graph.getStarterPack](https://docs.bsky.app/docs/api/app-bsky-graph-get-starter-pack) | `BlueskyAgent.GetStarterPack()` [*](https://github.com/bluesky-social/atproto/issues/2920) | ✔ |
| | [app.bsky.graph.getStarterPacks](https://docs.bsky.app/docs/api/app-bsky-graph-get-starter-packs) | `BlueskyAgent.GetStarterPacks()` [*](https://github.com/bluesky-social/atproto/issues/2920) | ✔ |
| | [app.bsky.graph.getSuggestedFollowsByActor](https://docs.bsky.app/docs/api/app-bsky-graph-get-suggested-follows-by-actor) | `BlueskyAgent.GetSuggestedFollowsByActor()` | ✔ |
| | [app.bsky.graph.muteActorList](https://docs.bsky.app/docs/api/app-bsky-graph-mute-actor-list) | `BlueskyAgent.MuteActorList()` | ✔ |
| | [app.bsky.graph.muteActor](https://docs.bsky.app/docs/api/app-bsky-graph-mute-actor) | `BlueskyAgent.MuteActor()` | ✔ |
| | [app.bsky.graph.muteThread](https://docs.bsky.app/docs/api/app-bsky-graph-mute-thread) | `BlueskyAgent.MuteThread()` | ✔ |
| | [app.bsky.graph.unmuteActorList](https://docs.bsky.app/docs/api/app-bsky-graph-unmute-actor-list) | `BlueskyAgent.UnmuteActorList()` | ✔ |
| | [app.bsky.graph.unmuteActor](https://docs.bsky.app/docs/api/app-bsky-graph-unmute-actor) | `BlueskyAgent.UmnuteActor()` | ✔ |
| | [app.bsky.graph.unmuteThread](https://docs.bsky.app/docs/api/app-bsky-graph-unmute-thread) | `BlueskyAgent.UnmuteThread()` | ✔ |
| **Notifications** | [app.bsky.notification.getUnreadCount](https://docs.bsky.app/docs/api/app-bsky-notification-get-unread-count) | `BlueskyAgent.GetNotificationUnreadCount()` | ✔ |
| | [app.bsky.notification.listNotifications](https://docs.bsky.app/docs/api/app-bsky-notification-list-notifications) | `BlueskyAgent.ListNotifications()` | ✔ |
| | [app.bsky.notification.updateSeen](https://docs.bsky.app/docs/api/app-bsky-notification-update-seen) | `BlueskyAgent.UpdateNotificationSeenAt()` | ✔ |
| **Direct Messages** | [chat.bsky.convo.deleteMessageForSelf](https://docs.bsky.app/docs/api/chat-bsky-convo-delete-message-for-self) | `BlueskyAgent.DeleteMessageForSelf()` | ✔ |
| | [chat.bsky.convo.getConvoForMembers](https://docs.bsky.app/docs/api/chat-bsky-convo-get-convo-for-members) | `BlueskyAgent.GetConversationForMembers()` | ✔ |
| | [chat.bsky.convo.getConvo](https://docs.bsky.app/docs/api/chat-bsky-convo-get-convo) | `BlueskyAgent.GetConversation()` | ✔ |
| | [chat.bsky.convo.getLog](https://docs.bsky.app/docs/api/chat-bsky-convo-get-log) | `BlueskyAgent.GetConversationLog()` | ✔ |
| | [chat.bsky.convo.getMessages](https://docs.bsky.app/docs/api/chat-bsky-convo-get-messages) | `BlueskyAgent.GetMessages()` | ✔ |
| | [chat.bsky.convo.leaveConvo](https://docs.bsky.app/docs/api/chat-bsky-convo-leave-convo) | `BlueskyAgent.LeaveConversation()` | ✔ |
| | [chat.bsky.convo.listConvos](https://docs.bsky.app/docs/api/chat-bsky-convo-list-convos) | `BlueskyAgent.ListConversations()` | ✔ |
| | [chat.bsky.convo.muteConvos](https://docs.bsky.app/docs/api/chat-bsky-convo-mute-convo) | `BlueskyAgent.MuteConversation()` | ✔ |
| | [chat.bsky.convo.sendMessageBatch](https://docs.bsky.app/docs/api/chat-bsky-convo-send-message-batch) | `BlueskyAgent.SendMessageBatch()` | ✔ |
| | [chat.bsky.convo.sendMessage](https://docs.bsky.app/docs/api/chat-bsky-convo-send-message) | `BlueskyAgent.SendMessage()` | ✔ |
| | [chat.bsky.convo.unmuteConvos](https://docs.bsky.app/docs/api/chat-bsky-convo-unmute-convo) | `BlueskyAgent.UnmuteConversation()` | ✔ |
| | [chat.bsky.convo.updateRead](https://docs.bsky.app/docs/api/chat-bsky-convo-update-read) | `BlueskyAgent.UpdateRead()` | ✔ |

## AT Protocol Endpoints
| Group        | Endpoint                                                     | Class / Method                | Status |
| ------------ | ------------------------------------------------------------ | ----------------------------- | ------ |
| **Identity** | * _Uses DNS and .well-known endpoint resolution not the API  | `AtProtoAgent.ResolveHandle()` | ✔ |
| **Labels**   | [com.atproto.label.queryLabels](https://docs.bsky.app/docs/api/com-atproto-label-query-labels) | `AtProtoAgent.QueryLabels()` | ✔ |
| **Repo**     | [com.atproto.repo.applyWrites](https://docs.bsky.app/docs/api/com-atproto-repo-apply-writes) | `AtProtoAgent.ApplyWrites()` | ✔ |
| | [com.atproto.repo.createRecord](https://docs.bsky.app/docs/api/com-atproto-repo-create-record) | `AtProtoAgent.CreateRecord()` | ✔ |
| | [com.atproto.repo.deleteRecord](https://docs.bsky.app/docs/api/com-atproto-repo-delete-record) | `AtProtoAgent.DeleteRecord()` | ✔ |
| | [com.atproto.repo.describeRepo](https://docs.bsky.app/docs/api/com-atproto-repo-describe-repo) | `AtProtoAgent.DescribeRepo()` | ✔ |
| | [com.atproto.repo.getRecord](https://docs.bsky.app/docs/api/com-atproto-repo-get-record) | `AtProtoAgent.GetRecord()` | ✔ |
| | [com.atproto.repo.listRecords](https://docs.bsky.app/docs/api/com-atproto-repo-list-records) | `AtProtoAgent.ListRecords()` | ✔ |
| | [com.atproto.repo.putRecord](https://docs.bsky.app/docs/api/com-atproto-repo-put-record) | `AtProtoAgent.PutRecord()` | ✔ |
| | [com.atproto.repo.uploadBlob](https://docs.bsky.app/docs/api/com-atproto-repo-upload-blob) | `AtProtoAgent.UploadBlob()` | ✔ |
| **Server** | [com.atproto.server.describeServer](https://docs.bsky.app/docs/api/com-atproto-server-describe-server) | `AtProtoAgent.DescribeServer()` | ✔ |
| | [com.atproto.server.createSession](https://docs.bsky.app/docs/api/com-atproto-server-create-session) | `AtProtoAgent.Login()` | ✔ |
| | [com.atproto.server.deleteSession](https://docs.bsky.app/docs/api/com-atproto-server-delete-session) | `AtProtoAgent.Logout()` | ✔ |
| | [com.atproto.server.getSession](https://docs.bsky.app/docs/api/com-atproto-server-get-session) | `AtProtoAgent.GetSession()` | ✔ |
| | [com.atproto.server.refreshSession](https://docs.bsky.app/docs/api/com-atproto-server-refresh-session) | `AtProtoAgent.RefreshSession()` | ✔ |
