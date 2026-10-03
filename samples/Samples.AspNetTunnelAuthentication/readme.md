# ASP.NET tunnel authentication sample

This sample is meant to run with a reverse tunnel, such as Cloudflare Tunnel, routing a public DNS name to the local application.
It identifies its OAuth client as `idunnoBlueskySample` at `https://dotnet.blue`.

You must use a domain you control, not `dotnet.blue`. Replace `dotnet.blue` in the OAuth client ID,
homepage, callback, privacy policy and terms of service URLs, `AllowedHosts`, the browser launch URL and the tunnel's public hostname with your own domain.

It requests only the `atproto` scope and displays the authenticated identity's DID on the home page, with controls to refresh credentials and log out.
It is a confidential client: it uses authorization code authentication with PKCE, PAR and DPoP, and authenticates to the
authorization server with private_key_jwt client assertions signed by a local ES256 key.

## Running

Create the client signing key (see [Client signing key](#client-signing-key)), then ensure the ASP.NET development HTTPS certificate is installed and trusted:

Install Node.js 20 or later as well. The build uses `npm ci` with the committed `package-lock.json`, then runs
`scripts/copy-client-libraries.js` to copy Bootstrap, jQuery and validation libraries into `wwwroot/lib`.
These locally served assets give the sample and authentication pages the same Bootstrap styling as `Samples.AspNetAuthentication`.

```powershell
dotnet dev-certs https --trust
dotnet run --project samples\Samples.AspNetTunnelAuthentication
```

The application listens on **https://localhost:7253**, using Kestrel's development certificate that is created when the .NET SDK is first used,
or when [dotnet dev-certs https --trust](https://learn.microsoft.com/en-us/dotnet/core/additional-tools/self-signed-certificates-guide) is run.
This fixed port is different from the other ASP.NET samples. No certificate or private key is stored in the repository.

Configure your tunnel's public hostname as your development domain, e.g., **dotnet.blue** and its local HTTPS service as **https://localhost:7253**.
Keep the connector running on the same machine as the application.
The connector must trust the development certificate. For Cloudflare, set the origin server name to `localhost` if necessary,
and configure a trusted CA pool if the connector does not use the Windows trust store.
Keep TLS certificate verification enabled. Do not validate the origin certificate against your public hostname:
the standard ASP.NET development certificate covers localhost, not the public hostname.
Cloudflare provides the public-facing HTTPS certificate separately.

Enable the tunneling and open your public hostname in your browser, e.g., **https://dotnet.blue/** through the tunnel. 
An anonymous request redirects to the Bluesky login page.
After OAuth consent, the callback returns to your application, signs you in,
and redirects to the home page, which displays your DID. No profile or timeline permissions are requested.
Use the public hostname throughout login so the correlation and authentication cookies remain on the same host.

The public name must route to the tunnel from the browser as well as from authorization servers.
Use a DNS resolution setup that reaches the tunnel's public hostname for the OAuth flow;
loopback records alone do not expose the application to authorization servers.
Direct access to `https://localhost:7253/` can check that the server is running, but do not start login there.

### Refreshing and logging out

The home page displays your DID, access token expiration time (UTC) and session's client signing key ID, without exposing tokens or private keys.
Select **Refresh credentials** to make a token refresh request using a signed client assertion. The agent factory persists the updated
credentials to the identity store, and the page confirms success and shows the updated expiration time. Refresh failures are displayed on
the page; check the application logs for details.

Select **Log out** on the home page or in the header to submit directly to the package's logout handler, without a confirmation page.
Logout attempts to revoke the refresh and access tokens using signed client
assertions, then clears the stored session and authentication cookie. Revocation is best effort: local logout still completes if the
authorization server is unavailable, so check the application logs to confirm the revocation requests succeeded.
Refresh and logout submit antiforgery-protected POST requests. Neither action requires additional scopes.

## Authentication and metadata

A fallback authorization policy protects application endpoints unless explicitly marked anonymous. The package's login, callback and error pages allow
anonymous access so the OAuth flow can complete. Its UI static assets are also anonymous.

The metadata middleware runs before authorization and serves
**/oauth-client-metadata.json** without requiring a login.
The Bootstrap-styled `/privacy` and `/tos` Razor Pages are also anonymous. Their public HTTPS URLs are configured as
`OAuthOptions.PolicyUri` and `TosUri` and advertised in the generated metadata.
These pages describe the development sample, not production policies; replace their text with your own policies before deployment.

Do not place Cloudflare Access authentication or interactive bot challenges in front of the metadata or OAuth flow endpoints.
Authorization servers must be able to fetch metadata with HTTP 200 and `application/json`, without redirects or a browser session.
Forwarded protocol and client-address headers are accepted only from ASP.NET's default trusted loopback proxies;
the sample does not trust arbitrary forwarded hosts.

## Client signing key

`New-ClientSigningKey.ps1` creates the ES256 (ECDSA P-256) key pair the sample authenticates with.
It requires PowerShell 7.4 or later:

```powershell
.\samples\Samples.AspNetTunnelAuthentication\New-ClientSigningKey.ps1
```

The script writes the private key to `client-signing-key.pem` (PKCS#8 PEM) and the public key to
`client-signing-key.jwk.json` in a `.blueskyDotnet` folder in your user profile directory, outside the repository.
The JWK's `kid` is its RFC 7638 thumbprint. Existing keys are not replaced unless you pass `-Force`.
Keep the private key secret.

To create a new key pair alongside existing keys for rotation, use `-NewKey`:

```powershell
.\samples\Samples.AspNetTunnelAuthentication\New-ClientSigningKey.ps1 -NewKey
```

This creates uniquely named `client-signing-key-<identifier>.pem` and `.jwk.json` files without replacing existing keys.
The script prints JSON settings to merge into `BlueskyAgent:OAuthOptions`, with the new key as `ClientSigningKeyPath`
and other `client-signing-key*.pem` files in that directory as `AdditionalClientSigningKeyPaths`. Paths under the user's profile
use `~/` in the JSON; paths outside it remain absolute. Review the retained paths.
First publish the new key as an additional key while keeping the current active key; then promote it using the printed settings.
Restart or redeploy after each step, and retain previous keys until their sessions expire.
`-NewKey` cannot be combined with `-Force`.

The sample's `appsettings.json` sets `BlueskyAgent:OAuthOptions:ClientSigningKeyPath` to `~/.blueskyDotnet/client-signing-key.pem`.
On startup the key is loaded from that file and set as `OAuthOptions.ClientSigningKey`; the sample fails to start if the file is missing.
Change `ClientSigningKeyPath` to load the key from somewhere else. You can also set an optional `ClientSigningKeyId` to publish instead of the thumbprint.
The generated metadata then publishes the public key in `jwks`, with `token_endpoint_auth_method` set to `private_key_jwt`,
and each PAR, token, refresh and revocation request carries a signed client assertion.
The metadata `kid` is the same RFC 7638 thumbprint the script writes to the JWK file.
OAuth sessions are bound to the signing key. If you replace the key, existing sessions can no longer be refreshed and users must sign in again.

The default identity and correlation stores are in-memory and intended for development. Restarting the application loses sessions.
This sample does not fetch profile details, publish content or persist credentials to a database.
