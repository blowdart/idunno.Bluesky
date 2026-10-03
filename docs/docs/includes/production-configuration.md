The following settings need attention before deploying an OAuth application to production. ASP.NET-specific requirements apply
when using `idunno.Bluesky.AspNet.Authentication`; other applications must manage their own credential and login-state storage.

## Public URLs and permissions

Use your application's production client ID and callback URL, rather than the special `http://localhost` development client ID.
For a web application, use public HTTPS URLs and publish the client metadata at the configured client ID.
The metadata URL must return HTTP 200 and `application/json` without authentication, redirects or interactive proxy challenges.
Ensure that authorization servers can reach it and that browsers can reach the callback.

Request only the scopes and permission sets the application needs. `atproto` alone is sufficient to identify an account.
If you support progressive authorization, advertise the maximum permissions in the metadata while requesting only the subset needed
for the current operation.

## Persistent sessions and multiple instances

Persist credentials securely, including refreshed credentials and the session's client signing key ID. Do not log access tokens,
refresh tokens, client assertions or private keys.

For ASP.NET applications, replace the default in-memory identity and correlation stores with production stores.
The identity store must retain sessions across application restarts; the correlation store must retain login state until the callback completes.
Persist the ASP.NET Core Data Protection keys used to protect cookies and stored state, and protect those keys at rest.
Losing these keys makes existing protected cookies and state unreadable even if the identity store is persistent.

When running multiple ASP.NET instances, use shared identity and correlation stores, shared Data Protection keys and the same
Data Protection application name. Configure the same authentication scheme and cookie settings on every instance.
Use a store with cross-instance refresh coordination to prevent simultaneous requests from exchanging the same refresh token.
See the [ASP.NET stores documentation](../asp.net.md#stores) for store options, lifetimes and refresh-lock settings.

## Reverse proxies and HTTPS

For ASP.NET applications behind a reverse proxy or tunnel, configure forwarded headers from explicitly trusted proxies or networks.
Process them before authentication so the application sees the correct HTTPS scheme. If forwarded host headers are needed, restrict
the accepted hosts as well; do not trust headers from arbitrary clients. Configure `AllowedHosts` for your public hostname.
Use secure authentication and correlation cookies, and keep TLS certificate validation enabled between the proxy and the application.

Keep insecure-protocol and loopback allowances disabled for production connections unless your deployment specifically requires them.
Do not disable certificate validation to work around a proxy configuration problem.

## Confidential client keys and rotation

For confidential clients, provision an ES256 private key securely and publish only its public key in client metadata.
Store private keys outside source control, restrict access to the application identity and ensure that backups are protected.
All application instances must have the same active and retained signing keys, with matching key IDs and metadata.

Publish a new key on every instance before switching any instance to sign with it. Retain the previous key until sessions using it
can no longer refresh. In ASP.NET, signing keys are loaded and cached: updating key files and configuration requires an application
restart, or a redeployment if they are packaged with the application. Apply each rotation step to every instance.
See [signing key rotation](../asp.net.md#rotating-the-signing-key) for the configuration and sequence.

Additional keys loaded through `AdditionalClientSigningKeyPaths` use their public-key thumbprints as IDs. If an old active key used a
custom `ClientSigningKeyId`, retain it through `AdditionalClientSigningKeys` in code, supplying the original ID to
`OAuthClientSigningKey.FromPemFile(path, keyId)`. Otherwise the saved session ID will not match the retained key.
Outside ASP.NET, load the keys into `ClientSigningKey` and `AdditionalClientSigningKeys` yourself; setting paths alone does not load them.

## Clocks and operational checks

Keep application clocks synchronized. `OAuthOptions.ClientAssertionClockSkew` backdates client assertion `iat` timestamps by
30 seconds by default; it is separate from `OAuthOptions.ClockSkew`, which controls token validation.
Do not use a large allowance to mask a clock synchronization problem, as authorization servers may reject assertions backdated too far.

Exercise sign-in, token refresh and logout against the deployed application. Confirm that refreshed credentials are persisted and that
logout removes the local session and attempts token revocation. ASP.NET logout is best effort: local sign-out can succeed even when
remote revocation fails, so inspect the application logs rather than treating a logged-out page as proof of revocation.
Also check that sessions survive a restart and, for multiple instances, remain usable when subsequent requests reach another instance.

## Discovery validation

> [!WARNING]
> `AtProtoAgent.BuildOAuth2LoginUri` uses discovery mechanisms to resolve the PDS `Uri` and the Authorization Server `Uri`
> for the specified handle. A malicious user could supply a handle which returns URIs that point to internal
> host names or malicious authorization servers. A malicious PDS resolution would cause your application to issue
> requests to the `.well-known/oauth-protected-resource` path against a host name they control.
> A malicious authorization server would redirect the user to login on an authorization server under attacker control,
> but at that point the malicious user is redirecting themselves. Neither of these feel particularly concerning,
> but you should be aware of the possibility if you are writing an application that could be hosted with a
> corporate environment.
>
> `AtProtoAgent.BuildOAuth2LoginUri` accepts two optional parameters, `validatePds` and `validateAuthorizationServer` which
> are both callback methods which you can use to validate the URIs discovered during the building of an OAuth2
> login URI. You can use these methods to mitigate against
> [SSRF](https://owasp.org/www-community/attacks/Server_Side_Request_Forgery) attacks and/or to validate
> the authorization server is one you expect.
>
> A default implementation of discovery validation (`SecurityHelpers.DefaultDiscoveryUriValidator`)
> which rejects any PDS or authorization server that doesn't resolve to a public and safe IP address when
> `BuildOAuth2LoginUri` is called without a `validatePds` or `validateAuthorizationServer` callback,
> or with either of those parameters set to null and the `validateDiscoveredEndpoints` option set to `true`.
