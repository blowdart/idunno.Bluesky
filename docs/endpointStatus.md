# Endpoint Implementation Status

A Class and Method shown in *italics* indicate classes and specialized methods wrapping the more generic AT Protocol method listed above them. 

For example, `BlueskyAgent.CreatePost()` is a specialized method for creating a Bluesky post which, in turn, uses the `AtProtoAgent.CreateRecord() `method. 

## Bluesky Endpoints

| Group | Endpoint                                                     | Class / Method                | Status |
| ----- | ------------------------------------------------------------ | ----------------------------- | ------ |
| **Actor** | [app.bsky.actor.getProfile](https://docs.bsky.app/docs/api/app-bsky-actor-get-profile) | `BlueskyAgent.GetProfile()`   | ✅      |
| | [app.bsky.actor.getProfiles](https://docs.bsky.app/docs/api/app-bsky-actor-get-profiles) | `BlueskyAgent.GetProfiles()`  | ✅      |
| | [app.bsky.actor.getSuggestions](https://docs.bsky.app/docs/api/app-bsky-actor-get-suggestions) | `BlueskyAgent.GetSuggestions()` | ✅      |
| | [app.bsky.actor.searchActors](https://docs.bsky.app/docs/api/app-bsky-actor-search-actors) | `BlueskyAgent.SearchActors()` | ✅      |
| **Feed** | [app.bsky.feed.getActorFeeds](https://docs.bsky.app/docs/api/app-bsky-feed-get-actor-feeds) |  |  |
|  | [app.bsky.feed.getActorLikes](https://docs.bsky.app/docs/api/app-bsky-feed-get-actor-likes) |  |  |
|  | [app.bsky.feed.getAuthorFeed](https://docs.bsky.app/docs/api/app-bsky-feed-get-author-feed) |  |  |
|  | [app.bsky.feed.getFeedGenerator](https://docs.bsky.app/docs/api/app-bsky-feed-get-feed-generator) |  |  |
| | [app.bsky.feed.getFeedGenerators](https://docs.bsky.app/docs/api/app-bsky-feed-get-feed-generators) | | |
| | [app.bsky.feed.getFeed](https://docs.bsky.app/docs/api/app-bsky-feed-get-feed) | | |
| | [app.bsky.feed.getLikes](https://docs.bsky.app/docs/api/app-bsky-feed-get-likes) | | |
| | [app.bsky.feed.GetListFeed](https://docs.bsky.app/docs/api/app-bsky-feed-get-list-feed) | | |
| | [app.bsky.getPostThread](https://docs.bsky.app/docs/api/app-bsky-feed-get-post-thread) | `BlueskyAgent.GetPostThread()` | ✅ |
| | [app.bsky.feed.getPosts](https://docs.bsky.app/docs/api/app-bsky-feed-get-posts) | `BlueskyAgent.Posts()` | ✅ |
|  | [app.bsky.feed.getTimeline](https://docs.bsky.app/docs/api/app-bsky-feed-get-timeline) | `BlueskyAgent.GetTimeline()` | ✅    |
| | [app.bsky.feed.searchPosts](https://docs.bsky.app/docs/api/app-bsky-feed-search-posts) | `BlueskyAgent.SearchPosts()` | ✅    |
| Notifications | [app.bsky.notification.getUnreadCount](https://docs.bsky.app/docs/api/app-bsky-notification-get-unread-count) | `BlueskyAgent.GetUnreadCount()` | ✅ |
|  | [app.bsky.notification.listNotifications](https://docs.bsky.app/docs/api/app-bsky-notification-list-notifications) | `BlueskyAgent.ListNotifications()` | ✅ |
|  | [app.bsky.notification.updateSeen](https://docs.bsky.app/docs/api/app-bsky-notification-update-seen) | `BlueskyAgent.UpdateSeen()` | ✅ |

## AT Protocol Endpoints
| Group        | Endpoint                                                     | Class / Method                | Status |
| ------------ | ------------------------------------------------------------ | ----------------------------- | ------ |
| **Identity** | [com.atproto.identity.resolveHandle](https://docs.bsky.app/docs/api/com-atproto-identity-resolve-handle) | `AtProtoAgent.ResolveHandle()` | ✅      |
| **Repo**     | [com.atproto.repo.createRecord](https://docs.bsky.app/docs/api/com-atproto-repo-create-record) | `AtProtoAgent.CreateRecord()` | ✅      |
|              |                                                              | &nbsp;&nbsp;*BlueskyAgent.CreatePost()* | ✅      |
| | | &nbsp;&nbsp;*BlueskyAgent.LikePost()* | ✅ |
| | | &nbsp;&nbsp;*BlueskyAgent.Repost()* | ✅ |
|              | [com.atproto.repo.deleteRecord](https://docs.bsky.app/docs/api/com-atproto-repo-delete-record) | `AtProtoAgent.DeleteRecord()` | ✅ |
| |  | &nbsp;&nbsp;*BlueskyAgent.DeletePost()* | ✅ |
| |  | &nbsp;&nbsp;*BlueskyAgent.UndoLike()* | ✅ |
| |  | &nbsp;&nbsp;*BlueskyAgent.UndoRepost()* | ✅ |
| | [com.atproto.repo.describeRepo](https://docs.bsky.app/docs/api/com-atproto-repo-describe-repo) | `AtProtoAgent.DescribeRepo()` | ✅ |
| **Server**   | [com.atproto.server.describeServer](https://docs.bsky.app/docs/api/com-atproto-server-describe-server) | `AtProtoAgent.DescribeServer()` | ✅      |
|              | [com.atproto.server.createSession](https://docs.bsky.app/docs/api/com-atproto-server-create-session) | `AtProtoAgent.Login()`        | ✅      |
|              | [com.atproto.server.deleteSession](https://docs.bsky.app/docs/api/com-atproto-server-delete-session) | `AtProtoAgent.Logout()`       | ✅      |
|              | [com.atproto.server.refreshSession](https://docs.bsky.app/docs/api/com-atproto-server-refresh-session) | `AtProtoAgent.RefreshSession()` | ✅      |
