# Version History

## 7.0.0-aspnet - ** unreleased **

### Added

#### idunno.AtProto

* Added `OAuthOptions.AllowInsecureProtocols` and `OAuthOptions.AllowLoopback`, which allow an OAuth flow to reach an authorization server or personal data
  server over HTTP, or on a loopback address, without the OAuth return uri having to use one itself. Both default to `false`, and apply to the validation of a
  discovered endpoint as well as to the transport, so a single setting now covers local development against a server on the local machine.
* Added `JetstreamOptions.CloseTimeout` and `AtProtoJetstreamBuilder.SetCloseTimeout()`, which bound how long a jetstream waits for a server to answer a close handshake before aborting the connection. The default is 30 seconds.
* Added `Resolution.VerifyHandle()` and `Resolution.ResolveVerifiedHandle()`, which check that a handle and a DID resolve to each other.
* Added an optional `maximumWellKnownResponseSize` parameter to the `Resolution.ResolveDidDocument()` and `Resolution.ResolvePds()` overloads which resolve a handle.
* Added `StringExtensions.GetUtf8Length()`, which returns the length of a string when encoded as UTF-8 bytes. AT Protocol lexicons count their
  `maxLength` constraints in UTF-8 bytes rather than in UTF-16 characters, so use this rather than `string.Length` when validating a value against a
  `maxLength` limit. It sits alongside the existing `GetGraphemeLength()`, which covers the matching `maxGraphemes` constraint.
* Added `AtProtoAgentOptions.MaximumWellKnownResponseSize`, an optional `maximumWellKnownResponseSize` parameter on the `AtProtoServer.ResolveHandle()`
  and `Resolution.ResolveHandle()` overloads, and the `AtProtoServer.DefaultMaximumWellKnownResponseSize` constant, which configure the number of bytes
  read from a `/.well-known/atproto-did` response when resolving a handle. The default is 4KB.
* Added `AtProtoAgentOptions.MaximumResponseSize`, an optional `maximumResponseSize` parameter on every `AtProtoServer` and `BlueskyServer` endpoint
  method, and the `AtProtoHttpClient.DefaultMaximumResponseSize` constant, which cap the number of bytes read from an XRPC response body. A response
  larger than the maximum fails with an `AtErrorDetail` whose `Error` is `ResponseTooLarge`. The default is 32MB. An agent applies its configured value
  to every request it makes; the process wide `AtProtoHttpClient.MaximumResponseSize` static property added earlier in this release has been removed in
  favour of the per agent option.
* Added `DirectoryAgentOptions.MaximumResponseSize` and an optional `maximumResponseSize` parameter on the `DirectoryServer.ResolveDidDocument()`,
  `Resolution.ResolveDidDocument()` and `Resolution.ResolvePds()` overloads, which cap the number of bytes read from a DID document response.
  An `AtProtoAgent` applies its `MaximumResponseSize` to the DID documents it resolves. A `did:web` DID names the host its document is resolved
  from, so that response is untrusted.
* Added `IServiceCollection.AddAtProtoHttpClient()`, which registers the named `HttpClient` an agent constructed with an `IHttpClientFactory` resolves,
  configured with the same SSRF protections, proxy and certificate revocation settings an agent applies when it builds its own `HttpClient`. Supplying
  an `IHttpClientFactory` to an agent without this registration produces a client with none of those protections.
* Added an `AtProtoJetstream` constructor which takes an `IHttpClientFactory`, so a jetstream can share an application's connection pool rather than
  building a service provider and a `SocketsHttpHandler` of its own.
* Added `AtProtoJetstreamBuilder.WithHttpClientFactory()` and `AtProtoJetstreamBuilder.ConfigureHttpClientOptions()`, so a jetstream built through the
  builder can use an application's `IHttpClientFactory`, or configure the `HttpClient` it builds for itself.
* Added `AtProtoJetstreamBuilder.WithWebSocketOptions()` and `AtProtoJetstreamBuilder.SetMaximumTotalMessageSize()`. The `WebSocketOptions` the jetstream
  constructor accepts, which carry the WebSocket proxy and keep-alive interval, and the maximum total message size, could not be expressed through the
  builder at all, so a jetstream created by the builder always used the defaults for both.
* Added `WebSocketOptions.KeepAliveTimeout`, which bounds how long a WebSocket waits for a keep-alive ping to be answered before the connection is
  considered dead. A jetstream now defaults both `KeepAliveInterval` and `KeepAliveTimeout` to 30 seconds, so a peer which stops responding without
  closing its socket is noticed rather than leaving the jetstream reading forever. `KeepAliveTimeout` is ignored on .NET 8, whose `ClientWebSocketOptions`
  has no keep-alive timeout.
* Added `JetstreamOptions.MaximumConcurrentMessageParsers` and `AtProtoJetstreamBuilder.SetMaximumConcurrentMessageParsers()`, which bound how many
  messages a jetstream parses at once. Reading pauses when the limit is reached, so a slow event handler applies back pressure to the server rather
  than growing an unbounded number of in-flight parses. The default is 64.
* Added `WebSocketMessageAbandonedException`, thrown when a WebSocket message is abandoned because it exceeded the maximum message size or arrived as
  too many consecutive empty fragments.
* Added `Agent.HasCredentials` which returns a flag indicating whether the agent has access credentials, regardless of whether the credentials are expired.
* Added `AtProtoAgent.CredentialsUpdatedAsync`, an awaitable counterpart to the `CredentialsUpdated` event, of type
  `Func<CredentialsUpdatedEventArgs, CancellationToken, Task>?`. Unlike the event, the agent awaits this callback before continuing, so asynchronous
  credential persistence completes before the updated credentials are used, and any exceptions it throws surface to the caller rather than being
  silently swallowed. Code which persists credentials should move from the `CredentialsUpdated` event to `CredentialsUpdatedAsync`.
* Added `JetstreamOptions.SendTimeout` and `AtProtoJetstreamBuilder.SetSendTimeout()`, which bound how long a jetstream waits to send a message to the
  server. The default is 30 seconds.

#### idunno.AtProto.OAuthCallback

* Added `CallbackServer.ContentSecurityPolicy`, which sets the `Content-Security-Policy` header sent with every response. It defaults to a policy which
  allows only what the built in page needs.
* Added `CallbackServer.FailureTitle` and `CallbackServer.FailureBody`, the page rendered when a callback arrives without an authorization code.

#### idunno.AtProto.Types

* Added `Cid.TryParse(string, out Cid?)` which attempts to parse a string into a `Cid` instance, returning a boolean indicating success or failure.

#### idunno.Bluesky

* Added `BlueskyAgent.UpdateProfile(Profile, Cid?, CancellationToken)` which allows updating a user's profile with an optional `Cid` parameter.
  The `Cid` is used to identify the specific version of the profile being updated, ensuring that updates are applied to the correct version and preventing conflicts.
* Added an optional `maxPageSize` parameter to `BaseEmbeddedCardGenerator.GetPageContent()` and the `BaseEmbeddedCardGenerator.DefaultMaximumPageSize`
  constant, which cap the number of bytes read from a page when generating an embedded card. The default is 1MB.
* Added `Maximum.MessageLengthInGraphemes`, `Maximum.ConversationRequestsToList`, `Maximum.JoinRequestsToList`, `Maximum.GroupMembers`,
  `Maximum.GroupNameLengthInBytes` and `Maximum.GroupNameLengthInGraphemes`, replacing the hard coded limits used by the chat endpoints.
* Added `readState`, `status`, `kind` and `lockStatus` filter parameters to `BlueskyAgent.ListConversations()` and `BlueskyServer.ListConversations()`,
  exposing the filters `chat.bsky.convo.listConvos` supports.
* Added `Chat.ConversationReadState`, and `Chat.ConversationKind.Direct` and `Chat.ConversationKind.Group`, holding the known values for the new
  `readState` and `kind` filters.
* Added `Maximum.ReactionLengthInBytes`.
* Added `Maximum.JoinLinkPreviewCodes`, which `GetJoinGroupLinkPreviews()` now validates against in place of a hardcoded `50` in each of its two
  overloads.

#### idunno.Bluesky.AspNet.Authentication

* Add a new package, `idunno.Bluesky.AspNet.Authentication`, which provides ASP.NET Core authentication support for Bluesky. It includes an `AuthenticationHandler` which can be registered with
  `AuthenticationBuilder.AddBluesky()`, and a `ProfileClaimsTransformer` which can be registered with `IServiceCollection.AddProfileClaimsTransformer()`.
  The handler and transformer work together to authenticate users via Bluesky, and to transform their profile into claims for use in the application.
* Added `BlueskyClaimsTransformerOptions.VerifyHandle`, which controls whether the handle a profile carries is verified against the directory before it
  becomes a claim. It defaults to `true`.
* Added `BlueskyAuthenticationMetrics.HandleVerificationFailures`, a counter of handles which did not resolve back to the DID whose profile returned them.

### Changed

#### idunno.AtProto

* `AtProtoJetstream` no longer builds a `ServiceCollection`, a `ServiceProvider` and a `SocketsHttpHandler` of its own when an `IHttpClientFactory` is
  supplied, and now shares the single definition of the SSRF protected handler an agent uses rather than carrying a second copy of it.
* `OAuthClient.OpenBrowser()` now throws an `ArgumentException` for a relative uri, or one whose scheme is not `http` or `https`, rather than handing it
  to the platform shell and launching whichever handler is registered for it.
* `AtProtoAgent.CreateOAuthClient()` is now `public virtual`, so the transport an agent uses for OAuth can be replaced.
* `AtProtoAgent.RefreshCredentials()` now throws `SecurityTokenValidationException` when the authorization server issues a token for an account other
  than the one being refreshed.

#### idunno.AtProto.OAuthCallback

* `CallbackServer` now sends a `Content-Security-Policy` header with every response. A caller whose `SuccessBody`, `FailureBody` or `ResponseStyleSheet`
  loads anything beyond inline CSS and data URI images has to widen `CallbackServer.ContentSecurityPolicy` to match.
* `CallbackServer.WaitForCallbackAsync()` now throws `ObjectDisposedException` when disposal runs between its disposal check and taking its lock,
  rather than returning a task cancelled with a token which was never cancelled.
* The documentation for `CallbackServer.SuccessTitle` and `CallbackServer.ResponseStyleSheet` now says that the value is written into the `head` of the
  page verbatim, so it has to carry its own `title` or `style` element.

#### idunno.Bluesky

* `DefaultFacetExtractor` now uses a more permissive regex for URLs, which allows for query strings to be included in the extracted URL.

### Breaking Changes

#### idunno.AtProto.Types

* `AtUri` now reports an invalid record key segment as an `AtUriFormatException` rather than a `RecordKeyFormatException`, so that every way an AT URI can
  be malformed is reported the same way. Code which catches `RecordKeyFormatException` around `new AtUri(string)` should catch `AtUriFormatException`
  instead. `AtUri.TryParse()` is unaffected. `RecordKey` itself still raises `RecordKeyFormatException` for callers parsing a record key in isolation.
* `RecordKey.Value` is now read only. Previously it had an `init` accessor, which allowed object initializer syntax to replace a validated record key
  with an arbitrary string, for example `new RecordKey("self") { Value = "../../../etc/passwd" }`.

#### idunno.AtProto

* `AtProtoRecord` and its derived record types now compare `ExtensionData` by its contents rather than by reference, so two records carrying the same extension data are now equal.
* `SelfLabels` now has value equality, comparing its labels by their contents rather than by reference.
* Renamed `AtProtoJetstreamBuilder.MaximumMessageSize` to `ReadBufferSize` and `AtProtoJetstreamBuilder.SetMaximumMessageSize()` to `SetReadBufferSize()`, as they configure the size of each block read from the web socket rather than a limit on a message. Use `SetMaximumTotalMessageSize()` to limit how large a message may be.
* `AtProtoJetstream.ConnectAsync()` now throws `WebSocketException` when a connection cannot be made, instead of returning normally.
* `AtProtoJetstreamBuilder.WithCompressionDictionary()`, `WithTaskFactory()` and the `FilterTo()` overloads now throw `ArgumentNullException` when passed `null`.
* A cancelled request now throws `OperationCanceledException` instead of returning a result with a status code of `OK`.
* Removed the `ReaderWriterLockSlim` property in `Credentials` to avoid potential deadlocks. Any custom credentials using this property should now use its own locking mechanism to avoid deadlocks.
* `DPoPRevokeCredentials` no longer implement `IDisposable`.
* The `onCredentialsUpdated` parameter on `AtProtoServer` and `AtProtoHttpClient` methods has changed from `Action<AtProtoCredential>?` to
  `Func<AtProtoCredential, CancellationToken, Task>?`, and the callback is now awaited. Previously the callback was invoked synchronously, which meant
  asynchronous credential persistence could not be awaited, and any work it started could be abandoned. Callers passing a lambda should change
  `credential => Save(credential)` to `(credential, cancellationToken) => SaveAsync(credential, cancellationToken)`.
* `Label.Signature` is now `IEnumerable<byte>?` rather than `IEnumerable<byte>`. The `sig` property is optional in
  `com.atproto.label.defs`, so most labels carry no signature. Previously an absent `sig` left the non-nullable property `null`,
  and code following the annotation would fail with a `NullReferenceException`. Check `Signature` for `null` before enumerating it.
* `AtJetstreamCommit.Record` is now `JsonElement?` rather than `JsonDocument?`. A `JsonDocument` owns memory rented from the array pool and has to be
  disposed, but a record handed to an event handler has no owner to dispose it, so every commit leaked the buffer it had rented. A `JsonElement` owns
  nothing. Replace `Record.RootElement` with `Record.Value`, and remove any `using` or `Dispose()` around the record.
* The `idunno.atproto.jetstream.total_connections_failed` counter has been renamed to `idunno.atproto.jetstream.total.connections_failed`, matching the
  documented name and every other counter the jetstream publishes. Dashboards and alerts using the old name need updating.

#### idunno.Bluesky

* `Post`, `EmbeddedImages`, `EmbeddedGallery`, `EmbeddedVideo` and `Embed.External.Properties` now compare their collection properties by contents rather than by reference. `Post` equality still includes `CreatedAt`, so two posts created at different times are never equal.
* `EmbeddedImages` now copies the collection it is constructed from instead of holding on to the caller's collection.
* The `imageMimeType` parameter on `BaseEmbeddedCardGenerator.DownloadAndUploadImageBlob()` is now only a hint; the type recorded on the uploaded blob is always the sniffed one.

* The `Maximum` constants which carry a lexicon `maxLength` have been renamed to end in `InBytes`, because a lexicon `maxLength` is counted in UTF-8
  bytes rather than in characters. `PostLengthInCharacters`, `TagLengthInCharacters`, `MessageLengthInCharacters` and `DraftTextLengthInCharacters`
  become `PostLengthInBytes`, `TagLengthInBytes`, `MessageLengthInBytes` and `DraftTextLengthInBytes`, and `DisplayNameLength`, `DescriptionLength`
  and `PronounLength` become `DisplayNameLengthInBytes`, `DescriptionLengthInBytes` and `PronounLengthInBytes`. Their values are unchanged.
* Post text, tags, direct messages, group names, draft text, profile display names, descriptions and pronouns are now measured in UTF-8 bytes when
  validated against those limits, rather than with `string.Length`. A UTF-8 byte count is always greater than or equal to the number of UTF-16
  characters in the same string, so the previous check was too permissive and never too strict: text which the PDS would reject could pass local
  validation and fail at the server instead. Text which was accepted before and is within the real limit is still accepted. Non-ASCII text close to a
  limit may now be rejected locally, which is the limit the server was always applying.
* `RichText.LinkFacetFeature.Uri` has been changed from a `Uri` type to a `string` type as the Bluesky web app can create facets with illegal URIs. The constructor has also been updated to accept a `string` instead of a `Uri`.
* `Embed.External.Properties.Uri` has been changed from a `Uri` type to a `string` type as the Bluesky web app can create facets with illegal URIs. The constructor has also been updated to accept a `string` instead of a `Uri`.
* `Embed.EmbeddedExternal` has been updated to only accept a `string` for its `uri` constructor parameter instead of a `Uri` type, as the Bluesky web app can create facets with illegal URIs.
* `Embed.EmbeddedExternal` constructors have been simplifed to a single constructor, which has defaults for all optional parameters.
* The `onCredentialsUpdated` parameter on `BlueskyServer` methods has changed from `Action<AtProtoCredential>?` to
  `Func<AtProtoCredential, CancellationToken, Task>?`, mirroring the change in `idunno.AtProto`.
* `BlueskyAgent.ListJoinGroupRequests()` and `BlueskyServer.ListJoinGroupRequests()` now take a single `conversationId` and an optional `limit`, and
  return a `PagedViewReadOnlyCollection<Chat.Group.JoinRequestView>` rather than a `PagedViewReadOnlyCollection<Chat.Group.JoinRequestConversationView>`.
  The `chat.bsky.group.listJoinRequests` endpoint takes one required `convoId` and returns `joinRequestView`, so the previous signature could not be
  called successfully.
* `BlueskyServer.ListConversations()` now takes `readState`, `status`, `kind` and `lockStatus` parameters between `cursor` and `service`. Callers
  passing the `service`, `accessCredentials` and `httpClient` arguments positionally will need to update. `BlueskyAgent.ListConversations()` takes the
  same four filters as optional parameters, so only callers passing its `cancellationToken` positionally are affected.
* The properties on `Chat.Group.JoinRequestView` and `Chat.Group.JoinRequestConversationView`, and the `Facets`, `Embed` and `ReplyTo` properties on
  `Chat.MessageInput`, are now `init` only. They are populated when the object is created and describe a message which has been sent or a request which
  has been made.
* `GetNotificationUnreadCount()` on `BlueskyAgent` and `BlueskyServer` now returns an `AtProtoHttpResult<int?>` rather than an `AtProtoHttpResult<int>`,
  and `UpdateAllRead()` on both now returns an `AtProtoHttpResult<ulong?>` rather than an `AtProtoHttpResult<ulong>`. `AtProtoHttpResult<T>.Succeeded`
  requires `Result` to be non-null, which a non-nullable value type can never be, so these methods reported a failed call as a success carrying a
  meaningless count. Callers reading `Result` will need to handle a `null`, which now unambiguously means the call failed.

### Fixed

#### idunno.AtProto

* A jetstream event whose `kind` is one this library does not model is now surfaced as `JetStreamEventKind.Unknown` rather than failing to deserialize.
  The jetstream server decides which kinds it emits, so a kind added upstream previously made every message carrying it unparsable and dropped.
* `AtJetstreamEvent.DateTimeOffset` now clamps a `time_us` outside the range a `DateTimeOffset` can hold to `DateTimeOffset.MinValue` or
  `DateTimeOffset.MaxValue`, rather than throwing `ArgumentOutOfRangeException` out of a property getter on remote input.
* Copying an `AtJetstreamEvent` with `with { TimeStamp = ... }` now reports the new timestamp. A cached value was copied by the record copy
  constructor, so a copy whose original had already been read reported the original's time.
* The jetstream connection counters are no longer tagged with the subscription uri, which names every DID and collection being watched and the cursor.
  The tag now carries only the server, so who is being watched is not published to whatever collects the metrics, and the tag has a bounded set of values.
* A jetstream whose `TaskFactory` refuses to start a message parser now gives back the parse slot it took. Previously enough such failures left the
  receive loop waiting forever for a slot with the socket still open, reading nothing and reporting nothing.
* An exception thrown by a `RecordReceived` handler is no longer counted as a message parsing failure or logged as an unparsable server message.
* The jetstream now bounds how long it waits to send a close reply or an options update, so an unresponsive server cannot leave it blocked indefinitely
  holding the send lock.
* The `ZstdSharp.Decompressor` a jetstream shares across messages is now used under a lock, as it is not thread safe and messages are parsed concurrently.
* A handle whose `_atproto` DNS record set carries more than one, conflicting, `did=` text record is now treated as unresolvable, and the conflict is
  logged as an error. Resolution previously fell back to `/.well-known/atproto-did`, which let the host the handle points at choose which of the
  conflicting records won. Identical, repeated, records are still resolved.
* A rotated DPoP nonce is now applied to the credentials it belongs to whether or not a credentials updated callback was supplied, so calls made directly through `AtProtoServer` and `BlueskyServer` no longer fail once a server rotates its nonce.
* A failed or throwing call to `AtProtoAgent.RefreshCredentials()` no longer leaves the background refresh timer stopped for the lifetime of the agent.
* A refresh token the server has already exchanged is now remembered even when the credentials it issued cannot be validated, so it is not presented a second time.
* A refresh token which was exchanged by an attempt that never completed is no longer reported as a successful refresh, so the background refresh keeps retrying instead of leaving the agent on credentials it never refreshed.
* `TokenRefreshFailed` is now raised when an OAuth refresh fails, and when the access token a refresh issues cannot be validated.
* `TokenRefreshFailed` is now raised outside the credential refresh semaphore, so a handler which refreshes the agent no longer deadlocks it.
* An access token which is already close to expiry when the refresh timer starts is now refreshed through the timer rather than inline, which stops a server issuing short lived tokens driving an unbounded chain of immediate refreshes.
* `AtProtoAgent.Logout()` now reads the agent credentials once, so a concurrent logout or refresh cannot leave it revoking one credential having checked another.
* Setting `AtProtoAgent.Credentials` on a disposed agent now throws `ObjectDisposedException` rather than silently discarding the credential.
* `AtProtoAgent.RefreshCredentials()` now reads the agent credentials once, so a refresh which runs concurrently cannot leave it refreshing a credential it did not check.
* `AccessCredentials.ExpiresOn` and `AccessCredentials.Did` are now read under the lock their values are written under.
* Setting `AccessTokenCredential.AccessJwt` now updates `Did` and `ExpiresOn` from the new token, rather than leaving them describing the token it replaced.
* An OAuth login response is now processed with the proof key of the login it belongs to, rather than with a cached client left over from an earlier
  login, which bound the issued token to a key the returned credentials did not carry.
* The state of a login in progress is now discarded however processing its response ends, rather than only when the authorization server returned an
  error, so a spent PKCE code verifier and a private proof key are no longer readable through `OAuthClient.State` after a login completes or fails.
* `OAuthClient.BuildOAuth2LogoutUri()` no longer sends the access token as an `id_token_hint`, so a live token does not reach the browser address bar,
  history, or the `Referer` of anything the logout page goes on to request.
* `OAuthClient.BuildOAuth2LogoutUri()` no longer overwrites the proof key and authority of a login in progress, and no longer writes them outside the
  lock the rest of the login state is published under.
* An OAuth refresh which is answered with a token for a different account is now rejected rather than silently re-pointing the agent at that account.
* `AccessTokenCredential` and `DPoPAccessCredentials` now report a value which cannot be parsed as a token, or which carries a subject which is not a
  DID, as an `ArgumentException` naming the parameter it came from.
* Setting `AccessJwt` on `AccessCredentials`, `AccessTokenCredential` and `ServiceCredential` now extracts the new token's identity and expiry before publishing any of them, so a token which cannot be read leaves the credential untouched instead of pairing a new token with an old identity.
* `ServiceCredential.ExpiresOn` and `ServiceCredential.Did`, and `AccessTokenCredential.ExpiresOn` and `AccessTokenCredential.Did`, are now read under the lock their values are written under.
* A service token carrying no audience is now rejected with `ArgumentException` rather than `InvalidOperationException`.
* `AtProtoCredential.TryCreate()` now rejects a `ClaimsIdentity` whose `did` claim disagrees with the subject of its access token, and requires the service to be an absolute http or https `Uri`.
* An empty or whitespace `DPoP-Nonce` response header is no longer treated as a nonce to rotate to, which could throw whilst handling a response or discard a working nonce.
* `DPoPRefreshCredential` and `DPoPRevokeCredentials` no longer build a DPoP proof token factory on every request.
* `AccessCredentials.ExtractJwtProperties()` is no longer exposed as a protected member.
* `AtProtoHttpClient` no longer disposes the HTTP handler it shares between requests, which left every request after the first on an instance throwing `ObjectDisposedException`.
* Request headers supplied to an individual `AtProtoHttpClient` call are now sent with that request. Previously only headers configured on the client were sent, which silently dropped the headers passed to methods such as `BlueskyAgent.GetFeed()`.
* A header collection supplied to an `AtProtoHttpClient` call is no longer modified, so a collection reused across calls no longer accumulates the headers configured on the client.
* A header supplied for an individual call now replaces the one configured on the client rather than both being sent.
* A failed `Login()` no longer leaves background token refresh stopped for the session it did not replace. A login which could not resolve a PDS, or whose
  issued access token could not be validated, left the agent holding its existing credentials with nothing left to refresh them, and the session then expired.
* A failed `Logout()` no longer leaves background token refresh stopped for the session it did not end.
* An OAuth `Logout()` whose token revocation fails now discards the agent credentials, as a username and password logout already did. The agent previously
  went on reporting itself as authenticated, and refreshing, against a session the caller had asked it to end.
* Disposing an agent now clears the credentials it is holding, so a live access token and refresh token are not left reachable through a disposed agent.
* `AtProtoAgent` no longer throws `ObjectDisposedException` from its credential refresh semaphore when it is disposed during a refresh.
* `DPoPAccessCredentials` now reuses its DPoP proof token factory instead of importing the proof key on every request.
* `OAuthClient` now reuses its DPoP proof token factory when refreshing credentials instead of importing the proof key on every request it makes.
* `OAuthClient` now rejects an access token whose `sub` is missing or is not a DID, rather than letting it escape as an `ArgumentException` when the credential is built.
* `OAuthClient` now rejects a login whose token response is not of type `DPoP`, so a token which is not bound to the proof key is no longer stored as though it were.
* Restoring `OAuthClient.State` now rejects an expected authority or expected service which is not an absolute http or https uri. The expected authority is what an issued token's `iss` is checked against, so tampered state can no longer widen that check.
* `OAuthClient` no longer replaces the proof key, expected authority and expected service of a login in progress when a subsequent call to `BuildOAuth2LoginUri()` is rejected or fails to prepare.
* A failed login now clears all of the `OAuthClient` login state rather than just the proof key, so the next call reports that there is no login in progress instead of a missing proof key.
* `OAuthClient` now enumerates the scopes it is passed once, so a sequence which can only be enumerated once is handled correctly.
* `OAuthClient` now logs a warning for each requested scope the authorization server did not grant.
* `AtProtoJetstream` now reads each connection on the socket it was opened for, so reconnecting no longer leaves the previous receive loop reading the new connection alongside the new one and tearing messages in half between them.
* `AtProtoJetstream` now raises `ConnectionStateChanged` when a connection is lost without a close handshake, rather than ending its receive loop silently.
* `AtProtoJetstream` now serialises writes to the underlying WebSocket, which allows only one at a time.
* `AtProtoJetstream.CloseAsync()` now works against a single socket, so a concurrent reconnection cannot leave it aborting one socket having inspected another.
* `AtProtoJetstream.MessageLastReceived` and `AtProtoJetstream.DisconnectedGracefully` are now published to the threads which read them.
* Trimming and AOT suppressions which never reached the IL trimmer have been corrected, so `idunno.AtProto` and `idunno.Bluesky` now trim and publish as native AOT without warnings.
* `AtProtoJetstream` connection state change events are now raised outside the lock which guards its filters and outside the semaphore which serialises connections, so a handler which sets a filter or reconnects no longer deadlocks.
* `AtProtoJetstream.CloseAsync()` now abandons the close handshake once `JetstreamOptions.CloseTimeout` expires, instead of waiting indefinitely for a server which never answers it.
* `AtProtoJetstream` now truncates a message to 1024 characters before logging it, instead of writing the whole of a remote message to the log.
* `AtProtoJetstream` no longer throws when an event names a payload it does not carry.
* `AtProtoJetstream` no longer counts a single connection failure twice in its `total_connections_failed` metric.
* `AtProtoJetstream` no longer throws `ObjectDisposedException` from its connection semaphore when it is disposed during a connection attempt.
* `AtProtoJetstream` now replaces its WebSocket whenever the previous one has been connected, rather than only when it reached `Aborted` or `Closed`. A close which did not complete left a socket in `CloseSent` or `CloseReceived`, which was then reused, and a `ClientWebSocket` can only be connected once, so every subsequent reconnection failed for the lifetime of the jetstream.
* `AtProtoJetstream.CloseAsync()` now aborts the socket when the close handshake is cancelled or throws, rather than leaving a half closed socket behind.
* `AtProtoJetstream` now closes the connection when a message is abandoned because it exceeded the maximum message size or arrived as too many consecutive empty fragments. The remainder of the abandoned message is still queued on the socket and cannot be skipped, so reading on returned its tail as though it were a message of its own.
* `AtProtoJetstream` now resets `DisconnectedGracefully` when it connects, so a new connection no longer reports how the connection before it ended.
* The `AtProtoJetstreamBuilder` properties which correspond to a `Set` or `With` method now apply the same validation as the method, rather than accepting `null` or an out of range value which then failed, or silently misbehaved, when the jetstream was built.
* An `HttpContent` request body supplied by the caller is no longer disposed by the client, and is buffered so that it survives a DPoP nonce retry.
* A failed background credential refresh now schedules a retry even when the refresh timer had not been created yet, which previously ended background refresh for the lifetime of the agent.
* The refresh token replay check now remembers the last few exchanged tokens rather than only the most recent one.
* Exchanged refresh tokens are now forgotten when the session ends, so they are not retained for the lifetime of the agent.
* `DPoPAccessCredentials` and `DPoPRefreshCredential` now read their token once when signing a request, so the DPoP proof and the authorization header always carry the same token.
* `DPoPRefreshCredential` now stores an absent DPoP nonce as an empty string rather than leaving the non-nullable `DPoPNonce` property returning `null`.
* `DPoPRevokeCredentials` now throws an `ArgumentNullException` rather than a `NullReferenceException` when constructed with `null`.
* OAuth access token validation now matches `atproto` as a discrete entry in the `scope` claim rather than as a substring.
* OAuth access token validation now throws an `OAuthException` rather than an `ArgumentException` when the token has no `scope` claim.
* OAuth access token validation no longer throws an `ArgumentOutOfRangeException` in time zones east of UTC when the token has no `nbf` or `exp` claim.
* OAuth access token lifetimes are now validated with a clock skew allowance, configurable through the new `OAuthOptions.ClockSkew` property.
* The log message written when an OAuth access token has an unexpected issuer no longer reports the actual and expected authorities the wrong way around.
* `OAuthLoginState.GetHashCode()` now derives the hash code from the contents of `ExtraProperties`, matching `Equals()`.
* `did:web` DIDs are now resolved only when they meet the restrictions AT Protocol places on the method.
* `ResolvePds()` now rejects an `#atproto_pds` service endpoint which is neither `https`, nor `http` to a loopback address.
* The background credential refresh timer no longer throws an `ArgumentException` when an access token expires in exactly sixty seconds. The refresh
  interval subtracts a minute from the expiry, which at exactly sixty seconds left an interval of zero, a value `System.Timers.Timer` rejects.
* `Label`, `SelfLabel` and `CreateModerationReport()` now measure their lexicon `maxLength` limits in UTF-8 bytes rather than in UTF-16 characters,
  matching `com.atproto.label.defs` and `com.atproto.moderation.createReport`. A UTF-8 byte count is never smaller than the UTF-16 character count, so
  the previous checks were too permissive for non-ASCII values and accepted text the server rejects.
* `CreateModerationReport()` now also rejects a `reason` longer than 2,000 graphemes, the `maxGraphemes` limit in
  `com.atproto.moderation.createReport`, which was never checked.
* `AtProtoAgent.Logout()` now revokes OAuth credentials rather than always failing. The revocation credentials rejected the empty DPoP nonce every
  revocation necessarily starts with, so an `ArgumentException` was thrown before any request was made to the authorization server, leaving access and
  refresh tokens live until they expired.
* The `AtProtoAgent` constructors which take an `IHttpClientFactory` now keep the `AtProtoAgentOptions` they are given. Previously the options were
  discarded, so an agent created through dependency injection behaved as if no options had been supplied. Amongst other things this left `OAuthOptions`
  unset, which made logging out of an OAuth session throw.
* `AtProtoJetstreamBuilder.Build()` now passes the `IMeterFactory` given to `WithMeterFactory()` to the jetstream it builds. Previously it was
  discarded, so jetstream metrics were published through the shared static meter instead of the application's meter factory.
* A jetstream built by `AtProtoJetstreamBuilder` now uses compression by default, matching the default on `JetstreamOptions`. Previously building a
  jetstream, rather than constructing one, silently turned compression off unless `UseCompression(true)` was called.
* The response body is now buffered, up to `MaximumResponseSize` bytes, before an `OnResponseReceived` handler is called. Requests are made with
  `HttpCompletionOption.ResponseHeadersRead`, so the handler was previously given an unbuffered network stream and a handler which read it allocated
  whatever the service chose to send, bypassing `MaximumResponseSize` entirely. A response larger than the limit is now rejected as `ResponseTooLarge`
  without the handler being called. Responses are not buffered when no handler has been attached.
* `AtProtoHttpClient.OnSendingRequest` and `AtProtoHttpClient.OnResponseReceived` can now be set. Both were documented as settable but only had a
  getter, so neither handler could be attached through the non-generic client at all. Setting either to `null` now throws an `ArgumentNullException`
  rather than leaving the client in a state where sending a request would throw a `NullReferenceException`.
* Reading a response body no longer throws from `ArrayPool` when `MaximumResponseSize` is set above 1GB. The read buffer was doubled in `int`
  arithmetic, which overflowed to a negative length rather than stopping at `MaximumResponseSize`, so an over-large response threw instead of being
  reported as `ResponseTooLarge`.
* A jetstream now limits how far a compressed message is allowed to expand. `JetstreamOptions.MaxMessageSize` was only applied to the compressed frame
  as it arrived, and decompression was left at its own 2GB default, so a small frame could declare and expand to hundreds of megabytes. A 24KB frame
  expanding to 768MB was enough to exhaust a client. The limit is now applied to the decompressed message, and a message which exceeds it is dropped in
  the same way as one which cannot be decompressed.
* A jetstream configured with `UseCompression` set to `false` now delivers the messages it receives. The receive loop copied each message out of an
  empty buffer rather than out of the message it had just read, so every uncompressed message arrived as a run of zero bytes and failed to parse,
  and a message larger than `JetstreamOptions.BufferSize` threw instead.
* A jetstream now sends the maximum message size to the server under the name the server expects. The `maximumMessageSizeBytes` query string parameter
  does not exist, the parameter is `maxMessageSizeBytes`, so the server applied no limit at all. The value sent is now `MaxMessageSize` rather than
  `BufferSize`, which is the size of the blocks a message is read in rather than a limit on the message itself.
* Reading a message from a web socket now gives up on a message made up of empty fragments. An empty fragment adds nothing to the message being
  assembled, so the maximum message size could never bring the read to an end, and a peer which sent them endlessly held the read loop for as long as
  it cared to. A message which closes part way through is now abandoned as well, rather than being handed to the caller as if it had ended there.
* `AtProtoJetstream.DisconnectedGracefully` is now `true` when the server closed the connection and the jetstream completed the close by replying to
  it. Previously it was set to `false`, which is the value for a connection which was dropped, so the two could not be told apart.
* `AtProtoJetstream.ConnectAsync()` now serialises connection attempts. Callers which arrived together both reached the underlying web socket's
  connect, where the second threw, and could each start a receive loop reading the same socket. A caller which arrives whilst a connection attempt is
  in progress now waits for it, and returns without doing anything if it left the jetstream connected.
* `AtProtoJetstream.DidFilter` and `AtProtoJetstream.CollectionFilter` now throw `ArgumentNullException` when set to `null`, are updated under a lock,
  and are copied under that lock when the filters are sent to the server, so the server is sent a consistent pair of filters rather than one from
  before a concurrent update and one from after it.
* `AtProtoServer.ApplyWrites()` now throws an `ArgumentException` when an operation is not a `CreateOperation`, `UpdateOperation` or `DeleteOperation`, or when its record value cannot be serialized, rather than silently dropping the operation from the batch.
* `ListRecords()` now skips and logs a record it cannot deserialize, rather than failing the entire page, and no longer throws a `NullReferenceException` when the service returns a null `records` collection or a null record within it.
* `UploadBlob()` now validates the mime type with `MediaTypeHeaderValue.TryParse()` and sends the parsed value, rejecting mime types containing control characters and accepting well formed values with surrounding whitespace which previously threw a `FormatException`.
* The `PutRecord()` overload which takes an `AtProtoRepositoryRecord<TRecord>` and a `JsonSerializerOptions` now sends the record's `Cid` as `swapRecord` rather than as `swapCommit`. A record CID never matches a commit CID, so the compare-and-swap that overload performed could never succeed.
* `ListRecords()` now calls `com.atproto.repo.listRecords`, matching the casing in the lexicon.
* The exception thrown when `limit` is out of range now reports the supplied value rather than the literal text `{limit}`.
* Responses are now deserialized with `RespectNullableAnnotations` enabled, so a property declared non-nullable which the service sends as `null` fails deserialization rather than leaving a `null` in a non-nullable member.
* `ApplyWrites()` no longer throws a `NullReferenceException` when the service omits the `results` collection or sends it as `null`.
* `UploadBlob()` and `DescribeRepo()` now use the same `JsonSerializerOptions` as every other endpoint, rather than the source generation context's own options.
* `QueryLabels()` now skips and logs a `null` entry inside an otherwise well formed `labels` collection rather than handing that `null` to the caller. Neither
  `JsonRequired` nor `RespectNullableAnnotations` applies to a collection's element type.

#### idunno.AtProto.OAuthCallback

* `CallbackServer` now binds Kestrel to the loopback adapter explicitly and ignores ambient configuration. `WebApplication.CreateBuilder()` reads
  `appsettings.json` from the current working directory, environment variables and the command line, and a `Kestrel:Endpoints` section found there
  replaced the loopback address the server configured, silently moving the server which receives OAuth authorization codes onto an externally
  reachable interface whilst `CallbackServer.Uri` continued to report loopback. Host filtering did not contain this, as it only inspects the `Host`
  header, which any client can set to `127.0.0.1`.
* `CallbackServer.WaitForCallbackAsync()` now completes when the supplied `CancellationToken` is cancelled. Previously the timeout was implemented by
  awaiting `Task.Delay(timeout, cancellationToken)`, which throws when the token is cancelled, so the code which cancelled the returned task was never
  reached and the caller waited forever.
* `CallbackServer.WaitForCallbackAsync()` no longer completes whilst the response to the callback is still being written, and no longer runs the
  waiting caller's continuations on the request thread. A caller which did any blocking work when its callback arrived stalled the page the browser was
  waiting on.
* `CallbackServer` now surfaces a failure to start, rather than discarding the task returned by `RunAsync()`. If the port was taken between
  `GetRandomUnusedPort()` returning it and the server binding to it the instance appeared to construct correctly but was dead, and callers waited for a
  callback which could never arrive.
* Disposing a `CallbackServer` now completes any pending `WaitForCallbackAsync()` task with an `ObjectDisposedException` instead of leaving it pending
  forever, and calling `WaitForCallbackAsync()` on a disposed server now throws `ObjectDisposedException`.
* `CallbackServer` now validates its `port` and `timeoutInSeconds` arguments. A `timeoutInSeconds` large enough to overflow the millisecond conversion
  previously threw on a background thread where the exception could not be observed.
* Repeated calls to `CallbackServer.WaitForCallbackAsync()` no longer start an additional timer for each call.
* `CallbackServer` now sends `Referrer-Policy: no-referrer` and `Cache-Control: no-store` with every response, keeping the authorization code in the callback URL out of `Referer` headers and browser caches.
* `CallbackServer` responses now declare `charset=utf-8` rather than leaving the encoding to the browser.
* Repeated calls to `CallbackServer.WaitForCallbackAsync()` no longer leak a `CancellationTokenSource` when the supplied `CancellationToken` was already cancelled.
* Disposing a `CallbackServer` without ever calling `WaitForCallbackAsync()` no longer raises a `TaskScheduler.UnobservedTaskException`.
* `CallbackServer` now listens on, and accepts requests addressed to, the IPv6 loopback address and `localhost` as well as `127.0.0.1`.
* `CallbackServer` now only treats a request as the callback when it carries a `code`, `state` or `error` query string parameter and, where the browser
  says so through `Sec-Fetch-Dest`, is a top level navigation. The callback is single shot, so any page the user happened to be visiting during a login
  could previously consume it with a cross site request to the loopback address, leaving the redirect which actually carried the authorization code to
  arrive after the wait had already finished. A request with no query string is now rejected rather than answered with the success page.
* `CallbackServer` now rejects a `path` containing a character which does not mean the same thing in a route pattern and in a URI. `{`, `}` and `*` were
  read as a route parameter or a catch all, and `?`, `#` or a space made `CallbackServer.Uri` describe something other than the route which was mapped.
* `CallbackServer` now answers every method other than `GET` with `405 Method Not Allowed`, as its documentation said. Only `POST` did so previously, and
  `PUT`, `DELETE`, `PATCH` and `HEAD` fell through to a `400 Bad Request`.
* `CallbackServer.GetRandomUnusedPort()` now returns a port which is free on both loopback families. It only checked IPv4, but the server binds both, so a
  port already taken on IPv6 stopped the server from starting.
* `CallbackServer` now sends `X-Content-Type-Options: nosniff` with every response.
* `CallbackServer` no longer throws an `InvalidOperationException` over the original failure when writing the callback response fails after the response
  has started.
* `CallbackServer` now renders a failure page, rather than the success page, when a callback arrives without an authorization code. A user who denied
  consent was previously told the login had completed.
* `CallbackServer` now rejects a `path` which a `Uri` resolves away, such as one containing a `.` or `..` segment. `CallbackServer.Uri` described a
  different address from the route which was mapped, so the redirect arrived to find nothing listening for it.
* `CallbackServer` now releases the timeout registration on the caller's `CancellationToken` once a callback has arrived, rather than leaving the server
  rooted to a long lived token until it is disposed.
* `CallbackServer` no longer logs that it is awaiting a callback with a timeout it will not use when `WaitForCallbackAsync()` is called a second time.

#### idunno.AtProto.Types

* `TimestampIdentifier.Next()` no longer returns duplicate identifiers when called concurrently.
* `Nsid.TryParse` now returns `false` for `null`, empty or whitespace input rather than throwing an `ArgumentException` or `ArgumentNullException`.
* `RecordKey` values which are syntactically invalid now throw a `JsonException` when deserialized, rather than allowing a `RecordKeyFormatException` to escape.
* `Did.Method` now returns the correct method for DIDs containing more than three colon separated segments. Previously `did:web:example.com:user:alice`
  and `did:web:localhost:3000` reported a method of `INVALID`.
* `new Handle(null)` now throws an `ArgumentNullException` rather than a `NullReferenceException`. This also applies to the implicit conversion from
  `string` to `Handle`.
* `Cid.ToString()` and `Cid.Value` no longer lower case CIDv0 identifiers. CIDv0 is base58btc encoded, whose alphabet is case sensitive, so case
  normalizing it produced a different identifier which no longer parsed back into an equal `Cid`. The base32 used by CIDv1 is case insensitive and
  continues to be normalized to lower case.
* An invalid `RecordKey` now throws a `RecordKeyFormatException` rather than an `NsidFormatException`.
* `AtUri` no longer validates the collection segment twice. The duplicate check meant a malformed collection could be reported by either of two code
  paths, only one of which produced an `AtUriFormatException`, leaving an `NsidFormatException` able to escape had the first check ever been changed.
* `TimestampIdentifier.Next()` now generates a clock identifier over the full 10 bit range required by the specification, rather than only 5 bits.
* `TimestampIdentifier` now rejects hyphenated identifiers rather than trimming the hyphens and accepting them.
* `Cid` now rejects truncated, over-long and missing multihash byte sequences rather than producing a `Cid` with an empty hash.
* `Cid` now reports the offending version number when a CID version is unsupported.
* `Cid(byte, ulong, byte[])` now validates its arguments and takes a copy of the supplied hash.
* `AtUri` and `Nsid` now check the length of their input before scanning, splitting or matching it.

#### idunno.Bluesky

* `PostBuilder.GetHashCode()` no longer returns a different value on every call for an unchanged builder.
* `PostBuilder.Equals()` now compares builder contents rather than collection references, so two builders holding the same content are now equal. It also takes `DisableReplies` and any embedded video into account.
* `PostBuilder.Append()` and `PostBuilder.WithText()` now measure text against the maximum post length in UTF-8 bytes rather than in UTF-16 characters, matching the constructor and the lexicon. Text which is within the character limit but over the byte limit is now rejected instead of producing a post the server refuses.
* `PostBuilder.ToPost()` no longer modifies the builder it was called on, and the returned `Post` no longer shares an image collection with it.
* The embedded card generators now decide a thumbnail's MIME type by sniffing the downloaded content rather than trusting the type declared by the page.
* The embedded card generators now reject an `og:url` whose scheme is not http or https.
* The embedded card generators now read `/.well-known/site.standard.publication` with a 4KB limit rather than the 1MB page limit.
* The embedded card generators now apply the image download limit before writing to the temporary file rather than after.
* The embedded card generators no longer request a thumbnail whose URI scheme is not http or https.
* The embedded card generators now create their temporary file readable only by the current user on Unix.
* The embedded card generators now decode a page using the character set it declares rather than always assuming UTF-8.
* The embedded card generators now throw `ObjectDisposedException` once disposed rather than continuing to make requests.
* `BlueskyAgent.SendMessage()` now rejects an over-long message before extracting facets from it, rather than after scanning it and resolving every handle it mentions.
* `RichText.DefaultFacetExtractor` now resolves each distinct handle mentioned in a piece of text at most once. A handle mentioned more than once, or
  one which does not resolve, caused a separate network round trip for every time it appeared.
* String length checks for various methods were corrected to work on UTF-8 lengths.
* The embedded card generators no longer buffer an entire page or image into memory before applying their size limits.
* `RichText.DefaultFacetExtractor` no longer extracts a mention from an `@` which is not preceded by the start of the text, whitespace or an opening
  parenthesis. An email address in a post, such as `bob@example.com`, was extracted as a mention of the handle `example.com`, which silently mentioned
  and notified whoever holds that handle. This also means that two mentions which are not separated, such as `@alice.example@bob.example`, are now
  read as a single mention, matching the behaviour documented for Bluesky rich text.
* `RichText.DefaultFacetExtractor` no longer abandons the rest of the text when it skips a tag. A tag which was only punctuation, such as `#!`, or one
  longer than the maximum tag length, discarded every hash tag or cash tag which followed it in the post.
* `RichText.DefaultFacetExtractor` now returns the correct byte range and tag value when a hash tag or cash tag is preceded by whitespace other than a
  space, or by an opening parenthesis. Previously only a space was accounted for, so a tag following a tab or a newline produced a facet whose range
  covered the preceding whitespace and whose value retained its `#` prefix, and `($AAPL)` produced a tag of `($AAPL`.
* `RichText.DefaultFacetExtractor` now extracts a single letter cash tag which starts the post text, and throws an `ArgumentNullException` rather than a
  `NullReferenceException` when the text it is given is `null`.
* The regular expressions used by `RichText.DefaultFacetExtractor` now use a one second match timeout rather than five seconds.
* `TypeResolver.JsonTypeInfoResolvers` now returns a read only list. It previously returned the live list, allowing any caller to change how every
  consumer in the process deserializes AT Protocol and Bluesky types.
* `ListConversationRequests()` now sends the `cursor` it is given to the server. It was accepted and documented, but never placed in the query string, so
  every page after the first repeated the first page and a caller paging until the returned cursor was `null` never terminated.
* `Chat.MessageInput` now rejects text longer than `Maximum.MessageLengthInGraphemes` (1,000) as well as text longer than
  `Maximum.MessageLengthInBytes`, matching the `chat.bsky.convo.defs#messageInput` lexicon and the other text fields in the library.
* `EditGroup()` now limits a group name to `Maximum.GroupNameLengthInBytes` (500) and `Maximum.GroupNameLengthInGraphemes` (50), matching the
  `chat.bsky.group.editGroup` lexicon and `CreateGroup()`. It previously allowed names two and a half times longer than the server accepts.
* `BlueskyAgent.GetJoinGroupLinkPreviews()` now throws `AuthenticationRequiredException` when the agent is not authenticated, matching every other chat
  method on the agent.
* `ListConversations()` now returns the cursor the service sent, rather than always returning `null`. Paging conversations returned the first page
  repeatedly, and a caller paging until the cursor was `null` never terminated. It was the only paged chat endpoint which did not return its cursor.
* `AddReaction()` now rejects a reaction longer than `Maximum.ReactionLengthInBytes` (64) UTF-8 bytes.
* Paged readers no longer throw a `NullReferenceException` or `ArgumentNullException` when the service omits a collection or sends it as `null`. `ListNotifications()`, `SearchActors()`, `GetSuggestions()`, `GetProfiles()`, `GetBookmarks()`, `SearchPostsV2()` and `GetTrendingTopics()` now require their collections to be present and non-null.
* A response which returns `200 OK` but cannot be deserialized is no longer reported as a successful empty page. Fifteen readers returned an empty collection alongside the original status code, which left `Succeeded` returning `true`, hiding the failure and silently reporting no results.
* `ListNotifications()` now skips and logs a null notification within a page, rather than throwing a `NullReferenceException`.
* Paged readers now skip and log a null entry within an otherwise well formed collection, rather than handing the `null` to the caller. Twenty nine readers wrapped the collection returned by the service wholesale, and neither `JsonRequired` nor `RespectNullableAnnotations` applies to the element type of a collection.
* A further fifteen readers which returned `200 OK` with a body which could not be deserialized no longer report the failure as a successful empty result. These readers built their empty result with a constructor call, such as `new Timeline()` or `new Followers(subject: null, followers: [], null)`, so were missed when this was first fixed.
* `GetPreferences()` now reports a response which omits its `preferences` collection as a failure, rather than throwing an `ArgumentNullException`.
* `GetLabelerServices()` now reports a response which omits its `views` collection as a failure, rather than returning an empty collection alongside a successful status code.
* `GetNotificationUnreadCount()` no longer reports a failed call as a success carrying a count of `-1`, and `UpdateAllRead()` no longer reports a failed call as a success carrying a count of `0`. See the breaking changes above.
* Eighteen properties across nine types applied `JsonRequired` with the `field:` attribute target rather than `property:`. The attribute landed on the
  compiler generated backing field, where `System.Text.Json` ignores it, so none of those properties were ever actually required. `ChatStatus`,
  `ReplyReference`, `ListConvoRequestsResponse`, `SearchStarterPacksResponse`, `SearchStarterPacksV2Response`, `TrendingTopic`, `TrendView`,
  `GetPostThreadOtherV2Response` and `GetPostThreadV2Response` now use `property:` and reject a response which omits the property.
* `SearchPosts()`, `SearchActorsTypeahead()`, `GetRelationships()` and `ListJoinRequests()` now report a response which omits its collection as a failure.
  The collection is required by the lexicon, but a missing property is not covered by `RespectNullableAnnotations`, which rejects only an explicit `null`,
  so the collection was silently `null`.
* `LabelersPreference` now reports a preference which omits its `labelers` collection as a failure rather than leaving it `null`. Its sibling preference
  types already guarded against this.
* `KnownFollowers` now reports a response which omits its `followers` collection as a failure, rather than throwing an `ArgumentNullException`.
* `EmbeddedImages` no longer throws an `ArgumentOutOfRangeException` when deserializing an empty `images` array. `app.bsky.embed.images` sets no minimum,
  so an empty array is a valid payload. Constructing an `EmbeddedImages` directly still requires at least one image.
* `GetPosts()`, `GetSuggestedStarterPacks()`, `GetSuggestedUsers()`, `GetSuggestions()`, `GetTrends()` and `SendMessageBatch()` now skip and log `null` entries
  inside an otherwise well formed collection rather than handing those `null`s to the caller, matching the paged readers.
* The recommendation identifier returned by `app.bsky.unspecced.getSuggestedUsers` is now read as nullable. It is optional in the lexicon, and a missing
  property is not covered by `RespectNullableAnnotations`, so the member claimed a guarantee the wire format does not give.
* `GetConversationMembers()`, `GetListsWithMembership()`, `GetPopularFeedGenerators()`, `GetStarterPacksWithMembership()`, `GetSuggestedStarterPacks()`,
  `GetTaggedSuggestions()`, `GetTrends()` and `ListActivitySubscriptions()` now report a response which omits its collection as a failure. The collection is
  required by the lexicon, but it is declared as a positional record parameter, where `JsonRequired` has to be applied with the `property:` target to have any
  effect. Without it a missing collection surfaced to the caller as a `NullReferenceException` rather than as a failed result.
* `ListActivitySubscriptions()` now skips and logs `null` entries inside an otherwise well formed `subscriptions` collection rather than handing those `null`s
  to the caller.
* `GetTimeline()` now reports a response which omits its `feed` collection as a failure. The collection is required by the lexicon, but the response type
  declared it as nullable and substituted an empty collection when it was absent, so a malformed response was indistinguishable from an empty timeline.
* `GetConversationLog()`, `GetMessages()`, `ListConversations()`, `GetList()`, `GetFeedGenerators()`, `GetLikes()`, `GetQuotes()`, `GetRepostedBy()`,
  `GetSuggestedFeeds()`, `GetTimeline()`, `SearchPosts()`, `SearchPostsV2()` and `GetPostThreadOtherV2()` now skip and log `null` entries inside their
  collections rather than returning them. Neither `JsonRequired` nor `RespectNullableAnnotations` applies to a collection's element type, so a service could
  place a `null` inside an otherwise well formed collection and it would be handed straight to the caller.

#### idunno.Bluesky.AspNet.Authentication

* The authentication cookie re-issued by a renewal now carries only the DID claim, as the cookie written at sign in does, rather than the hydrated identity.
  A renewal previously protected the access token, the refresh token and the DPoP proof key into the cookie, putting them in the browser.
* The identity store and the correlation state cache are now protected with data protection unless the application configures
  `BlueskyAuthenticationOptions.IdentityStoreEvents` or `CorrelationStateCacheEvents` itself, so credentials and login state are no longer written to shared
  storage in the clear by default.
* The handle a profile carries is now verified against the directory before it becomes a `ClaimTypes.Name` or handle claim, and is dropped when it does not
  resolve back to the DID it was returned for.
* A stored identity whose credentials cannot be read no longer authenticates the request. It previously skipped the expiry check and was handed to the application.
* An expired ticket now revokes the credentials it referred to at the authorization server rather than only dropping the local record of them.
* Signing in as a different DID now revokes and removes the session it replaces, rather than leaving live credentials in the store until they age out.
* An agent built from a principal issued by a scheme other than a registered Bluesky one now writes credential updates to the identity store of the scheme
  the agent factory was registered for, rather than to a fresh store nothing reads.
* The identity store refresh lock token is now compared in constant time.

## 6.0.0 - 2026-09-05

### Added

#### idunno.Bluesky

* Added support for opt-ing out of a reference list (typically a starter pack), via `BlueskyAgent.CreateReferenceListOptOut`. `ListReferenceListOptOuts` allow
  for enumeration of an authenticated user's opt-outs, and `DeleteReferenceListOptOut` allows the deletion of the opt-out. Bluesky allows multiple optout
  records for the same list. If another opt-out record for the list still exists after deletion the opt-out remains in effect. See [APP-2933: implement reference-list opt-outs in AppView](https://github.com/bluesky-social/atproto/pull/5461).

  To check if the authenticated user has opted out of a reference list, you use `agent.GetList`, then check `List.Viewer?.ReferenceListOptOut != null`.

  As a list owner you can check if a user has opted out of your reference list by calling `agent.GetList`, then checking `List.Viewer?.ReferenceListOptOut`. If the user has opted out,
  the `ReferenceListOptOut` property will be non-null with a value of `true`. For example:
  ```c#
  var myListResult = await agent.GetList(myList, cancellationToken: cancellationToken);
  var optedOutUsers = from item in myListResult.Result
                      where item.SubjectOptedOut is not null && item.SubjectOptedOut.Value
                      select item.Subject;
  ```
* `ListBlock` has been added in the `idunno.Bluesky.Graph` namespace, representing a block relationship against an entire list of accounts (actors).
* Added `Actor.ProfileAssociatedActivitySubscription` and the known values `AllowSubscriptionsKnownValues`. This appears in the lexicon, but does not current seem to be used anywhere.
* Added gallery support to Draft posts, via `Draft.EmbeddedGallery`.
* Added optional `UpdatedAt` property to `Actor.InterestsPreference`, which indicates when the account owner last updated their interests. See [Add updatedAt to base prefs lexicon- #43](https://github.com/bluesky-social/bsky/pull/43/)
* Added `Preferences.Interests` property, which is an `InterestsPreference` instance, which in turn allows for a new `UpdatedAt` property.
* Added `Preferences.LiveEventPreferences` property, which is a `LiveEventPreferences` instance.

### Breaking Changes

#### idunno.Bluesky

* Namespace changes to match the lexicon:
  * `BlueskyList` been renamed to `List` and has moved namespaces from `idunno.Bluesky.Record` to `idunno.Bluesky.Graph`.
  * `BlueskyListItem` has been renamed to `ListItem` and has moved namespaces from `idunno.Bluesky.Record` to `idunno.Bluesky.Graph`.
  * `Block` has moved namespaces from `idunno.Bluesky.Record` to `idunno.Bluesky.Graph`.
  * `Follow` has moved namespaces from `idunno.Bluesky.Record` to `idunno.Bluesky.Graph`.
  * `StarterPack` has moved namespaces from `idunno.Bluesky.Record` to `idunno.Bluesky.Graph`.
  * `Verification` has moved namespaces from `idunno.Bluesky.Record` to `idunno.Bluesky.Graph`.
  * `Record.Like` has been moved to `Feed.Like` to match the lexicon definition.
  * `Record.Repost` has been moved to `Feed.Repost` to match the lexicon definition.
  * `Record.Profile` has been moved to `Actor.Profile` to match the lexicon definition.
  * `Record.Status` has been moved to `Actor.Status` to match the lexicon definition.
  * `Record.KnownStatusValues` has been moved to `Actor.KnownStatusValues`.
  * `Record.LabelerDeclaration` has been moved to `Labeler.Service` to match the lexicon definition.
* `BlueskyAgent.GetContentVisibilityDeclaration` now returns a `ContentVisibilityDeclaration` instance instead of a `bool`.
  The `HideFromAlgorithmicRecommendations` property on the returned object indicates whether the content is hidden from algorithmic recommendations.
* Removed deprecated `Bluesky.UploadAnimatedGif`. Use `BlueskyAgent.UploadVideo` with a MIME type of `image/gif` instead.
* Removed deprecated `UploadVideo(string fileName, byte[] video, CancellationToken cancellationToken)`.
  Use `UploadVideo(string fileName, byte[] video, string mimeType, CancellationToken cancellationToken)` with a MIME type of `video/mp4` instead.
* `BlueskyAgent.UnmuteModeList` spelling has been corrected to `BlueskyAgent.UnmuteModList`.
* `ListViewerState` has been converted to a C# Record and now has all it's properties as nullable, to match the lexicon.
* `Feed.Likes` has been renamed to `LikesCollection` to better reflect its purpose.
* `Feed.Like` has been moved to its own `Feed.Likes` namespace, as it is only a component in the `LikesCollection` collection.
* Actor preferences have had major changes, to match the published lexicons and fix deserialization issues with the previous implementation.
  * The `SavedFeedPreferences2` type has been renamed to `SavedFeedPreferencesV2` to match the lexicon.
  * The `SavedFeedPreference` type has been renamed to `SavedFeedsPreference` to match the lexicon.
  * The `SavedFeedPreference2` type has been renamed to `SavedFeed` to match the lexicon.
  * Changed `InterestsPreference.Tags` from `IReadOnlyList<string>` to `ICollection<string>` to allow for easier modification of the list of tags.
  * `Preferences.SavedFeedPreference2s` has been replaced by `Preferences.SavedFeedsPreferenceV2`,
    which is an `IReadOnlyList<SavedFeed>`.
  * `Preferences.InterestTags` has been removed, use `Preferences.Interests` instead, and iterate through the `Tags` property.
  * `Preferences.SavedFeedPreferences`, an `IReadOnlyList<SavedFeedPreference>`, has been removed and replaced with `Preferences.SavedFeedsPreference`, a single nullable `SavedFeedsPreference`.
  * Changed `SavedFeedsPreference.Saved` and `SavedFeedsPreference.Pinned` from `IReadOnlyList<AtUri>`
    to `ICollection<AtUri>` to allow for easier modification.
  * `Preferences.InteractionPreferences` has been renamed to `Preferences.PostInteractionSettingsPreferences` to match the lexicon definition.
  * `Preferences.FeedViewPreferences` guards against multiple instances of `FeedViewPreference` for the same feed,
    and will use the last instance in the list if duplicates are present. Whilst duplicates are technically valid in the lexicon,
    they are not expected to be present in the wild, and this change prevents deserialization errors when they are encountered.
  * `BlueskyAgent.PutPreference` has been removed, use `BlueskyAgent.PutPreferences` instead, which accepts a `Preferences` instance and replaces the entire preferences for the authenticated user.
* `ActivitySubscription` has been replace with `Actor.ProfileAssociatedActivitySubscription` to match the lexicon definition. `ProfileAssociated` has been updated to use the new record.

### Fixed

#### idunno.Bluesky

* Fixed a bug in the deserialization of `Actor.Preferences` where `SavedFeedsPreferenceV2` was not being deserialized correctly, resulting in null values.
* `Preferences.SavedFeedsPreference` is now correctly deserialized.
* `Preferences.SavedFeedsPreferenceV2` is now correctly deserialized.

## 5.0.0 - 2026-08-24

### Added

#### idunno.Bluesky

* Added support for the new multi-part video upload API, via `StartUpload`, `UploadPart`, `FinishUpload`, `GetUploadStatus` and `AbortUpload`.
* Added new error classes for errors from the multi-part video upload APIs.
  * `BadAspectRatio`
  * `DailyLimitExceeded`
  * `InvalidPartNumber`
  * `MissingParts`
  * `PartSizeMismatch`
  * `TooManyOpenUploads`
  * `ServiceOverloaded`
  * `UnsupportedContentType`
  * `UploadAborted`
  * `UploadAlreadyCompleted`
  * `UploadExpired`
  * `UploadFailed`
  * `UploadForbidden`
  * `UploadNotFound`
  * `UploadNotReady`
  * `VideoTooLarge`
  * `VideoTooLong`
* Added support for `ContentVisibilityDeclaration`, with
  `BlueskyAgent.GetContentVisibilityDeclaration`, `BlueskyAgent.SetContentVisibilityDeclaration` and `BlueskyAgent.DeleteContentVisibilityDeclaration`.
  See [Add content visibility lexicon](https://github.com/bluesky-social/atproto/pull/5372).
* Added `KnownLikers` view. See [Add knownLikers to viewer state](https://github.com/bluesky-social/atproto/pull/5427).

### Breaking Changes

#### idunno.AtProto
* `accessCredentialsUpdated` parameter on `AtProtoServer.GetServiceAuth` has been renamed to `credentialsUpdated` to match other methods.

#### idunno.Bluesky

* `BlueskyServer.GetVideoJobStatus` has been renamed to `BlueskyServer.GetJobStatus` to reflect the new multi-part video upload API.
* `BlueskyAgent.GetVideoJobStatus` has been renamed to `BlueskyAgent.GetJobStatus` to reflect the new multi-part video upload API.
* `FeedViewerState` has been renamed to `ViewerState` to match the lexicon definition. All properties in `ViewerState` are now nullable, as they're defined as optional in the ATProto lexicon
  A `KnownLikers` property has been added to provide a list of likers of a post who the authenticated user also follows. See [Add knownLikers to viewer state](https://github.com/bluesky-social/atproto/pull/5427).
* `UploadVideo` now requires a MIME type parameter to allow for more video formats. The previous method which assumed a MIME type of `video/mp4` has been marked obsolete.
* `UploadAnimatedGif` has been marked obsolete. Use `UploadVideo` with a MIME type of `image/gif` instead.
* `GetVideoUploadLimits` has been renamed to `GetUploadLimits` to more accurately reflect the lexicon.
* `GetAnimatedGifJobStatus` has been removed, use `GetJobStatus` instead.

### Fixed

#### idunno.AtProto

* Fixed a bug in `AtProtoAgent.GetServiceAuth` where if OAuth credentials were used DPoP nonce updates were not respected.

## 4.0.0 - 2026-08-10

### Added

#### idunno.AtProto

* Added `BlobUploadLimit` to `ServerDescription`, which indicates the maximum size of a blob that a server will accept via uploadBlob. See [pds: expose blobUploadLimit through describeServer](https://github.com/bluesky-social/atproto/pull/5277).

#### idunno.Bluesky

* Added support for muting actors reposts and quote posts, with the addition of scopes to the `MuteActor` method to specify the type of mute. See [Add repost and quotepost-only mutes](https://github.com/bluesky-social/atproto/pull/5118).
   For example, to mute only reposts from an actor, you can use the following code:
   ```c#
   await agent.MuteActor(
     new Handle("jcsalterego.bsky.social"),
     onlyReposts: true,
     onlyQuotePosts: null);
   ```
* Added optional `FailureCode` property to `JobStatus` class to provide machine-readable failure codes for video processing jobs. Known values are defined in the `FailureCodes` class. See [Add video job failure codes](https://github.com/bluesky-social/atproto/pull/5283).
* Added new, undocumented, `Uploading` and `Encoding` states to the `JobState` enum to reflect the discovered video processing states.
* Added implementation of the unspecced `GetPostThreadV2` and `GetPostThreadOtherV2` apis.
* Updated `CreateGroup` to allow up to 10000 members in a group. See [update chat lexicons](https://github.com/bluesky-social/atproto/pull/5303).
* Added fallback in `OpenGraphEmbeddedCardGenerator` to also look for `<meta name="og:([^\"]+)" content="([^\"]+)"` tags, not just `<meta property="og:([^\"]+)" content="([^\"]+)"` tags.
* Added `SubscribedLabelers` optional parameter to `ListActivitySubscriptions`, `ListNotifications` and `GetSuggestedUsers` to allow labels to be applied to the returned results.

### Fixed

#### idunno.AtProto

* Fixed `BlueskyServer.GetVideoUploadStatus` to correctly use the correct service credentials.

#### idunno.Bluesky

* Fixed a bug in `GetMutes` where the cursor query string parameter was being generated incorrectly.

### Breaking Changes

#### idunno.AtProto

* `ServerDescription`, `Links` and `Contact` are now part of the `idunno.AtProto.Server` namespace.
* `InviteCodeRequired` and `PhoneVerificationRequired` properties of `ServerDescription` are now nullable, as they're defined as optional in the ATProto lexicon.

#### idunno.Bluesky

* `ActorViewerState` has been renamed to `ViewerState` to match the lexicon definition. Its constructors have updated to be `internal`,
  and the `Muted` and `BlockedBy` properties are now nullable, as they're defined as optional in the ATProto lexicon.
  Two new properties, `MutedOnlyReposts` and `MutedOnlyQuotePosts`, have been added to reflect the new mute scopes.
* Removed the ambiguous `ListNotifications` method.
* Removed the ambiguous `GetSuggestedUsers` method.

## 3.1.0 - 2026-07-29

### Added

#### idunno.Bluesky

* Added support for the new `SearchStarterPacksV2` API endpoint.
* Added a Starter Pack property to the `Notification` class, which is present when the notification is for a follow originating from a starter pack. See [Hydrate starter pack info for follow notifications](https://github.com/bluesky-social/atproto/pull/5263).
* Added an optional `Sort` parameter to the `GetFollowers` and `GetFollows` methods, which allows sorting by "latest" or "top". See [Add sort order to follows endpoints](https://github.com/bluesky-social/atproto/pull/5257).
* Added description field to trending topics. See [Add description field to trending topics](https://github.com/bluesky-social/atproto/pull/5254).

## 3.0.0 - 2026-07-11

### Added

#### idunno.AtProto

* Added error classes for known error messages.
  * `AccountNotFound`
  * `AccountTakedown`
  * `AuthenticationRequired`
  * `AuthFactorTokenRequired`
  * `BadExpiration`
  * `BlobNotFound`
  * `BlockNotFound`
  * `ConsumerTooSlow`
  * `DidDeactivated`
  * `DidNotFound`
  * `DuplicateCreate`
  * `ExpiredToken`
  * `FutureCursor`
  * `HandleNotAvailable`
  * `HandleNotFound`
  * `HeadNotFound`
  * `HostBanned`
  * `HostNotFound`
  * `IncompatibleDidDoc`
  * `InvalidEmail`
  * `InvalidInviteCode`
  * `InvalidPasscode`
  * `InvalidRequest`
  * `InvalidSwap`
  * `InvalidToken`
  * `MethodNotImplemented`
  * `RecordNotFound`
  * `RepoDeactivated`
  * `RepoNotFound`
  * `RepoSuspended`
  * `RepoTakenDown`
  * `TokenRequired`
  * `UnresolvableDid`
  * `UnsupportedDomain`
  * `XrpcNotSupported`
* Added `MapError` property to `AtProtoHttpResult<TResult>` which is an `IList<Func<AtErrorDetail?, AtErrorDetail?>>`
  to allow mapping for the generic `AtErrorDetail` type to a more specific derived error type.

#### idunno.Bluesky

* Added `BlueskyHttpClient` which derives from `AtProtoHttpClient` and adds the Bluesky specified error mapping.
* Added error classes for known error messages.
  * `AccountSuspended`
  * `ActorNotFound`
  * `BadQueryString`
  * `BlockedActor`
  * `BlockedSubject`
  * `ConversationLocked`
  * `EnabledJoinLinkAlreadyExists`
  * `FollowRequired`
  * `InsufficientRole`
  * `InvalidCode`
  * `InvalidConversation`
  * `LinkDisabled`
  * `MemberLimitReached`
  * `MessageDeleteNotAllowed`
  * `MessagesDisabled`
  * `NewAccountCannotCreateGroup`
  * `NoJoinLink`
  * `NotFollowedBySender`
  * `OwnerCannotLeave`
  * `ReactionInvalidValue`
  * `ReactionLimitReached`
  * `ReactionMessageDeleted`
  * `ReactionNotAllowed`
  * `RecipientNotFound`
  * `UserForbidsGroups`
  * `UserKicked`
* Added `Embed.EmbeddedGallery` and `Embed.Gallery.GalleryImage` types to support the new [image gallery format](https://github.com/bluesky-social/atproto/pull/4827).
  Note that `GalleryImage` requires an aspect ratio, unlike the `EmbeddedImage` type.
* Added new overloads to `BlueskyAgent.Post` to support `EmbeddedGallery` content.
  If you pass more than 4 `EmbeddedImage` items to `Post()`, it will attempt to convert them an `EmbeddedGallery`. Your `EmbeddedImage`
  instances must have an aspect ratio for this to work, otherwise an exception will be thrown.
  You can use [Magick.NET](https://github.com/dlemstra/Magick.NET), [ImageSharp](https://github.com/SixLabors/ImageSharp) or other image processing libraries to calculate the aspect ratio of your images if needed.
* Added new overloads to `PostBuilder` to support `EmbeddedGallery` content.
  If you use the `+` operator to add more than 4 `EmbeddedImage` items to a `PostBuilder`, it will attempt to convert them to `EmbeddedGallery` format. Your `EmbeddedImage`
  instances must have an aspect ratio for this to work, otherwise an exception will be thrown.
  You can use [Magick.NET](https://github.com/dlemstra/Magick.NET), [ImageSharp](https://github.com/SixLabors/ImageSharp) or other image processing libraries to calculate the aspect ratio of your images if needed.
* Added support for group conversation APIs, including
  * `AddMembersToGroup`
  * `ApproveJoinGroupRequest`
  * `CreateGroup`
  * `CreateJoinGroupLink`
  * `DisableJoinGroupLink`
  * `EditGroup`
  * `EditJoinGroupLink`
  * `EnableJoinGroupLink`
  * `GetJoinGroupLinkPreviews`
  * `ListJoinGroupRequests`
  * `ListMutualGroups`
  * `RejectJoinGroupRequest`
  * `RemoveGroupMembers`
  * `RequestJoinGroup`
  * `UpdateJoinGroupRequestsRead`
  * `WithdrawJoinGroupRequest`
* Added support for locking conversations with `LockConversation` and unlocking conversations with `UnlockConversation`.
* Added Animated GIF support with `BlueskyAgent.UploadAnimatedGif()`
* Expanded video support with a new overload to `BlueskyAgent.UploadVideo()` which accepts a MIME type parameter to allow for more video formats.
* Added `GetStarterPacksWithMembership` to allow the authenticated user to search their starter packs and see which ones a specified actor is a member of.
* Added `SearchPostsV2` to allow searching for posts with more advanced filtering options.

### Fixed

#### idunno.AtProto

* Added error handling inside `AtProtoAgent.ResolveHandle()` to swallow exceptions from DNS and HTTP lookup.
  Errors are logged as Debug log messages and the method will return null if errors are encountered.

#### idunno.Bluesky

* Added missing `Like` settings to `Notification.Preferences`.

### Changed

#### idunno.AtProto.Types

* Added comparison operators to `TimestampIdentifier` to allow for equality comparisons and sorting.

#### idunno.Bluesky

* `GetUnreadConversationCounts` now has an optional `includeGroupChats` parameter, which defaults to `true`, to include or exclude group conversations in the unread counts.

## Breaking Changes

#### idunno.Bluesky

* Corrected case of `AllowSubscriptions` property on `ActivitySubscriptions`.
* Changed `BlueskyAgent.GetRelationships()` to accept `AtIdentifier` instead of `Did` types for the `actor` and `others` parameters.
  This allows for more flexibility in specifying the subject and actor, as they can now be provided as either a DID or a handle, and matches the API definition.
* Changed return type of `BlueskyAgent.GetRelationships()` to `RelationshipMap`.
* `MessageViewBase` has had its properties removed, and is now an empty class.
  The `Id`, `Revision`, and `SentAt` properties have been moved to the derived classes `MessageView`, `DeletedMessageView` and `SystemMessageView`. This was
  necessary to support the new `MessageBeforeUserJoinedGroupView` type, which does not have these properties.
* `Notifications.PreferenceTypes.ChatPreference` has been marked obsolete.
  Use `GetChatNotificationPreferences` and `SetChatNotificationPreferences` with `Chat.Notifications.Preferences` instead.
* Marked `BlueskyAgent.UploadVideo()` without the MIME type parameter as obsolete, as it only supports MP4 video files. Use the new overload with the MIME type parameter instead.

## 2.0.0 - 2026-06-02

### Added

#### idunno.AtProto.Types

* Moved the atproto `Blob` type from idunno.AtProto.
* Added the atproto `CidLink` type.
* Added the atproto `Bytes` type and associated JSON converter.

#### idunno.Bluesky

* Added classes for Germ Network lexicon.
* Added classes for Standard.Site lexicon.
* Added `Labels` property to `StatusView` to represent labels associated with a status. See [[APP-1775] Hydrate labels for actor statuses](https://github.com/bluesky-social/atproto/pull/4555)
* Added `OpenGraphEmbeddedCardGenerator` to generate embedded records from Open Graph metadata, and documented its use.
* Added `StandardSiteEmbeddedCardGenerator` to generate embedded records from [standard.site](https://standard.site), and documented its use.

### Changed

#### idunno.Bluesky

* `BlueskyAgent.Post(Post post)` now extracts facets from the post text by default, as promised in documentation. You can use the `extractFacets` parameter to control this behavior.
* Updated `EmbeddedExternalView` to support the new [Standard Site integration](https://github.com/bluesky-social/atproto/discussions/4978).
* Updated `EmbeddedExternal` to support the new [Standard Site integration](https://github.com/bluesky-social/atproto/discussions/4978).
* Updated `ProfileViewBasic` to add properties for [Germ integration](https://github.com/bluesky-social/atproto/pull/4415).
* Updated `VerificationView` to add properties for [Fix app.bsky.actor.getProfile verifier data](https://github.com/bluesky-social/atproto/pull/5016)

### Breaking Changes

#### idunno.AtProto

* Moved `Blob` type to `idunno.AtProto.Types`.
* Moved `BlobReference` type and renamed to `CidLink` in `idunno.AtProto.Types`.

#### idunno.Bluesky

* Moved various external Embedded content classes to their own files and namespaces. This is a breaking change if you are using these classes directly, rather than through the utility methods on `BlueskyAgent`.
* Removed previously marked obsolete `BlueskyAgent.SetLiveStatus` method. Update your code to use `BlueskyAgent.CreateLiveStatus` instead.
* Removed previously marked obsolete `BlueskyAgent.SetStatus` method. Update your code to use `BlueskyAgent.CreateStatus` instead.
* Removed previously marked obsolete `SelfLabelNames` class. Update your code to use `SelfLabelValues` instead.
* `CreateLiveStatus` now requires a description.

## 1.8.3 - 2026-05-16

### Added

#### idunno.Bluesky

* Added validation to `PostBuilder` to check its internal state before converting to a post.
  You can call `IsValid()` to check if the `PostBuilder` is coherent and enumerate through `ValidationErrors()` to
  see the errors that are present.
* Added some more helpers to `PostBuilder`, including `Add` overloads for self labels,
  `ContainsGraphicMedia`, `ContainsNudity`, `ContainsPorn` and `ContainsSexualContent` for setting individual self labels.
* Added `.ReplyTo()` to `PostBuilder` for replying to a post or a thread.
* Added `.Quote()` to `PostBuilder` for adding a quote of another post to the post content.
* Added various `Add` overloads to `PostBuilder` for adding content to the post body, including hash tags, links and mentions.

### Changed

#### idunno.Bluesky

* Updated `PostBuilder` to allow for replies to quote other posts. Fixes [#343](https://github.com/blowdart/idunno.Bluesky/issues/343), thank you [OatmealDome](https://github.com/OatmealDome).

## 1.8.2 - 2026-05-11

### Added

#### idunno.Bluesky

* Added support for `GetListsWithMembership()`, which enumerates the lists created by the authenticated user, and includes membership information about the specified actor in those lists.

### Changed

#### idunno.Bluesky

* Updated `EmbeddedExternal` to include option `AssociatedRecord` property. See [Add associatedRecord to external embed record](https://github.com/bluesky-social/atproto/pull/4915)

## 1.8.1 - 2026-04-23

### Changed

* Updated OpenTelemetry dependencies to address [CVE-2026-40894 - OpenTelemetry dotnet: Excessive memory allocation when parsing OpenTelemetry propagation headers](https://nvd.nist.gov/vuln/detail/CVE-2026-40894)

## 1.8.0 - 2026-04-04

### Added

#### idunno.AtProto

* Added metrics in `AtProtoHttpClientMetrics` including request duration, request count and failure count.
* Added metrics in `DidPlcDirectory` including request duration, request count and failure count.
* Added extensions for `OpenTelemetry.Metrics`: `AddAtProtoHttpClientMetrics`, `AddAtProtoDirectoryMetrics`, and `AddAtProtoJetStreamMetrics`.
* Added new constructor overloads for `AtProtoHttpClient` to allow for use with `MetricsFactory`.
* Added `Throttled` to `AccountStatus` in Jetstream account events.
* Added `MaxMessageSize` to `JetStreamOptions` to guard against a malicious jetstream server sending overly large messages.
* Added optional validation callbacks to `AtProtoAgent.BuildOAuthLoginUri` to allow for validation of the discovered PDS and authorization server URIs.
* Added default validation function to disallow loopback and private IP ranges in discovered URIs, to mitigate SSRF attacks.
* Added override on `ToString()` on `AtProtoCredential` to return a redacted string in case of accidental logging.
* Added default SSRF protections to `AtProtoAgent`, `AtProtoHttpClient` and `AtProtoJetStream` with [idunno.Security.Ssrf](https://github.com/blowdart/idunno.Security.Ssrf/blob/main/src/idunno.Security.Ssrf/).
  This can be disabled by passing your own `HttpClient` when creating an agent, or into `AtProtoHttpClient`.
* Added `AllowLoopback` parameter to `BuildOAuth2LoginUri` to allow loopback addresses in discovered URIs for testing and development purposes. This is disabled by default.

### idunno.AtProto.Types

* Added == and != operations to `Cid`.

### idunno.Bluesky

* Added `Bot` property to `Profile` record to set or unset the profile self label
  indicating a bot account, see [[APP-1928] add bot/automated account badge and self-labeling settings](https://github.com/bluesky-social/social-app/pull/10008/)
* Added `SelfLabels` property to `ProfileViewBasic` which returns a list of self labels applied to a profile,
  which can be used in conjunction with `SelfLabelValues` to check if a profile has applied any self labels to itself,
  including the `Bot` self label and `DiscourageShowingToLoggedOutUser` self label. e.g.

  ```c#
  var profile = await agent.GetProfile("beans.monster");
  if (profile.Result.SelfLabels.Contains(SelfLabelValues.Bot))
  {
      // 🤖 - Do some action because the profile self identifies as a bot.
  }
  ```
* Added `SelfLabels` property to `PostView`.
* Added `SelfLabelValues` class.
* Added `Bot` and `DiscourageShowingToLoggedOutUser` to `SelfLabelValues`.
* Added `JsonPolymorphic` attributes to individual records to remove the extraneous `ExtensionData` entries.
* Added `CreateStatus`, `GetStatus` and `UpdateStatus` to `BlueskyAgent`.
* Added a setter to `DurationMinutes` on `Status` and setters to `ExternalProperties` to allow for updating of an existing profile status.

### Documentation

* Added documentation for metrics.
* Added documentation for setting a status on a profile.

### Changed

#### idunno.AtProto

* Changed `AtJetStreamIdentity` class to make `Handle` property nullable.
* Add version to `JetstreamMetrics`
* Made `JetStream.MeterName` and `JetStream.MeterVersion` properties public to allow for easy OTEL configuration.
* Fixed OAuth logout.
* Changed `JetStreamMetrics` from `public` to `internal` because it is not intended for public use.
* Remove `[Serializable]` from `AtProtoCredential`.
* Exclude `Credential` in `CredentialException` from serialization because it may contain sensitive information.

#### idunno.AtProto.Types

* Updated DID validation regex to align with [specs: allow digits in DID method](https://github.com/bluesky-social/atproto-website/issues/292).

#### idunno.Bluesky

* Updated `SuggestedActors` to include `RecIdStr`, see [Add recIdStr to suggested follows by actor](https://github.com/bluesky-social/atproto/pull/4644)
* Added setter to `Notification.Declaration.AllowSubscriptions` for easy updating of the value.
* Mark `SetStatus` as obsolete in favor of `CreateStatus` and `UpdateStatus`.
  This allows for better handling of the case where a profile does not have an existing status,
  and clearer intent when updating an existing status.
* Marked `SelfLabelNames` as obsolete in favor of `SelfLabelValues`, as the new name is more correct.

#### idunno.AtProto

## 1.7.0 - 2026-03-12

### ⚠️Security Advisory - [CVE-2026-26127 - .NET Denial of Service](https://github.com/dotnet/announcements/issues/384)

* A transitive dependency of `idunno.AtProto` and `idunno.AtProto.OAuthCallback`, `Microsoft.Bcl.Memory`
  had a Denial of Service security vulnerability,
  [CVE-2026-26127](https://github.com/dotnet/announcements/issues/384)

  v1.7.0 updates the dependencies on `Duende.IdentityModel.OidcClient` and
  `Duende.IdentityModel.OidcClient.Extensions` which have
  updated their dependency on `Microsoft.Bcl.Memory` to 10.0.4, resolving the vulnerability.

  All previous versions of the library are now marked as vulnerable to CVE-2026-26127.
  Please update to v1.7.0 or later to resolve this vulnerability.

## 1.6.0 - 2026-02-21

### Added

#### idunno.AtProto

* Added `AccessTokenException`, which will be thrown if a supplied access token is not valid for a service.
* Added `GetRawRecord` to `AtProtoAgent` and `AtProtoServer`.

#### idunno.AtProto.Types

* Added `Self` property to `RecordKey` which returns a `RecordKey` with the value of `self`.

#### idunno.Bluesky

* Added support for drafts, including `Draft` record, `CreateDraft()`, `GetDraft()`, `ListDrafts()`, `DeleteDraft()` and `PublishDraft()`.
* Added `Post(DraftWithId)` to create a post from a draft.

### Fixed

#### idunno.Bluesky

* Fixed `DeleteLike()` to use the correct collection when validating the `uri` parameter.

#### Documentation

* Removed erroneous references to `CreatePost()`, thanks [shiftkey](https://github.com/shiftkey)

### Changed

#### idunno.AtProto

* `GetRecord<T>` and `ListRecords<T>` will now attempt to resolve the PDS hosting the record if a service is not specified.
* `GetRecord`, `CreateRecord`, `DeleteRecord`, `PutRecord`, `ListRecords` and `ApplyWrites` methods on `AtProtoServer` will now
  throw `AccessTokenException` if any specified credentials have not been issued by the specified service.
* Removed `Type` property from `Blob`.
* Removed `Type` property from `SelfLabels`.

#### idunno.Bluesky

* Removed `Type` property from `Facet`.

### Breaking Changes

#### idunno.AtProto

* The overloads for `ListRecords<T>` that requires authentication will ignore any service parameter passed to it.

#### idunno.Bluesky

* Switched `RemainingDailyVideos` and `RemainingDailyBytes` in `UploadLimits` to `long?`

## 1.5.0 - 2026-02-02

### Added

#### idunno.AtProto

* Added `TypeResolver` static class.
* Added `TimestampIdentifier` to `SourceGenerationContext.`

#### idunno.AtProto.Types

* Added new constructor on `TimeStampIdentifier` to create from a `RecordKey`.
* Added `TimeStampIdentifierJsonConverter` for `TimeStampIdentifier`.
* Added explicit and implicit conversions between `TimeStampIdentifier` and `RecordKey`.
* Added tests for `TimeStampIdentifier` serialization and deserialization.

#### idunno.Bluesky

* Added `TypeResolver` static class.
* Added support for `GetActorStarterPacks()`, addresses [#288](https://github.com/blowdart/idunno.Bluesky/issues/288), thank you [j-childers](https://github.com/j-childers)
* Added `FeedItem` record to represent feeds added in a `StarterPack` record.
* Added `Presentation` property to `EmbeddedVideo` and `EmbeddedVideoView` to for video presentation hints.
* Added `VideoPresentationKnowValues` static class with known presentation values for videos.

### Changed

#### idunno.Bluesky

* Added `Status` property to `ProfileViewBasic`, `ProfileView` and `ProfileViewDetailed` to represent live streaming status for a profile, if any.
* Changed `Description` property in `ListView` to be nullable to match the API, fixes [#289](https://github.com/blowdart/idunno.Bluesky/issues/289)
* `EmbeddedVideo` and `EmbeddedVideoView` include the optional `Presentation` property. See [Add presentation to video embed as a hint to the client about how to display the video](https://github.com/bluesky-social/atproto/pull/4581).
* `EmbeddedVideo` and `EmbeddedVideoView` constructors updated to take optional `presentation` parameter.

### Breaking Changes

#### idunno.AtProto

* `BlobReferences` now enforces that the link property is a CID.

#### idunno.Bluesky

* Changed `StarterPack` `Feeds` property to be a list of `FeedItem`, fixes [#288](https://github.com/blowdart/idunno.Bluesky/issues/288)
* Changed `StarterPack` constructor to take a list of `FeedItem` for feeds parameter, fixes [#288](https://github.com/blowdart/idunno.Bluesky/issues/288)
* Changed `EmbeddedVideo` and `EmbeddedVideoView` to include the optional `Presentation` property.
* `EmbeddedVideo` constructor overload that took a single `Caption` parameter has been removed. Use the constructor that takes an `ICollection` of `Caption`.

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

