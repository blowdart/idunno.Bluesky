# Version History

## 2.0.0 - Unreleased, In Progress

### Added

#### idunno.Bluesky.AspNet.Authentication

* Add support for Bluesky authentication in ASP.NET Razor Pages.

## 1.5.0 - Unreleased, In Progress

### Added

#### idunno.AtProto

* Added `TypeResolver` static class.

#### idunno.Bluesky

* Added `TypeResolver` static class.

## 1.4.0 - 2026-18-01

### Added

#### idunno.AtProto
* Add public `AtProtoJsonSerializerOptions` to enable easier creation of `JsonSerializerOptions` with chained resolvers
for custom `AtProtoRecord` classes.
* Add `ToAccessCredentials` and `ToAccessTokenCredential` methods to `Session`.

#### idunno.Bluesky

* Add "cashtag" facet detection to the default facet extractor. See [Add cashtag support for stock ticker discussions](https://github.com/bluesky-social/social-app/pull/9689)
* Add `SetLiveStatus` to indicate if a user is live streaming, and `SetStatus` and `DeleteStatus` to set and delete a user's status.
* Add `BlueskyJsonSerializerOptions` to enable easier creation of `JsonSerializerOptions` with chained resolvers

### Fixed

#### idunno.AtProto

* Fix `PutRecord<TRecord>(AtProtoRepositoryRecord<TRecord>)` to use the record CID for `swapRecord`, not the `swapCommit`.

### Changed

#### idunno.AtProto

* Update `Value` property on `AtRepositoryRecord` to be settable.

## 1.3.0 - 2026-01-10

### Added

#### idunno.AtProto

* Add non-generic version of `AtProtoHttpClient` to allow for "raw" get/post operations.
* Add `AccessTokenCredential` to wrap an access jwt for which no refresh token is available.
* Add `Resolution` class with static methods to resolve DIDs for handles,and DidDocuments and PDS URIs for handles and DIDs without needing to instantiate a client.
* Add `GetGraphemeLength` extension method on `string` to get the grapheme length of a string.

### Documentation

* Add documentation for using the raw client.
* Add documentation on using your own record types with typed client, and an approach to mapping a lexicon definition to a C# POCO.

### Changed

#### idunno.AtProto

* Bump `ZstdSharp.Port` from 0.8.6 to 0.8.7.

#### idunno.Bluesky

* Change `DeleteRepost` to accept either a repost AT-URI, or the AT-URI of the original post.
* Change access modifiers on the constructors for `Facet` and `PostBuilderFacetFeature` to `public`.

## 1.2.0 - 2025-12-27

### Added

#### idunno.AtProto

* Add .NET 10 support
* Add support for configuration binding and options in DI.
* Add `OAuthLoginState` to `SourceGenerationContext`.
* Expose `ValidationRegex` constants on `Did` and `Handle`.
* Add optional parameter `uriExtraParameters` to `BuildOAuth2LoginUri`.
* Add optional parameter `stateExtraProperties` to `BuildOAuth2LoginUri`.

#### idunno.AtProto.OAuthCallback

* Add .NET 10 support

### idunno.AtProto.Types

* Create common ATProto types `idunno.AtProto.Types` package.

#### idunno.Bluesky

* Add .NET 10 support
* Add support for configuration binding and options in DI.

### Changed

#### idunno.AtProto

* Update 3rd party dependencies.
* Fix `AtProtoAgent.RefreshCredentials()` to attempt a refresh if the access token has expired and there is a refresh token available.
* Update OAuth code for `IdentityModel.OidcClient.Extensions` version 7.0.0.

#### idunno.AtProto.OAuthCallback

* Update 3rd party dependencies.

### idunno.Bluesky

* Update 3rd party dependencies.
* Add Uri and Cid to statusView on profile responses. See [[APP-1750] Add uri and cid to statusView on profile responses](https://github.com/bluesky-social/atproto/pull/4516).

### Breaking Changes

#### idunno.AtProto

* Move common ATProto types to `idunno.AtProto.Types` package.
* The `AtProtoAgent` constructor can now take an `ClaimsPrincipal` or `ClaimsIdentity`.
  This means you must now use named parameters to call the desired constructor, e.g.
  ```c#
  using var agentWithoutLogging =
    new AtProtoAgent(service: new("https://bsky.social"));
  {
  }
  ```

#### idunno.Bluesky

* Add block information to `Graph.Relationship`, see [bsky: Expand getRelationships to include blocks](https://github.com/bluesky-social/atproto/pull/4418).
* Add `contactMatch` to `NotificationReason`, see [bsky: Add contact-match notification type](https://github.com/bluesky-social/atproto/pull/4436)
* Add `DeclaredAgePreference` to `Preferences`, see [[APP-1672] Add new read-only #declaredAgePref](https://github.com/bluesky-social/atproto/pull/4432)

## 1.1.0 - 2025-09-30

### Added

#### idunno.Bluesky

* Add support for pronouns and website in `Profile` and its associated views.
* Add support for some of the more useful unspecced APIs
  * `GetAgeAssuranceState()`
  * `GetPopularFeedGenerators()`
  * `GetSuggestedStarterPacks()`
  * `GetSuggestedUsers()`
  * `GetTaggedSuggestions()`
  * `GetTrendingTopics()`
  * `GetTrends()`
* Add support for Tags in `ReplyTo()` and `Quote()` methods.
* Expose `FacetExtractor` on `BlueskyAgent`.

### Fixed

#### idunno.AtProto

* Calling logout on an agent which is authenticated via OAuth no longer causes a DPoP error on the AtProto API.

### Breaking Changes

#### idunno.Bluesky

* The constructor for `Profile()` which takes requires values for all parameters now includes a pronouns and website parameter.

## 1.0.0 - 2025-09-17

### Added

#### idunno.Bluesky

* Add support for Tags in `Post()` methods.

## 0.9.9 - 2025-09-11

* Bump System.Text.Json from 9.0.8 to 9.0.9.

## 0.9.8 - 2025-09-08

### Added

#### idunno.AtProto

* Added extra validation to `Cid`.

#### idunno.Bluesky

* Bookmark support, `CreateBookmark()`, `DeleteBookmark()`, `GetBookmarks()` and the `BookmarkCount` property on `PostView`.

### Fixed

#### idunno.AtProto

* Fixed `TimeStampIdentifier` generation to generate correct values.

### Breaking Changes

#### idunno.AtProto

* Moved `WebSocketOptions` from `idunno.AtProto.JetStream` to `idunno.AtProto`.
* Renamed `JetstreamOptions.MaximumMessageSize` to `JetstreamOptions.BufferSize`.

## 0.9.7- 2025-07-11

### Added

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

### Fixed

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

## 0.9.6 - Unreleased

* Various dependency bumps

## 0.9.5 - 2025-06-10

### Added

#### idunno.Bluesky

* Add optional parameter, `embeddedPost` to `SendMessage` to allow for embedding of posts in direct messages.
* Add live status to profile views

### Fixed

#### idunno.Bluesky

* Fix serialization exception in `SendMessage`. Fixes [#169](https://github.com/blowdart/idunno.Bluesky/issues/169)

## 0.9.4 - 2025-05-30

### Fixed

#### idunno.Bluesky

* Fix cursor pagination in `GetFollowers`, `GetBlocks`, `GetFollows`, `GetKnownFollowers`, `GetListBlocks`, `GetListMutes`, `GetList`, `GetLists`, and `ListConversations`.

## 0.9.3 - 2025-05-30

### Fixed

#### idunno.AtProto

* Properly restore service uri when credentials are refreshed from a stored state. Fixes [#164](https://github.com/blowdart/idunno.Bluesky/issues/164)

## 0.9.2 - 2025-05-29

### Added

#### idunno.AtProto

* Enable trimming for .NET 9.0 projects

#### idunno.AtProto.OAuthCallback

* Enable trimming for .NET 9.0 projects

#### idunno.Bluesky

* Enable trimming for .NET 9.0 projects


## 0.9.1 - 2025-05-21

### Added

#### idunno.AtProto

* Add new overrides for `Login` to accept Handles and Dids.
* Add `ClientState`, `MessageLastRecieved` and `FaultCount` properties to `AtProtoJetstream` to enable health monitoring.
* Add metrics to `AtProtoJetStream`.

#### idunno.Bluesky

* Add `ExtractFacets()` to `PostBuilder` to enable auto-detection in `ReplyTo()`.
* Add new `ReplyTo()` overload to `BlueskyAgent`.
* Exposed `BlueskyServer.JsonSerializationOptions` so bots can deserialize payloads into Bluesky records with AOT support.

## 0.9.0 - 2025-05-11

### Added

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

## 0.8.0 - 2025-05-01

### Added

#### idunno.Bluesky

* Add list management APIs and documentation.

## 0.7.0 - 2025-04-26

### Added

#### idunno.AtProto

* Added overload to UploadBlob that takes a filename.

#### Docs

* Added simple tutorial docs with the same topic as the official sdk docs

### Fixed

#### idunno.Bluesky

* Correct how DeleteLike() works so it takes the ATUri of the post to delete the like from.

### Breaking Changes

#### idunno.Bluesky

* Aligned some method and parameter names to be closed to the official SDK.

## 0.6.1 - 2025-04-23

### Fixed

#### idunno.AtProto

* Fix DPoP nonce handling for resource providers.

#### idunno.Bluesky

* Update `BlueskyTimestampedRecordValue` to use UTC DateTimeOffsets.
* Add `CreateBlueskyRecord` to make manually creating known Bluesky records easier.
* Make `VerificationRecordValue` public so everyone can create vanity verification records for [Jerry Chen](https://bsky.app/profile/did:plc:vc7f4oafdgxsihk4cry2xpze).

### Breaking Changes

#### idunno.AtProto

* Change `CreateRecord` record parameter name from `record` to `recordValue` to more accurately reflect what the parameter type.

## 0.6.0 - 2025-04-21

### Added

#### idunno.Bluesky

* Add support for verification, `VerificationView` is added to `ProfileViewBasic`, `ProfileView` and `ProfileViewDetailed`.

## 0.5.0 - 2025-04-21

### Fixed

#### idunno.Bluesky

* Fix [direct message support](https://github.com/blowdart/idunno.Bluesky/issues/135).

### Added

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

## 0.4.1 - 2025-03-17

### Fixed

#### idunno.Bluesky

* Fix video uploading code, sample and documentation.

## 0.4.0 - 2025-02-18

### Added

#### idunno.Bluesky

* Support for user thread gate and post gate preference settings.

## 0.3.0 - 2025-02-15

### Added

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

## 0.2.1 - 2025-01-05

### Added

#### idunno.AtProto

* Extra logging in token refresh

### Fixed

#### idunno.AtProto

* Fixed incorrect JWT DateTime comparison - thank you [alexmg](https://github.com/alexmg).
* Fixed json deserialization errors in `GetSessionResponse`.

#### Samples

* Added catch in Samples.SessionEvents when a bad token is being set on purpose - thank you [peteraritchie](https://github.com/peteraritchie).

## 0.2.0 - 2024-12-27

### Added

#### idunno.AtProto

* Add support for `GetServiceAuth()`.

#### idunno.Bluesky

* Add support for video.

## 0.1.3 - 2024-12-22

### Added

#### idunno.Bluesky

* Add self labels for posts.
* *Breaking* - Consolidation of record value classes.

### Fixed

#### idunno.Bluesky

* Fixed facet positioning.

### Breaking changes

#### idunno.Bluesky

*  Consolidation of record value classes.

### Docs

* Add Profile editing sample.

## 0.1.2 - 2024-12-19

### Added

#### idunno.Bluesky

* Adds Profile editing.

### Fixed

#### idunno.Bluesky

* Fixed positioning bug for `PostBuilder` facets.
* Removed incorrect link length check.

## 0.1.1 - 2024-12-14

### Fixed

#### idunno.AtProto

* Fixed bug when making requests a personal PDS timed out after authentication.

## 0.1.0 - 2024-12-12

### Added

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

