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

## Rendering and authentication

The app uses **static server-side rendering**, not Interactive Server or WebAssembly. Its pages and forms are Razor
components; login, logout and the OAuth callback are the library's Razor Pages, styled with a local layout.
No npm packages, external scripts or browser-side OAuth credentials are needed.

`AddRazorComponents()`, `AddCascadingAuthenticationState()` and `AuthorizeRouteView` expose the authenticated request's
principal to components. `[Authorize]` protects both GETs and POSTs to the claims and profile pages.
`AddBlueskyAgentFactory()` supplies a request-scoped authenticated agent. Each navigation and form submission makes
a new HTTP request, allowing the authentication handler to refresh credentials and renew cookies before rendering.
Do not simply add an interactive render mode: a long-lived circuit does not provide a current HTTP request or allow
cookie updates, and an agent created from `IHttpContextAccessor` is not a circuit authentication solution.

`EditForm` uses a named SSR form and `SupplyParameterFromForm` to bind POSTs, includes an antiforgery token automatically,
and validates both grapheme and UTF-8 limits on the server. Empty profile fields can be cleared. Saves retain the original
CID, preserve other profile fields, and send a conditional update to avoid overwriting concurrent edits.
HTTP errors are shown instead of being treated as empty data or successful saves.

## Configuration

The HTTP launch profile enables Development settings. Its localhost client ID declares the same FullApp permissions
as the authorization request, and the callback uses HTTP loopback with the browser's port.
Keep `EnableBackgroundTokenRefresh` disabled for request-scoped agents.
Distinct cookie names and a distinct data protection application name isolate the sample from the other web samples
(cookies are shared across ports on the same host).

Production settings contain **placeholder** HTTPS client and callback URLs. Set them to your deployed URLs, update
`AllowedHosts`, and publish matching metadata (the middleware serves `/oauth-client-metadata.json` for hosted clients).
The default in-memory identity/correlation stores are only for development and lose sessions on restart.
Before deployment, configure persistent protected stores, shared data protection keys, HTTPS and trusted proxy handling.
See the [ASP.NET guide](../../docs/docs/asp.net.md) and [production configuration](../../docs/docs/productionConfiguration.md).
