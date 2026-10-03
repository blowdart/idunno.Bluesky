# React backend-for-frontend sample

`samples/Samples.ReactBff` implements the BFF sample from [issue #588](https://github.com/blowdart/idunno.Bluesky/issues/588).
The React frontend uses relative, same-origin requests to an ASP.NET Core backend. Only the backend contacts Bluesky
and holds OAuth tokens, refresh credentials, PKCE state and DPoP private keys. No OAuth credential is returned in JSON,
stored in browser storage, or included in the authentication cookie.

The UI demonstrates OAuth login, session status, reading 20 timeline entries, creating a text-only public post,
deleting that exact post, and logging out. Timeline output is deliberately limited to public author handles, post URIs
and plain text: no embeds, moderation rendering or pagination. React renders text, never server-supplied HTML.

> [!WARNING]
> Use a test account. Creating a post publishes it on Bluesky. Delete it using **Delete this post** before logging out.
> Logout, session expiry and server restarts do not delete posts. A transport failure, interruption or ambiguous create
> response can leave a post on your account. Check Bluesky manually before retrying; the sample never retries writes automatically.
> After a restart or expiry the sample no longer has the reference needed for its delete button; delete the post in Bluesky.

## Prerequisites

* The .NET 10 SDK pinned in `global.json`.
* Node.js matching `^22.22.2 || ^24.15.0 || >=26.0.0` and npm: Node 22.22.2 or later in the 22.x line,
  Node 24.15.0 or later in the 24.x line, or Node 26 or later. Node 24 LTS at version 24.15.0 or later is recommended.
  Node 23 and 25 are not supported by the locked frontend test stack.

No development certificate, public hostname, tunnel or hosted client metadata is needed. This sample uses the special
`http://localhost` OAuth development client and HTTP loopback callbacks, like `Samples.AspNetAuthentication`.
The authorization server must support localhost development clients.

> [!IMPORTANT]
> HTTP and cookies without the Secure flag are appropriate only for this local development demonstration.
> A production app must use HTTPS, Secure cookies and publicly hosted HTTPS OAuth client metadata.
> Do not expose this HTTP development app on a public interface.

## Run the built frontend and backend

Run the following from the repository root:

```powershell
dotnet run --project samples\Samples.ReactBff
```

The `http` launch profile listens at **http://127.0.0.1:5254** and enables browser launch, matching the other ASP.NET sample.
IDEs that honor `launchBrowser` open it automatically; if your CLI does not, browse to that URL manually.
Its build runs `npm ci --ignore-scripts` with the committed lockfile,
then `npm run build`. Vite emits React assets under `wwwroot/lib/app`, already excluded by the repository's existing
generated-asset ignore rule. ASP.NET serves the generated HTML at `/` and its assets at `/lib/app/`.
There is no CDN dependency and no CORS configuration.

Use **127.0.0.1**, not `localhost`, in the browser throughout login: the callback uses the same loopback IP so
the correlation and session cookies stay on the same host. If you previously configured the sample's HTTPS user secrets,
remove the `AllowedHosts`, `BlueskyAgent:OAuthOptions:ClientId`, `ReturnUri` and `ClientUri` overrides before running.

Log in with your Bluesky handle and consent at your authorization server; the sample never asks for a password.
After returning, select **Load timeline**, create a post, delete it, and log out.
The OAuth callback consumes the SDK's protected, browser-bound correlation state and redirects to the fixed `/` URL,
not to an arbitrary browser-supplied return URL. Invalid or expired callbacks redirect with a non-sensitive login error indicator.

The development client ID declares the callback path and maximum scopes in URL-encoded `redirect_uri` and `scope`
query parameters. The configured return URI omits a port; the SDK selects the browser request's port during development.
The authorization server supplies virtual client metadata, so `/oauth-client-metadata.json` is not served.
The sample requests `atproto`,
the granular `app.bsky.feed.getTimeline` RPC scope with the Bluesky AppView audience, and
`repo:app.bsky.feed.post?action=create&action=delete`. It does not request transition scopes, blob permissions or
permission to write other record types. Servers must support granular OAuth scopes.
The localhost client is public, not confidential. For production metadata and confidential client configuration,
see [ASP.NET authentication](asp.net.md) and the [tunnel sample](../../samples/Samples.AspNetTunnelAuthentication/readme.md).

## Optional React development server

The built-asset mode above is the simplest way to run OAuth. For hot reload, keep the backend running on port 5254
and start Vite in a separate terminal:

```powershell
npm run dev --prefix samples\Samples.ReactBff\ClientApp
```

Vite opens **http://127.0.0.1:5173/lib/app/** and proxies `/api` and `/oauth/callback` to ASP.NET.
It preserves the browser's Host header, letting the SDK select port 5173 for the OAuth callback.
The callback redirects to `/`, which Vite redirects to its configured `/lib/app/` base.
The browser has one origin for the UI, cookies and API; no certificates or tunnel configuration are required.
Do not call port 5254 directly from frontend JavaScript, and do not enable credentialed CORS.
Both servers bind to loopback. Vite is for development only; use port 5254 to exercise the built frontend.

## Session and mutation protection

The BFF uses the existing `BlueskySignInManager` for OAuth discovery, redirect preparation and single-use correlation
state, then `BlueskyAgent.ProcessOAuth2LoginResponse` for SDK token exchange and validation.
It does not implement OAuth, token validation, DPoP signing, refresh or revocation itself.
The localhost development client uses virtual metadata generated by the authorization server.

The library's general-purpose identity stores are DID-keyed. This sample instead keeps a separate authenticated agent
for **each browser session**, including independent credentials and proof keys for two browsers using the same DID.
ASP.NET cookie authentication protects a ticket containing only the public DID and a random 256-bit session identifier.
The `ReactBff` cookie is HttpOnly, SameSite=Lax, host-only and path `/`; Lax permits the top-level OAuth callback.
Session, correlation and antiforgery cookies use `SameAsRequest`: they do not require Secure on HTTP loopback,
but receive the Secure flag on HTTPS. They do not use the `__Host-` prefix, which requires Secure.
In a production app, enforce HTTPS and use `CookieSecurePolicy.Always` for all three cookies.
It is a browser-session cookie with a server-enforced **one-hour absolute lifetime**, without sliding renewal.
Signing in rotates the identifier and cleans up any previous session in that browser.

At most 100 live sessions are held. Expired sessions cannot use their agent; a one-minute cleanup loop disposes them,
and shutdown disposes remaining agents. Expiry drops local credentials; it does not prove remote revocation.
The store is single-process and in-memory: **restart signs everyone out**. Nothing is persisted to disk.
An agent's credentials are mutable live objects in server memory, not encrypted durable storage.
These limitations are intentional sample boundaries, not a production-ready session infrastructure.

Each session serializes API operations, refresh, logout and expiry cleanup. Background token refresh is disabled.
Before timeline retrieval or writes, an access token expiring within one minute is refreshed using the existing SDK.
The same agent retains the rotated refresh credentials and DPoP nonce between requests.
A failed or throwing refresh invalidates the session instead of retrying a possibly spent refresh token.
Logout attempts SDK token revocation with a ten-second timeout, then always removes local credentials and expires the cookie,
even if revocation throws. Replacing a session likewise treats revocation of the old session as best effort so a
consumed OAuth callback can still establish the newly authorized session. Remote revocation is best effort and is not
confirmed by a successful logout response.

`GET /api/csrf` returns an ASP.NET antiforgery request token bound to an HttpOnly, SameSite=Strict antiforgery cookie.
The frontend obtains a fresh token before every mutation and sends it in `X-CSRF-TOKEN`.
Login, create, delete and logout validate that token. Logout is POST-only. No mutation is available through GET.
Missing, incorrect or another browser's tokens are rejected before any operation.
Antiforgery tokens are not OAuth credentials and carry no authentication authority.

The delete route accepts **no post URI, DID or record key** from the browser. It uses only the successful create result
stored in the current session, validates that result's repository and collection, and retains the reference when deletion fails.
Only one outstanding sample-created post is allowed per session. Another browser, even for the same DID, cannot use this
session's delete button. This intentionally does not offer arbitrary account post deletion.

Responses are `no-store`; the app sends `Referrer-Policy: no-referrer`, a same-origin content security policy and
`X-Content-Type-Options: nosniff`. JSON request bodies are capped at 16 KiB. HTTP is accepted for loopback development;
production must enforce HTTPS. Inputs are validated server-side, including 300-grapheme and 3000-byte post limits.
API authentication failures return JSON HTTP 401, not redirects to HTML login pages.
Upstream HTTP failures return a fixed operation description and numeric upstream status, not arbitrary upstream error bodies.
Unexpected exceptions produce a generic error and a status-only warning, never an exception payload.
SDK, ASP.NET request and HTTP client logging is suppressed to avoid recording OAuth callback query strings or credentials.
Do not add request-body/URL tracing, browser token storage, or token-bearing claims to cookie tickets.

## Tests and deployment boundaries

```powershell
dotnet build idunno.Bluesky.slnx -c Debug --no-incremental
dotnet test --project test\Samples.ReactBff.Test\Samples.ReactBff.Test.csproj -f net10.0
npm test --prefix samples\Samples.ReactBff\ClientApp
npm run build --prefix samples\Samples.ReactBff\ClientApp
```

The solution build also runs the Vitest suite so frontend tests are part of the same automated build gate as the backend.
Backend tests use ASP.NET TestServer, protected synthetic cookie tickets and an injectable session client.
They exercise endpoint authentication, CSRF, cross-browser session isolation, create/delete restrictions, token-refresh
serialization, absolute expiry, capacity, logout and session-replacement revocation failure cleanup, and error redaction.
They do not perform live OAuth consent.
Existing library OAuth tests cover the SDK primitives the sample calls. Frontend tests cover mutation headers, failure
handling, plain-text rendering, and the React create-then-delete interaction.

Before deploying a real BFF, replace this bounded in-memory agent registry with protected, durable **session-keyed**
credential storage and distributed refresh/nonce/write/logout coordination. Do not substitute a DID-only store if browser
sessions must remain independent. Persist and protect Data Protection keys, use explicit trusted proxy configuration,
add abuse/rate limits and operational monitoring without logging credentials, replace the localhost development client with
public HTTPS OAuth URLs and hosted metadata, require HTTPS with HSTS, and enforce Secure cookies.
Never deploy the Vite development server. See [production configuration](productionConfiguration.md).

Verify live consent, timeline access, creating/deleting a test post, actual token rotation and revocation against the
authorization server using your test account. Automated tests and a clean build do not prove those live interactions.
