# Version History

## 0.9.8

### Features

#### idunno.AtProto

* Added extra validation to `Cid`.

#### idunno.Bluesky

* Bookmark support, `CreateBookmark()`, `DeleteBookmark()`, `GetBookmarks()` and the `BookmarkCount` property on `PostView`.

### Bug Fixes

#### idunno.AtProto

* Fixed `TimeStampIdentifier` generation to generate correct values.

### Documentation

### Breaking Changes

#### idunno.AtProto

* Moved `WebSocketOptions` from `idunno.AtProto.JetStream` to `idunno.AtProto`.
* Renamed `JetstreamOptions.MaximumMessageSize` to `JetstreamOptions.BufferSize`.

## 0.9.7

### Features

#### idunno.Bluesky

* Added new overloads to `agent.Like()` and `agent.Repost()` that take a `FeedViewPost` which check if you are acting on a repost.
* Added new overloads to `agent.Like()` and `agent.Repost()` that take a `PostView`.
* Added new optional `Via` parameter to `Record.Like` and `Record.Repost` constructors that take a `StrongReference` to a repost record,
  to enable [notifications of likes and reposts of reposts](https://bsky.social/about/blog/07-02-2025-more-notification-control).
* Added new property `DisconnectedGracefully` to `JetStream`.
* Added a `RawNotificationReason` property to `Notification` for when the notification reason can't be parsed into a `NotificationReason`.
* Added the ability to declare an account's declaration for who can subscribe to notifications about their activity, `agent.SetNotificationDeclaration()`.
* Added the ability to get and set notification preferences, `agent.GetNotificationPreferences()` and `agent.SetNotificationPreferences`.
* Added the activity subscription support, `agent.ListActivitySubscriptions()`, `agent.SetActivitySubscription()`

### Bug Fixes

#### idunno.Bluesky

* Passing a DateTime to `JetStream.ConnectAsync()` sends the current cursor when opening a socket to the JetStream.
* Added `Deleted` to the JetStream `AccountStatus` enum.
* Added new notification reasons to `NotificationReason`.
 
#### Documentation

* Added details on a reconnect/retry strategy for the `JetStream`.

### Breaking Changes

#### idunno.Bluesky

* Moved `Actions.Like`, `Actions.Repost`, `Actions.Block` and `Actions.Follow` into the `Record` namespace.
* `Notification` has a new property, `RawReason`, which contains the over the wire reason for a notification as a string.
  The `Reason` property can now be `Unknown` in cases where the notification raw reason does not map to a `NotificationReason`.
* Added new notification reasons to `NotificationReason` for `Verified`, `Unverified`, `LikeViaRepost`, `RepostViaRepost` and `SubscribedPost`

## 0.9.6

* Various dependency bumps

## 0.9.5

### Features

#### idunno.Bluesky

* Add optional parameter, `embeddedPost` to `SendMessage` to allow for embedding of posts in direct messages.
* Add live status to profile views

### Bug fixes

#### idunno.Bluesky

* Fix serialization exception in `SendMessage`. Fixes [#169](https://github.com/blowdart/idunno.Bluesky/issues/169)

## 0.9.4

### Bug fixes

#### idunno.Bluesky

* Fix cursor pagination in `GetFollowers`, `GetBlocks`, `GetFollows`, `GetKnownFollowers`, `GetListBlocks`, `GetListMutes`, `GetList`, `GetLists`, and `ListConversations`.

## 0.9.3

### Bug fixes

#### idunno.AtProto

* Properly restore service uri when credentials are refreshed from a stored state. Fixes [#164](https://github.com/blowdart/idunno.Bluesky/issues/164)

## 0.9.2

### Features

#### idunno.AtProto

* Enable trimming for .NET 9.0 projects

#### idunno.AtProto.OAuthCallback

* Enable trimming for .NET 9.0 projects

#### idunno.Bluesky

* Enable trimming for .NET 9.0 projects


## 0.9.1

### Features

#### idunno.AtProto

* Add new overrides for `Login` to accept Handles and Dids.
* Add `ClientState`, `MessageLastRecieved` and `FaultCount` properties to `AtProtoJetstream` to enable health monitoring.
* Add metrics to `AtProtoJetStream`.

#### idunno.Bluesky

* Add `ExtractFacets()` to `PostBuilder` to enable auto-detection in `ReplyTo()`.
* Add new `ReplyTo()` overload to `BlueskyAgent`.
* Exposed `BlueskyServer.JsonSerializationOptions` so bots can deserialize payloads into Bluesky records with AOT support.

## 0.9.0

### Features

#### idunno.AtProto

* New `AtProtoJetstream` class for consuming the jetstream.

#### Docs

* Add Jetstream docs
* Add guidance for writing a simple scheduled post bot.

#### Samples

* Add Jetstream sample
* Add bot sample

### Breaking Changes

#### idunno.AtProto

* Grand renaming
  * `AtProtoRecord` is renamed to `AtProtoRepositoryRecord`
  * `AtProtoRecordValue` is renamed to `AtProtoRecord`
  * `AtProtoReferencedObject` is renamed to `AtProtoRepositoryObject`
  * `CreateRecord<TRecordValue>` is now `CreateRecord<TRecord>` and the `recordValue` parameter is renamed to `record`
  * `GetRecord<T>` is now `GetRecord<TRecord>` and returns `AtProtoRepositoryRecord<TRecord>`.
  * `ListRecords<TRecordValue>` is now `ListRecord<TRecord>`
  * `PutRecord<TRecordValue>` is now `PutRecord<TRecord>` and the `recordValue` parameter is renamed to `record`
  * `AtProtoRepositoryRecord<T>` added to wrap the responses from `GetRecord<T>`

#### idunno.Bluesky

* Grand renaming
  * `BlueskyRecordValue` is renamed to `BlueskyRecord`
  * `BlueskyTimestampedRecordValue` is renamed to `BlueskyTimestampedRecord`
  * `LabelerDeclarationRecord` is renamed to `ReferencedLabelerDeclaration`
  * `LabelerDeclarationRecordValue` is renamed to `LabelerDeclaration`
  * `PostRecord` is renamed to `ReferencedPost`
  * `ProfileRecord` is renamed to `ReferencedProfile`
  * `ProfileRecordValue` is renamed to `Profile`
  * `StarterPackRecordValue` is renamed to `StarterPack`
  * `VerificationRecord` is renamed to `ReferencedVerification`
  * `VerificationRecordValue` is renamed to `Verification`
  * `BlockRecordValue` is renamed to `Block`
  * `FollowRecordValue` is renamed to `Follow`
  * `LikeRecordValue` is renamed to `Like`
  * `RepostRecordValue` is renamed to `Repost`
  * `CreateBlueskyRecord<TRecordValue>` is now `BlueskyRecord<TRecord>` and the `recordValue` parameter is renamed to `record`
  * `GetPostGate()` and `GetThreadGate` now use `AtProtoRepositoryRecord<T>` internally.
  * `ReferencedLabelerDeclaration` deleted in favor of `AtProtoRepositoryRecord<LabelerDeclaration>`
  * `ReferencedPost` deleted deleted in favor of `AtProtoRepositoryRecord<Post>`.
  * `ReferencedProfile` deleted deleted in favor of `AtProtoRepositoryRecord<Profile>`.
  * `ReferencedVerification` deleted deleted in favor of `AtProtoRepositoryRecord<Verification>`.

## 0.8.0

### Features

#### idunno.Bluesky

* Add list management APIs and documentation.

## 0.7.0

### Features

#### idunno.AtProto

* Added overload to UploadBlob that takes a filename.

#### Docs

* Added simple tutorial docs with the same topic as the official sdk docs

### Bug fixes

#### idunno.Bluesky

* Correct how DeleteLike() works so it takes the ATUri of the post to delete the like from.

### Breaking Changes

#### idunno.Bluesky

* Aligned some method and parameter names to be closed to the official SDK.

## 0.6.1

### Bug Fixes

#### idunno.AtProto

* Fix DPoP nonce handling for resource providers.

#### idunno.Bluesky

* Update `BlueskyTimestampedRecordValue` to use UTC DateTimeOffsets.
* Add `CreateBlueskyRecord` to make manually creating known Bluesky records easier.
* Make `VerificationRecordValue` public so everyone can create vanity verification records for [Jerry Chen](https://bsky.app/profile/did:plc:vc7f4oafdgxsihk4cry2xpze).

### Breaking Changes

#### idunno.AtProto

* Change `CreateRecord` record parameter name from `record` to `recordValue` to more accurately reflect what the parameter type.

## 0.6.0

### Features

#### idunno.Bluesky

* Add support for verification, `VerificationView` is added to `ProfileViewBasic`, `ProfileView` and `ProfileViewDetailed`.

## 0.5.0

### Bug Fixes

#### idunno.Bluesky

* Fix [direct message support](https://github.com/blowdart/idunno.Bluesky/issues/135).

### Features

#### idunno.AtProto

* Add support for JSON source generation.
* Add builder for `AtProtoAgent`.
* Add support for `CreateModerationReport()`.
* Removed previously marked obsolete `RefreshSession()`, use `RefreshCredentials()` instead.

#### idunno.Bluesky

* Add support for JSON source generation.
* Add builder for `BlueskyAgent.`
* Wire up `at-proxy` support for Bluesky API endpoints due to upcoming [automatic forwarding deprecation](https://docs.bsky.app/blog/2025-protocol-roadmap-spring).
* Add support for adding and reading reactions to and from direct messages.
* Add support for `GetLabelerDeclaration()` and `GetLabelerServices()`.
* Add support for `CreateModerationReport()`, including well-known constants.

### Breaking Changes

#### idunno.AtProto

* Repo operations now return `*Result` records (`ApplyWritesCreateResult`, `ApplyWritesDeleteResult`, `ApplyWritesUpdateResult`, `CreateRecordResult`, `PutRecordResult`). 
  If you are using unwrapped repo operations you will need to update your type declarations, if not using `var`.

#### idunno.Bluesky

* Mirror `idunno.AtProto` return types.

## 0.4.1

### Bug Fixes

#### idunno.Bluesky

* Fix video uploading code, sample and documentation.

## 0.4.0

### Features

#### idunno.Bluesky

* Support for user thread gate and post gate preference settings.

## 0.3.0

### Features

#### idunno.AtProto

* OAuth support.

### Breaking Changes

* Agent constructors no longer take parameters directly. Use the appropriate options class instead.

  For example

  ```c#
  using (var agent = new BlueskyAgent(
    proxyUri: proxyUri,
    checkCertificateRevocationList: checkCertificateRevocationList,
    loggerFactory: loggerFactory))
  ```

  becomes

  ```c#
  using (var agent = new BlueskyAgent(
    new BlueskyAgentOptions()
    {
      LoggerFactory = loggerFactory,

      HttpClientOptions = new HttpClientOptions()
      {
        CheckCertificateRevocationList = checkCertificateRevocationList,
        ProxyUri = proxyUri
      }
    }))
  ```

* Authentication has be reworked to support OAuth.
  * `agent.Login(Credentials)` has been removed.
     Use `agent.Login(Login(string identifier, string password, string? authFactorToken = null)` instead.

* The `Session` property on an agent has been removed.

  `Credentials` can be accessed by the `Credentials` property.

  The DID for the authenticated user, if any, can be accessed by the `Did` property.

* The agent events for changes in session state have been replaced with events for changes in authentication state.

  The `SessionChanged` event has been removed, replaced by the `Authenicated` event.

  The `SessionEnded` event has been removed, replaced by the `Unauthenticated` events.

  Event arguments have changed to support OAuth credentials. If you are saving authentication state please see the AgentEvents sample for details.

* `RefreshToken()` has been removed, to manually refresh the agent credentials use `agent.RefreshCredentials()`.

#### idunno.AtProto.OAuthCallback

* Local HTTP server for testing OAuth authentication.

## 0.2.1

### Features

#### idunno.AtProto

* Extra logging in token refresh

### Bug Fixes

#### idunno.AtProto

* Fixed incorrect JWT DateTime comparison - thank you [alexmg](https://github.com/alexmg).
* Fixed json deserialization errors in `GetSessionResponse`.

#### Samples

* Added catch in Samples.SessionEvents when a bad token is being set on purpose - thank you [peteraritchie](https://github.com/peteraritchie).

## 0.2.0

### Features

#### idunno.AtProto

* Add support for `GetServiceAuth()`.

#### idunno.Bluesky

* Add support for video.

## 0.1.3

### Features

#### idunno.Bluesky

* Add self labels for posts.
* *Breaking* - Consolidation of record value classes.

### Bug fixes

#### idunno.Bluesky

* Fixed facet positioning.

### Breaking changes

#### idunno.Bluesky

*  Consolidation of record value classes.

### Docs

* Add Profile editing sample.

## 0.1.2

### Features

#### idunno.Bluesky

* Adds Profile editing.

### Bug fixes

#### idunno.Bluesky

* Fixed positioning bug for `PostBuilder` facets.
* Removed incorrect link length check.

## 0.1.1

### Bug Fixes

#### idunno.AtProto

* Fixed bug when making requests a personal PDS timed out after authentication.

## 0.1.0

### Features

#### idunno.AtProto

* PDS authentication and session management.
* List, Create, Get, Put, Delete records.
* Blob uploads.
* Handle and PDS resolution.

#### idunno.Bluesky

* Viewing feeds.
* Viewing a user's timeline and notifications.
* Viewing threads.
* Creating and deleting posts.
* Gating threads and posts.
* Likes, quotes, and reposts.
* Viewing user profiles.
* Following and unfollowing users.
* Muting and blocking users.
* Sending, receiving, and deleting messages.

