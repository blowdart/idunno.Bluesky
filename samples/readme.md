# idunno.Bluesky Samples

This folder contains samples demonstrating various features of the `idunno.Bluesky` library.

Most console samples use the same command line arguments:

* `--handle` : The handle to use when authenticating.
* `--password` : The password to use when authenticating (this parameter is ignored by OAuth samples).
* `--authcode` : The authorization code to use when authenticating.
* `--proxy`: The URI of the proxy server you wish to use.

If you don't supply a handle or a password samples will check the `_BlueskyHandle` and `_BlueskyPassword` environment variables.

If you use a Bluesky app password you don't need to worry about authorization codes.

## Sample List

* `Samples.ConsoleShell` - a skeleton console application which authenticates with a handle and password that you can use as a starting point for experimentation.
* `Samples.ConsoleShellOAuth` - a skeleton console application which authenticates with OAuth that you can use as a starting point for experimentation.
* `Samples.Common` - helper functions used in the sample applications.

* `Samples.AspNetClientMetadata` - a minimal ASP.NET sample which redirects its home page to generated OAuth client metadata configured for example.org, with `atproto` and the Bluesky `ViewAll` permission set.
* `Samples.AspNetProgressiveAuthentication` - a Razor Pages sample based on `Samples.AspNetAuthentication`, starting with read permissions and adding profile-create/update consent when saving. It retains encrypted, expiring edits bound to the account and browser session, then completes saves with a single-use protected POST. See the [ASP.NET progressive authorization guide](../docs/docs/oauth/progressiveAuthorizationAspNet.md).
* `Samples.AspNetTunnelAuthentication` - an HTTPS sample for a public reverse tunnel, protecting the site with Bluesky authentication, requesting only `atproto`, and displaying the authenticated DID. It authenticates as a confidential client with a locally generated signing key. See its [readme](Samples.AspNetTunnelAuthentication/readme.md) for tunnel setup.
* `Samples.AtProto` - a sample showing how to use the underlying AtProto APIs.
* `Samples.Bot` - a sample showing a simple bot posting on a scheduled time.
* `Samples.DirectMessages` - a sample showing how to use the conversation APIs.
* `Samples.EmbeddedCard` - a sample showing how to embed an Open Graph card in a post.
* `Samples.Feed` - a sample showing how to page through a feed.
* `Samples.Firehose` - a live `SubscribeReposAsync()` firehose sample showing each event type, with a five-retry reconnection limit and Ctrl+C shutdown.
* `Samples.Jetstream` - a live v2 `StreamAsync()` sample with ordered events, a five-retry reconnection limit, and Ctrl+C shutdown.
* `Samples.JetstreamReplay` - a Jetstream v2 archive snapshot and live replay sample with checkpoints.
* `Samples.Logging` - a sample showing how to configure logging with the .net console logger.
* `Samples.LoginDiscovery` - a sample that walks through the various stages of how a handle is resolved to its Personal Data Store (PDS).
* `Samples.ModerationLabels` - a `SubscribeLabelsAsync()` sample which shows the labels applied, and negated, by a labeler, defaulting to the Bluesky moderation service, or the labeler given by `--labeler`. `--list` lists every labeler which has published a labeler service record, and `--live` narrows that list to the labelers which answer a query. `dotnet publish` produces it as a single native AOT executable; an ordinary build does not, as the native link step needs a platform C/C++ toolchain.
* `Samples.Notifications` - a sample which shows notifications for the authenticated user.
* `Samples.OAuth` - a sample that demonstrates how to login via OAuth.
* `Samples.OAuthPermissionSets` - a sample that requests the published Bluesky create-posts and delete-content permission sets, creates a post, refreshes credentials, then deletes the post.
* `Samples.Posting` - a sample that shows how to make posts.
* `Samples.ProgressiveOAuth` - a sample that reads the timeline, demonstrates a real missing-scope write failure, then adds create/delete Permission Sets and retries the same post.
* `Samples.ReactBff` - a React frontend and ASP.NET backend-for-frontend demonstrating OAuth login, session status, timeline retrieval, creating and deleting a public post, and CSRF-protected logout with server-only credentials. See the [setup and security notes](../docs/docs/reactBff.md).
* `Samples.Timeline` - a sample that shows reading and paging through the authenticated user's timeline.
* `Samples.TokenRefresh` - a sample that shows background token refresh happening, by hacking the refresh timer to be very short.
* `Samples.Video` - a sample that demonstrates video uploading and embedding.
* `Samples.WinUIOAuth` - a packaged Windows-only WinUI 3 sample with native OAuth callbacks and a read-only signed-in profile. Open the main solution; see [deployment and debugging](../docs/docs/windowsOAuth.md). Non-Windows hosts build an empty placeholder without Windows dependencies. It does not read console environment credentials.

* `Samples.BulkDelete` - an implementation of a bulk delete application, which allows you to specify the date/time before which your posts, likes etc. will be deleted.

## ASP.NET OAuth client metadata

```powershell
dotnet run --project samples\Samples.AspNetClientMetadata
```

Browse to `http://127.0.0.1:5252/`. The home page redirects to `/oauth-client-metadata.json` so you can inspect the generated JSON.
The document declares `https://example.org/oauth-client-metadata.json` as its client ID,
`https://example.org/Bluesky/Callback` as its callback, and `atproto` followed by the `BlueskyOAuthPermissionSets.ViewAll` scope.
These are example production URLs; local browsing is a preview, not an OAuth login.
The optional client name and homepage URL are configured under `BlueskyAgent:OAuthOptions` in the sample's `appsettings.json`, alongside its client ID, callback and scopes.
The same section contains a structured `PermissionSets` array with the `ViewAll` NSID and its unencoded audience.
All OAuth settings are bound from configuration; no permission-set construction in code is needed.
The agent's `OAuthOptions` also supplies example terms of service and privacy policy URLs,
published as `tos_uri` and `policy_uri` in the document.
The sample does not implement authentication or the callback endpoint.

## OAuth permission sets

Run the permission-set sample with your handle:

```powershell
dotnet run --project samples\Samples.OAuthPermissionSets -- --handle your-handle.bsky.social
```

The sample opens a browser for OAuth consent using a localhost callback. It requests `atproto`,
`BlueskyOAuthPermissionSets.CreatePosts`, and `BlueskyOAuthPermissionSets.DeleteContent`, without transition scopes.
The delete-content set also permits deleting likes and reposts; the sample only deletes the post it creates.
No blob permissions are needed for this text-only post.

It creates a public post containing `Hello OAuth Permission Sets`, demonstrates credential refresh, then deletes that
same post using the refreshed credentials and logs out. If refresh or deletion fails, it reports the post URI so you
can delete it manually. An interruption or exception after creation can also leave the post on your account.

## Progressive OAuth scope requests

```powershell
dotnet run --project samples\Samples.ProgressiveOAuth -- --handle your-handle.bsky.social
```

This sample opens two browser consent flows using local callbacks. It initially requests only `atproto` and
`rpc:app.bsky.feed.getTimeline?aud=did%3Aweb%3Aapi.bsky.app%23bsky_appview`, reads five timeline entries, and prints them.
The granular RPC scope is the least privilege needed for this operation: the published `BlueskyOAuthPermissionSets.ViewAll`
also permits reading notifications, preferences, and many other endpoints.

It then attempts to create the public post `Hello OAuth Permission Sets` and prints the actual `AtProtoHttpResult`:
`Succeeded`, numeric/named HTTP status, and `AtErrorDetail.Error` / `Message`. Bluesky's PDS implementation reports a
missing repository permission as HTTP 403 with `ScopeMissingError` and a message identifying the required scope.
The SDK maps this to `ScopeMissingError`; HTTP 403 `insufficient_scope` maps to `InsufficientScope`.
The sample checks these typed errors together with HTTP 403. Other failures, including expired credentials, rate limits, unexpected
forbidden responses, and network errors, stop the demonstration rather than being treated as evidence of missing scopes.
If the server allows the first write, the sample reports that the negative demonstration cannot be observed and does not
create a duplicate; it still requests delete permission to clean up that post.

Following the [progressive scope request pattern](https://atproto.com/guides/oauth-patterns#progressive-scope-requests),
the second authorization retains the timeline scope and adds `BlueskyOAuthPermissionSets.CreatePosts`
(`app.bsky.authCreatePosts`) and `BlueskyOAuthPermissionSets.DeleteContent` (`app.bsky.authDeleteContent`).
One fixed localhost client ID declares the maximum scopes in its `scope` query parameter; each pushed authorization
request (PAR) supplies its own subset. Using bare `http://localhost` separately for each subset would produce different
client IDs, not an upgrade for the same client. In production, publish the maximum scopes in a hosted client metadata
document and retain the same client ID throughout.

The callback restores saved OAuth state and uses the SDK's `ProcessOAuth2LoginResponse` overload with `expectedDid`
to verify that the new credentials have the original DID **before** replacing the authenticated agent's credentials.
Both flows use the same agent; a successful upgrade replaces its credentials without an intervening logout.
Only a recognized missing-scope failure is retried, using the
same post record and record key. The sample deletes the created post and logs out, reporting cleanup failures and the
URI for manual deletion. DeleteContent also permits deleting likes and reposts; this sample only deletes its own post.
It needs no blob or transition scopes.

Use a test account: the post is public, and an interruption, ambiguous write response, or failed upgrade/cleanup can
leave it on your account. The server must support granular permissions, Permission Sets, and localhost clients.
Automated tests exercise scope transport and guards against mock servers; live consent and server enforcement must
be verified interactively and are not implied by those tests.
