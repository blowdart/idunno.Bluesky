# Adding Bluesky authentication to a Windows desktop app

A desktop app can use Bluesky OAuth without hosting a callback web server.
Publish an OAuth client metadata document, register a custom URI scheme with Windows,
and route the browser's callback to the app instance that started login.
The SDK handles account discovery, pushed authorization requests, PKCE, DPoP,
token exchange and token refresh.

`Samples.WinUIOAuth` is a packaged WinUI 3 reference implementation. It requests
only permission to read a profile, displays the signed-in user's profile, and
revokes tokens on logout. The same OAuth lifecycle applies to other desktop UI
frameworks, but their startup and activation integration will differ.

## Publish your client metadata

AT Protocol authorization servers discover your app from its **client ID**,
which is the public HTTPS URL of a JSON document you host. You do not register
the app separately with each user's server.

Choose a URL on a domain you control, such as
`https://example.com/windows-oauth-client.json`. Serve the document directly with
HTTP **200** and `Content-Type: application/json`; do not redirect to a download,
HTML page or another URL. The URL must not contain a port number, and the
document's `client_id` must exactly match the URL used to fetch it.
The app does not need a local web server; only this metadata is hosted.

For a public desktop client using the Bluesky read-only `app.bsky.authViewAll`
permission set, a suitable document is:

```json
{
  "client_id": "https://example.com/windows-oauth-client.json",
  "client_name": "Example Windows app",
  "client_uri": "https://example.com/",
  "application_type": "native",
  "grant_types": ["authorization_code", "refresh_token"],
  "response_types": ["code"],
  "redirect_uris": ["com.example.desktop:/callback"],
  "scope": "atproto include:app.bsky.authViewAll?aud=did%3Aweb%3Aapi.bsky.app%23bsky_appview",
  "token_endpoint_auth_method": "none",
  "dpop_bound_access_tokens": true
}
```

Replace the example URLs, name and scheme with your own before publishing.
Choose an app-specific reverse-domain scheme based on a domain you control.
Use the same redirect string in the metadata and the SDK options: the example
has **one slash**, no authority, and the path `/callback`.
`com.example.desktop://callback` is a different URI.

The important choices are:

| Field | Purpose |
| --- | --- |
| `application_type: native` | Allows a native app callback rather than treating your app as a website. |
| `token_endpoint_auth_method: none` | Declares a public client. Do not embed a shared client secret or confidential-client signing key in a distributed desktop app. |
| `grant_types` | Include `authorization_code` for login and `refresh_token` if you use SDK token refresh. |
| `response_types: ["code"]` | Returns an authorization code, not tokens in the browser callback. |
| `redirect_uris` | Declares the exact callback addresses your app accepts. |
| `scope` | Declares all permissions the app may request. Include `atproto` and only the additional permissions your features need. |
| `dpop_bound_access_tokens: true` | Required by AT Protocol. The SDK creates a per-login DPoP proof key; this is distinct from a confidential-client signing key. |

The `include:` scope references the published permission set; its audience identifies
the Bluesky AppView. Keep the encoded audience as shown rather than decoding it
before supplying scopes. `app.bsky.authViewAll` grants broader read-only access
than the single profile RPC used by the reference app. Metadata declares the available
permissions; each login requests a subset. See [OAuth permissions](oauth/oauthPermissions.md)
for other scopes and permission sets.
Optional `client_uri` must have the same hostname as `client_id`; branding does
not guarantee an authorization server will display your app name.

The reference sample uses the already published document at
<https://bluesky.idunno.dev/windows-oauth-client.json>, with that exact URL as its
client ID and `dev.idunno.bluesky:/callback` as its redirect. It uses the grants
and public-client settings above, but its fixed scope is the narrower
`atproto rpc:app.bsky.actor.getProfile?aud=did:web:api.bsky.app%23bsky_appview`.
The document and SDK configuration examples in this guide instead use `ViewAll`.
Use your own metadata URL and scheme
for a different app rather than sharing the sample's identity or protocol handler.
Keep metadata available throughout the session lifecycle: servers can fetch it
again during refresh, not just at the first login.

## Register the callback scheme with Windows

For a packaged desktop app, add a `windows.protocol` extension inside the
application's `<Extensions>` element in `Package.appxmanifest`:

```xml
<Extensions>
  <uap:Extension Category="windows.protocol">
    <uap:Protocol Name="com.example.desktop">
      <uap:DisplayName>Example app OAuth callback</uap:DisplayName>
    </uap:Protocol>
  </uap:Extension>
</Extensions>
```

Declare the `uap` namespace on the root `<Package>` element:

```xml
xmlns:uap="http://schemas.microsoft.com/appx/manifest/uap/windows10"
```

The protocol `Name` is just the scheme, without `:` or `/callback`. The manifest
registers the scheme; your callback handler must validate the path and query.
See the sample's `Package.appxmanifest` for the complete package identity,
desktop target family, visual assets and `runFullTrust` capability.

**Installing or deploying the package performs registration. Building alone does not.**
On a development machine, enable Windows Developer Mode and use Visual Studio
Deploy or F5 with the packaged launch profile. This registers the package and
protocol for the current user. The browser may ask permission to open your app.
For distribution, sign the MSIX with a trusted certificate matching the package
publisher; the sample disables signing only for local development.
Uninstalling the package removes its protocol registration.

A custom scheme is not an exclusive proof of app ownership: other applications
can register handlers too. Keep PKCE, DPoP and callback validation enabled.
Do not replace token exchange with code that trusts an activation URI alone.
Unpackaged apps need a different registration/deployment mechanism; the manifest
instructions here apply to packaged Windows apps.

## Route activation to the instance that started login

Windows may start a new process when the browser opens your callback. That process
does not have the original PKCE verifier, DPoP key or pending state.
Use Windows App SDK's `AppInstance` APIs to forward activation to the existing
instance **before** creating another window or beginning another login.

The sample's `Program.cs`:

1. Reads `AppInstance.GetCurrent().GetActivatedEventArgs()`.
2. Uses `AppInstance.FindOrRegisterForKey` with a stable, app-specific key.
3. If another instance owns the key, awaits its `RedirectActivationToAsync`
   and exits without creating a UI.
4. Otherwise, subscribes to `AppInstance.Activated`, captures the initial
   activation, and starts the UI.

For WinUI, disable the generated entry point so your own `Main` can make that
decision first:

```xml
<DefineConstants>$(DefineConstants);DISABLE_XAML_GENERATED_MAIN</DefineConstants>
```

Follow `Program.cs` for the threading arrangement: forwarding runs in an MTA,
and the XAML UI starts on an STA thread with a dispatcher synchronization context.
Do not synchronously block the STA/UI thread on activation redirection.

Read the protocol data **inside the activation event handler**, then copy
`protocol.Uri.OriginalString` into a managed `Uri` before returning.
Do not queue `AppActivationArguments` or `IProtocolActivatedEventArgs` themselves
for later UI processing: the forwarding process may exit and disconnect their
COM proxies before the dispatcher reads them.
The sample queues managed `ActivationRequest` objects, including activations
received before the window exists, and dispatches them to `MainWindow`.

## Prepare login and retain the original state

Add `idunno.Bluesky` to your desktop project. Configure the agent with the same
client ID, redirect and requested scopes as your metadata:

```csharp
BlueskyAgent agent = new(new BlueskyAgentOptions()
{
    OAuthOptions = new OAuthOptions()
    {
        ClientId = "https://example.com/windows-oauth-client.json",
        ReturnUri = new Uri("com.example.desktop:/callback"),
        Scopes = ["atproto"],
        PermissionSets = [BlueskyOAuthPermissionSets.ViewAll]
    }
});
```

`BlueskyOAuthPermissionSets` is in the `idunno.Bluesky.Authentication` namespace.
Its `ViewAll` property produces the `include:app.bsky.authViewAll` scope with
the encoded AppView audience shown in the metadata.

Validate the entered handle, create a fresh OAuth client for this login, and
prepare the browser URL:

```csharp
OAuthClient client = agent.CreateOAuthClient();
Uri startUri = await agent.BuildOAuth2LoginUri(
    client, handle, cancellationToken: cancellationToken);
OAuthLoginState pendingState = client.State
    ?? throw new OAuthException("No login state was prepared.");
```

Here `handle` is a validated `Handle` and `cancellationToken` belongs to the
login operation. Keep the agent and `pendingState` alive until the callback
completes, fails, expires or is canceled. Register the pending state and callback
waiter **before** opening the browser, so a quick callback cannot arrive too early:

```csharp
bool opened = await Windows.System.Launcher.LaunchUriAsync(startUri);
```

If `opened` is false, report that the browser could not be opened and discard
the pending login. Use the system browser rather than an embedded login view.
The sample's `LoginAsync` shows the complete waiter, timeout and cleanup logic.

`OAuthLoginState` contains the PKCE verifier and private DPoP proof key, not just
a correlation string. Never log or display it, serialize it to unprotected
storage, or regenerate it in the process receiving the callback.
The sample holds it only in memory, so closing the app requires a new login.
The native callback scheme does not require disabling discovery validation or
allowing insecure/loopback network endpoints.

## Validate and exchange the callback

Treat every protocol activation as untrusted, including cold-start activations.
Before handing it to the SDK, require a pending, unexpired login and validate:

* The exact callback scheme/address/path, with no fragment.
* A bounded, well-formed query with no duplicate parameters.
* A `state` matching the pending login and an `iss` matching its discovered authority.
* Either a nonempty `code` or an OAuth `error`, not both.

See `OAuthCallbackRouter.cs` for the implementation. It uses absolute URI
normalization for issuer comparison, accommodating a normalized root slash
without ignoring issuer paths, queries, user information or fragments.
Malformed or mismatched activations do not consume a valid pending login.
An accepted callback consumes the state **before** any asynchronous exchange,
preventing a second activation from redeeming the same login.
Canceled, expired, unsolicited and replayed callbacks must produce explicit
errors, not create or restore a session.

Resume SDK processing using the original state, not a fresh login:

```csharp
OAuthClient responseClient = agent.CreateOAuthClient(pendingState);
bool authenticated = await agent.ProcessOAuth2LoginResponse(
    responseClient, callback.OriginalString, cancellationToken);
```

Here `callback` is the accepted managed URI. Check `authenticated` and
`agent.IsAuthenticated` before displaying a signed-in UI.
The SDK performs its own response/state processing, PKCE token exchange and
token validation, including issuer, DPoP token type and audience checks.
The app's activation checks supplement that processing; they do not replace it.
Display fixed, understandable errors rather than raw provider exceptions or
callback query strings, which may expose sensitive data.

## Use the authenticated session and clean up

To obtain the signed-in user's detailed profile, pass their DID explicitly:

```csharp
AtProtoHttpResult<ProfileViewDetailed> result = await agent.GetProfile(
    agent.Did, cancellationToken: cancellationToken);
```

Call this only after confirming the agent is authenticated. Check
`result.Succeeded` before reading `result.Result`.
The parameterless `GetProfile()` retrieves a repository profile record rather
than the detailed view containing follower/following counts.
`MainWindow.RefreshProfileAsync` shows how to display the detailed view and
load an HTTPS avatar without passing SDK credentials to the image loader.

Keep network calls asynchronous. The sample also moves SDK/key-generation work
off the UI thread, serializes user operations, supports cancellation, and bounds
the browser wait. Do not use `.Wait()` or `.Result` on the UI thread.
The agent refreshes OAuth credentials in memory while the session is active.

For explicit logout, call `await agent.Logout(cancellationToken)` to request
revocation, then dispose the agent and clear the local UI in a `finally` block.
Even if revocation fails, the app should not retain a locally signed-in session.
Explain that remote logout could not complete; users may need to remove the app
authorization through their account settings.

On window close, cancel and await outstanding operations before disposing the
agent, dropping pending state and stopping credential refresh.
Disposal and clearing managed references are not guaranteed cryptographic
erasure of strings from RAM. Closing the sample discards the local session but
does **not** revoke remote tokens; browser cookies also remain under the browser's
control. Session persistence, if added to your app, needs a separately designed
protected credential store rather than copying this sample's state to a file.

## Open the reference implementation

Use Windows 10 build 19041 or newer, Visual Studio 2026 with the WinUI application
development workload and single-project MSIX tooling, the .NET SDK pinned in
`global.json`, and Windows SDK 10.0.26100. The sample pins Windows App SDK 2.5.1
and Windows SDK BuildTools 10.0.28000.2705.

Open `idunno.Bluesky.slnx`, set `Samples.WinUIOAuth` under `samples/Windows` as
startup, and choose **Debug / Any CPU** and **Samples.WinUIOAuth (Package)**.
The solution maps this app to x64; use Configuration Manager to select ARM64
for an ARM64 device. F5 deploys/registers the package for your current user.

The app's `.csproj` evaluates its WinUI/MSIX settings only on Windows.
Non-Windows hosts build an empty library so the main solution remains portable;
the actual app still requires Windows tooling. Callback routing tests run
cross-platform.

For further background, see the [AT Protocol OAuth specification](https://atproto.com/specs/oauth),
[OAuth permissions](oauth/oauthPermissions.md) and
[Microsoft's single-instance WinUI guidance](https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/applifecycle/applifecycle-single-instance).
