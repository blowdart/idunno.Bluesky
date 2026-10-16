# Blazor authentication

This .NET 10 Blazor Web App ports the functionality of `Samples.AspNetAuthentication`: OAuth sign-in and sign-out,
the authenticated timeline (including reply parents and images), non-secret claims and token expiry, and profile editing.
It references `idunno.Bluesky.AspNet.Authentication` and the optional `idunno.Bluesky.AspNet.Authentication.UI`.

```powershell
dotnet run --project samples\Samples.BlazorAuthentication --launch-profile http
```

Open `http://127.0.0.1:5255/`, choose **Sign in**, and enter your Bluesky handle. Consent happens at your authorization
server; this application never asks for your password. It requests `atproto` and `BlueskyOAuthPermissionSets.FullApp`,
just like the Razor Pages sample. This is deliberately broad; choose narrower permissions for a real application.
Profile saves change your public profile. Use a test account.

Timeline image thumbnails, including media attached to quote posts, open full-size images in a modal dialog.
The dialog has a **Close** button, also closes with Escape, and displays the image's alt text beneath it.
External embeds pointing directly to GIF media (including Tenor GIFs) animate inline, using the embed description
as alt text and a hover tooltip, without a text caption. Bluesky's `Alt: ` / `ALT: ` description prefixes are removed.
Other external links remain cards; provider pages are not scraped or converted into guessed animation URLs.
The timeline also provides native video controls with a poster. Videos use the server's HLS playlist;
inline playback requires browser-native HLS support. An always-visible **Watch video on Bluesky** link provides playback
on other browsers without adding a third-party video player.

The local `imageViewer.js` module opens native browser dialogs without a circuit round trip. Native dialogs provide
keyboard focus containment and return focus to the thumbnail on close. Its delegated click listener also handles
images added by timeline refresh or interactive navigation. Thumbnail buttons have an accessible action name
even when the image has no alt text.

## Rendering and authentication

The app uses **Interactive Server**, with initial prerendering. After connecting, navigation, timeline refresh and profile
saves run over the Blazor circuit without full page reloads. The browser loads the local `blazor.web.js` framework script;
no npm packages or browser-side OAuth credentials are needed.

Login, logout and the OAuth callback remain the library's Razor Pages, styled with a local layout. Login/logout links
disable enhanced navigation so cookies are updated through real HTTP requests and returning to the app starts a fresh
circuit. Logout still requires a confirmation POST with antiforgery protection.

`AddInteractiveServerComponents()`, `AddCascadingAuthenticationState()` and `AuthorizeRouteView` expose server-side
authentication state. `[Authorize]` protects initial requests to the claims and profile pages and interactive routing.
`CircuitAuthenticationStateProvider` snapshots the cookie ticket's expiry during the initial HTTP request; it never
retains the request context. Every 30 seconds it checks expiry and the server-side identity store, signing the circuit
out if the session is gone or replaced. An operation also checks these conditions immediately, before accessing account
data. The original DPoP key distinguishes the circuit's session from a later login for the same account.

`CircuitBlueskyClient` creates and disposes an agent per operation from the **current stored identity**, not a stale
request or circuit principal. It uses the identity store's refresh lock to coordinate with other circuits and the HTTP
authentication handler, refreshes credentials near expiry, and awaits persistence of token and nonce updates.
Background agent refresh remains disabled. No circuit code attempts to set cookies.
The identity store is keyed by DID, so signing out an account invalidates its other circuits too.
`SessionIdentityStore` wraps the configured store with a single-process mutation gate. Login replacement and logout
share this gate with credential writes, which atomically require the original session's proof key to remain stored.
An in-flight response cannot recreate a removed session or overwrite a replacement login.

### Staying signed in while active

`SessionHeartbeat` loads the local `sessionHeartbeat.js` module once the circuit is interactive. Keyboard, pointer,
touch and scroll activity trigger a rate-limited heartbeat (at most once per minute). Visible, recently active tabs
also check once per minute; hidden or inactive tabs do not keep the session alive. The heartbeat POSTs to `/session/renew`
with the cookie and an antiforgery token rendered by the initial HTTP request.
Returning to a visible tab counts as activity and immediately checks the session, subject to the same request rate limit.

The endpoint authenticates through the existing Bluesky handler, which handles OAuth refresh. Once more than half the
cookie ticket lifetime has elapsed, the handler's normal sliding renewal writes a DID-reference cookie, respecting
`SlidingExpiration` and `AllowRefresh`. The original ticket lifetime is preserved. The response contains only an opaque,
data-protected expiry receipt bound to the account and its login's DPoP key. The circuit validates that receipt and
updates its expiry, so cookie renewal does not leave it enforcing the old deadline. It never trusts a browser-supplied
expiry, and a receipt from a replacement login cannot authorize an old circuit. Profile edits and navigation state
survive renewal without page reloads. Renewal does not sign in or add an identity to the store, so it cannot revive
a concurrently removed or replaced session. The expiry receipt is bounded by the renewed cookie's lifetime.

Temporary network failures show a warning and retry while active. Expired, signed-out or replaced sessions require
sign-in; renewal cannot revive them. Idle sessions still expire according to
the authentication options (14 days by default); this sample does not introduce an additional absolute lifetime limit.
Keep the configured ticket lifetime comfortably longer than the one-minute heartbeat interval and allow for browser
timer throttling and network delays. A suspended browser or prolonged outage cannot guarantee continuous renewal.

The heartbeat's browser behavior has dependency-free Node tests:

```powershell
node --test test\Samples.BlazorAuthentication.Test\sessionHeartbeat.test.mjs
node --test test\Samples.BlazorAuthentication.Test\profileDraft.test.mjs
node --test test\Samples.BlazorAuthentication.Test\imageViewer.test.mjs
```

`EditForm` validates both grapheme and UTF-8 limits on the server and saves through an interactive event, not an HTTP form
POST. Empty profile fields can be cleared. Saves retain the original CID, preserve other profile fields, and send a
conditional update to avoid overwriting concurrent edits. The editor updates its CID after a successful save so it can
be saved again without navigation. HTTP and session errors are shown instead of being treated as empty data or successful
saves. Component disposal cancels outstanding operations. API operations already in flight when logout occurs may finish.

The local `profileDraft.js` module records input changes in this tab's `sessionStorage` before circuit expiry can remove
the editor. After signing in, `/Manage` restores the draft only for the same DID. It retains the original CID, so saving
still detects profile changes made elsewhere; restoring never submits a write automatically. Successful saves clear
the draft, and **Discard draft and reload profile** clears it explicitly. A different account does not see the draft.
Storage failures show a warning to copy edits manually. Drafts contain profile text, not OAuth credentials, and are not
encrypted: same-origin scripts can access them. Avoid sensitive drafts on shared devices; closing the tab normally
clears its session storage. A disconnected circuit can still retain typed input locally, but cannot save it remotely
until authentication and connectivity are restored.
Discarding a draft removes its values from the editor before reloading the profile; if reload fails, the error is
shown without leaving discarded values available to save. Session-recovery sign-in links retain the current page
and query rather than always redirecting to the profile editor.

## Configuration

The HTTP launch profile enables Development settings. Its localhost client ID declares the same FullApp permissions
as the authorization request, and the callback uses HTTP loopback with the browser's port.
Keep `EnableBackgroundTokenRefresh` disabled; circuit operations coordinate refresh through the identity store.
Distinct cookie names and a distinct data protection application name isolate the sample from the other web samples
(cookies are shared across ports on the same host).

Production settings contain **placeholder** HTTPS client and callback URLs. Set them to your deployed URLs, update
`AllowedHosts`, and publish matching metadata (the middleware serves `/oauth-client-metadata.json` for hosted clients).
The default in-memory identity/correlation stores are only for development and lose sessions on restart.
Before deployment, configure persistent protected stores, shared data protection keys, HTTPS and trusted proxy handling.
In a multi-process deployment, the store must provide equivalent atomic session-proof checks and identity mutations
across all workers; the sample's process-local gate is not distributed coordination.
See the [ASP.NET guide](../../docs/docs/asp.net.md) and [production configuration](../../docs/docs/productionConfiguration.md).
