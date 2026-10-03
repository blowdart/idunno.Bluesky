# Using idunno.Bluesky with ASP.NET

<a name="agentFactory"></a>

## The Bluesky Agent client factory

The `BlueskyAgentFactory` allows you to request a configured `BlueskyAgent` through ASP.NET's dependency injection.

To use the factory call `AddBlueskyAgentFactory` during your application configuration, for example

```c#
builder.Services.AddBlueskyAgentFactory();
```

You can then request an injected `BlueskyAgent` during your page constructor.

```c#
public class IndexModel(BlueskyAgent agent) : PageModel
```

> [!TIP]
> The BlueskyAgent is scoped to the request. Make sure all your On* methods are async and return a Task, eg. `public async Task<IActionResult> OnGet()`.

If you use the `idunno.Bluesky.AspNet.Authentication` to authenticate against Bluesky the injected factory will be authenticated if the current request is authenticated.

## Production Configuration

[!include[Production configuration](includes/production-configuration.md)]

## Authenticating with Bluesky

`idunno.Bluesky.AspNet.Authentication` provides an ASP.NET Core authentication handler for Bluesky, which is based on OAuth. Accompanying this is a Razor Pages default
UI package, `idunno.Bluesky.AspNet.Authentication.UI`, which provides login and logout pages, as well as code to process the OAuth callback.

`Samples.AspNetAuthentication` is a Razor Pages sample application that demonstrates how to use the authentication handler, and how an injected agent can be used to
perform authenticated operations.

### Adding Bluesky Authentication to your ASP.NET Razor Pages application

To add simple authentication to your razor pages application first add a reference to the `idunno.Bluesky.AspNet.Authentication` and `idunno.Bluesky.AspNet.Authentication.UI` packages to your project.

Next open your `program.cs` file and add the following to the app configuration, before the call to `builder.Services.AddRazorPages()`

```c#
builder.Services
    .AddAuthentication(BlueskyAuthenticationDefaults.AuthenticationScheme)
    .AddBluesky()
    .AddBlueskyAuthenticationUI();

builder.Services
    .AddBlueskyClaimsTransformer()
    .AddBlueskyAgentFactory();
```

This configures Bluesky authentication with in-memory stores, suitable for development use.

Next add the authentication middleware to your request pipeline, before the authorization middleware.

```c#
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();
```

> [!IMPORTANT]
> Without `app.UseAuthentication()` the authorization middleware only authenticates requests to endpoints which carry an authorization policy, so `User` is
> anonymous everywhere else. Pages which have no `[Authorize]` attribute, including the layout and login partial shown below, would keep rendering the signed
> out state even after a successful login.

Next, in your `appsettings.json` or `appsettings.Development.json` add configuration for the agent,

```json
    "BlueskyAgent": {
        "EnableBackgroundTokenRefresh": false,

        "OAuthOptions": {
            "ClientId": "http://localhost?redirect_uri=http://127.0.0.1/Bluesky/Callback&scope=atproto%20transition:generic",
            "ReturnUri": "http://127.0.0.1/Bluesky/Callback",
            "Scopes": [ "atproto", "transition:generic" ]
        }
    }
```

Note that the client ID and return URI are both http. When you run the application you should use the http profile, or edit the URIs to match your HTTPS configuration.
In production these will be configured with your production values.

Now add login and logout links. In the standard ASP.NET Razor Pages templates these are defined in `_LoginPartial.cshtml`. For example,

```html
@using idunno.Bluesky.AspNet.Authentication

<ul class="navbar-nav">
    @if (User.Identity?.IsAuthenticated ?? false)
    {
        <li class="nav-item">
            @if (User.GetAvatar() is not null)
            {
                <img src="@User.GetAvatar()" alt="Avatar" class="rounded-circle" width="30" height="30" />
            }
            else if (User.GetDisplayName() is not null)
            {
                <span>Hello @User.GetDisplayName()!</span>
            }
            else
            {
                <span>Hello @User.Identity?.Name!</span>
            }
        </li>
        <li class="nav-item">
            @{
                var logoutReturnUrl = Url.Page("/Index", new { area = "" });
            }
            <form class="d-inline" asp-area="Bluesky" asp-page="/Logout" asp-route-returnUrl="@logoutReturnUrl" method="post">
                <button type="submit" class="nav-link btn btn-link text-dark">Logout</button>
            </form>
        </li>
    }
    else
    {
        <li class="nav-item">
            <a class="nav-link text-dark" asp-area="Bluesky" asp-page="/Login">Login</a>
        </li>
    }
</ul>
```

Note the default area for the authentication pages is `Bluesky`.

Decorate a page with `[Authorize]`, or make your entire app require authentication, and run it. When you hit an endpoint that requires an authenticated user you should be
sent to the login page, where you enter your handle, bounced through the Bluesky OAuth login page, and back to your application where authentication will happen.

If you have injected a Bluesky agent using the [BlueskyAgentFactory](#agentFactory) you will see that it is now authenticated.

### Publishing OAuth client metadata

Production OAuth clients must publish a [client metadata document](https://atproto.com/specs/oauth#client-id-metadata-document)
at the exact URL configured as their `OAuthOptions.ClientId`. You can generate and serve this document without maintaining a JSON file.

Configure the agent's production OAuth settings and optionally configure metadata branding:

```c#
builder.Services.Configure<BlueskyAgentOptions>(options =>
{
    options.OAuthOptions = new OAuthOptions(
        "https://app.example.com/oauth-client-metadata.json",
        new Uri("https://app.example.com/Bluesky/Callback"),
        ["atproto", "transition:generic"])
    {
        ClientName = "My Bluesky application",
        ClientUri = new Uri("https://app.example.com/"),
        TosUri = new Uri("https://app.example.com/terms"),
        PolicyUri = new Uri("https://app.example.com/privacy")
    };
});

builder.Services.AddBlueskyOAuthClientMetadata();
```

`OAuthOptions` is in `idunno.AtProto.Authentication`; `BlueskyAgentOptions` is in `idunno.Bluesky`.
Keep your existing `AddBluesky()` authentication registration.

The optional `ClientName`, `ClientUri`, `TosUri` and `PolicyUri` properties belong to `OAuthOptions`, alongside `ClientId` and `ReturnUri`.
They can also be bound from the existing agent configuration:

```c#
builder.Services.AddBlueskyOAuthClientMetadata();
builder.Services.Configure<BlueskyAgentOptions>(
    builder.Configuration.GetSection("BlueskyAgent"),
    options => options.ErrorOnUnknownConfiguration = true);
```

For example, in `appsettings.json`:

```json
{
  "BlueskyAgent": {
    "OAuthOptions": {
      "ClientId": "https://app.example.com/oauth-client-metadata.json",
      "ReturnUri": "https://app.example.com/Bluesky/Callback",
      "Scopes": [ "atproto", "transition:generic" ],
      "ClientName": "My Bluesky application",
      "ClientUri": "https://app.example.com/",
      "TosUri": "https://app.example.com/terms",
      "PolicyUri": "https://app.example.com/privacy"
    }
  }
}
```

After building the app, add the publishing middleware **before** authentication, authorization, static files and anything else that
could intercept the metadata URL:

```c#
app.UseBlueskyOAuthClientMetadata();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
```

The middleware serves anonymous GET requests with HTTP 200 and `application/json`, supports HEAD, and rejects other methods
at the metadata URL with HTTP 405. Other URLs continue through the existing pipeline. It uses the path and query of the configured
client ID, not the request host, to select requests; the document's URLs never come from untrusted request headers.
If you use `UsePathBase()`, call it before the metadata middleware and include the public path base in the client ID.
Ensure that the configured HTTPS URL is publicly reachable and is not redirected by your proxy or other middleware.

By default the generated document describes a **public web client** (`token_endpoint_auth_method: none`) with DPoP, authorization code
and refresh token support. Set `OAuthOptions.ClientSigningKey` to describe a [confidential client](#confidential-clients) instead.
Native-client metadata is not supported.
The client ID must use HTTPS with no explicit port, credentials or fragment. The callback must use HTTPS with no credentials,
fragment or explicit default port. The homepage must share the client ID's hostname; logo, terms and privacy URLs must use HTTPS.

Scopes include `OAuthOptions.Scopes` and its typed `PermissionSets`, deduplicated with ordinal comparison.
Permission sets can be configured in either of two equivalent ways. For example, to request `atproto` and Bluesky's
read-only `ViewAll` permission set, put this under `BlueskyAgent:OAuthOptions`:

**Structured permission sets** keep the NSID and audience readable:

```json
"Scopes": [ "atproto" ],
"PermissionSets": [
  {
    "Nsid": "app.bsky.authViewAll",
    "Audience": "did:web:api.bsky.app#bsky_appview"
  }
]
```

`Audience` is optional when the permission set does not need an inherited RPC audience. If provided, it must be a DID with
a service fragment, or `*`. The NSID is validated during binding, and the audience is validated by the permission-set constructor.
Use `ErrorOnUnknownConfiguration = true` as shown above so invalid or incomplete collection entries fail configuration
instead of being skipped by the configuration binder. Strings are converted to validated `Nsid` values by a type converter;
the resulting `OAuthPermissionSet` objects remain immutable.

**Raw scopes** encode the same permission-set reference directly:

```json
"Scopes": [
  "atproto",
  "include:app.bsky.authViewAll?aud=did%3Aweb%3Aapi.bsky.app%23bsky_appview"
]
```

Both approaches produce the same OAuth requests and generated metadata. Structured permission sets percent-encode the audience
automatically; raw scopes must already contain the encoded audience. You can combine raw scopes and structured sets, and identical
scope strings are included only once. Code-based configuration still supports
`options.OAuthOptions.PermissionSets = [BlueskyOAuthPermissionSets.ViewAll]`.

If the app may request additional scopes later, add each to `BlueskyOAuthClientMetadataOptions.AdditionalScopes` so the document
advertises every scope the app might request. This does not change initial login scopes. Scope tokens must be valid OAuth scope
tokens and the advertised set must include `atproto`.

Metadata is validated and generated once when the pipeline is configured; invalid settings fail app startup. Restart the app after changing
OAuth or metadata settings. Publication is opt-in and does not affect apps which already host a static metadata file.
For local development, continue using the special `http://localhost` client ID: the authorization server supplies its virtual metadata,
so do not enable this publishing middleware with that ID.

#### Confidential clients

A confidential client authenticates to the authorization server with a signed client assertion (`private_key_jwt`),
which gives longer-lived sessions than a public client. Create an ES256 (ECDSA P-256) key pair, keep the private key out of source control,
and set its path in the `OAuthOptions` configuration:

```json
"BlueskyAgent": {
  "OAuthOptions": {
    "ClientId": "https://example.com/oauth-client-metadata.json",
    "ClientSigningKeyPath": "~/.blueskyDotnet/client-signing-key.pem"
  }
}
```

The key file must contain an unencrypted PKCS#8 or SEC 1 P-256 private key. Environment variables in the path are expanded,
a leading `~` is replaced with the user's profile directory, and a relative path is resolved against the application's content root.
The key ID defaults to the RFC 7638 thumbprint of the public key; set an optional `ClientSigningKeyId` to publish your own.
You can also set `options.OAuthOptions.ClientSigningKeyPath` in code.

`AddBluesky()`, `AddBlueskyOAuthClientMetadata()`, `AddBlueskyAgentFactory()` and `AddBlueskyClaimsTransformer()` load the key,
and set it as `OAuthOptions.ClientSigningKey`, when the agent options are first resolved. If `ClientSigningKey` is already set, the path is ignored.
`UseBlueskyOAuthClientMetadata()` does this at startup, so a missing or invalid key file stops the application from starting.
Outside ASP.NET, create the key with `OAuthClientSigningKey.FromPemFile()` or `OAuthClientSigningKey.FromPem()` and set
`OAuthOptions.ClientSigningKey` yourself.

With a signing key set:

* The generated metadata publishes the public key in `jwks` and declares `token_endpoint_auth_method: private_key_jwt`
  and `token_endpoint_auth_signing_alg: ES256`. The private key is never published.
* Pushed authorization, token, refresh and revocation requests carry a fresh client assertion, with the client ID as issuer and subject
  and the authorization server's issuer as audience.

`OAuthOptions.ClientAssertionClockSkew` backdates the `iat` timestamp of client assertions by 30 seconds by default, to accommodate
small clock differences with the authorization server. It applies to pushed authorization, token, refresh and revocation requests;
assertions still expire one minute after creation. Configure it in code with `TimeSpan.FromSeconds(...)`, or in configuration as
`"ClientAssertionClockSkew": "00:00:30"` under `BlueskyAgent:OAuthOptions`. Zero disables backdating; negative values are rejected.
This is separate from `OAuthOptions.ClockSkew`, which controls token validation. Keep the system clock synchronized rather than
using a large allowance, as authorization servers may reject assertions backdated too far.

Localhost development client IDs are always public clients, and setting a signing key, or additional signing keys, with one fails validation.

##### Rotating the signing key

Sessions are bound to the key they started with. Pending logins retain the PAR signing key ID in `OAuthLoginState.ClientSigningKeyId`
so a callback after rotation uses the same key. Each session records the key ID in its credentials (`ClientSigningKeyId`),
and the ASP.NET identity stores persist it as the `urn:atproto:oauth:signingkey` claim. Refresh and revocation requests sign with the
session's key, so an older key must stay available until the sessions that use it have expired.

`OAuthOptions.AdditionalClientSigningKeyPaths` (or `AdditionalClientSigningKeys` in code) holds those older keys. They are published
in `jwks` alongside the active key and used only for sessions that started with them; new sign-ins always use the active key.
Additional keys loaded from a path use the key thumbprint as their key ID, and every key ID must be unique.

To rotate a key:

1. Create a new key and add its path to `AdditionalClientSigningKeyPaths`, so it is published before it is used.
2. Make the new key the active `ClientSigningKeyPath`, and move the old key's path to `AdditionalClientSigningKeyPaths`.
3. Remove the old key once the refresh tokens issued with it have expired.

```json
"BlueskyAgent": {
  "OAuthOptions": {
    "ClientSigningKeyPath": "~/.blueskyDotnet/client-signing-key-2.pem",
    "AdditionalClientSigningKeyPaths": [ "~/.blueskyDotnet/client-signing-key.pem" ]
  }
}
```

> [!NOTE]
> Signing keys are loaded into memory and cached; changes to key files are not watched. Each rotation step that changes the configured keys
> requires an application restart to take effect.
>
> If key files and configuration are managed separately from the deployment, place the new key on the server, update `ClientSigningKeyPath`
> and `AdditionalClientSigningKeyPaths`, then restart the application. No rebuild or redeployment is needed.
>
> If key files or configuration are packaged with the application, deploy the updated package and restart the application as part of the deployment.
> In either approach, keep the previous key available for existing sessions, and apply the changes to every application instance.

A session with no recorded key ID, or with the ID of a key that is no longer configured, uses the active key. A warning is logged
for an unknown key ID, and the authorization server may reject the request, in which case the user must sign in again.

`Samples.AspNetTunnelAuthentication` is a confidential-client sample. Its `New-ClientSigningKey.ps1` script creates a key pair
in a `.blueskyDotnet` folder in your user profile, which the sample loads at startup.

To generate the JSON yourself, without the publishing middleware, call
`new BlueskyOAuthClientMetadataOptions().GenerateJson(oAuthOptions)`. This uses the same validation and source-generated serialization.

`Samples.AspNetClientMetadata` is a minimal metadata-only sample. Run it with
`dotnet run --project samples\Samples.AspNetClientMetadata` and browse to `http://127.0.0.1:5252/`.
The home page redirects to `/oauth-client-metadata.json`, showing a document configured for `https://example.org`
with the `atproto` scope and `BlueskyOAuthPermissionSets.ViewAll` permission set.
Its client name and homepage URL are bound from `BlueskyAgent:OAuthOptions` in `appsettings.json`.
It also binds the structured `PermissionSets` array from that section, with no permission-set construction in the sample code.
The local address is only a preview: the document advertises the example.org URLs, not localhost.
The sample does not implement login or the advertised callback; replace the example URLs and implement the callback before using it for OAuth.

### Changing the appearance of the UI pages

The pages in `idunno.Bluesky.AspNet.Authentication.UI` render inside a plain, self contained layout that the package ships, styled by a small stylesheet the package
also ships. This means the pages work in any application, including a minimal one with no layout of its own. The stylesheet is served as a static web asset, so your
application must call `app.MapStaticAssets()`, or `app.UseStaticFiles()` on earlier versions of ASP.NET Core, for the pages to pick up their styling.

Most applications will want the authentication pages to look like the rest of the site. Files in your application take precedence over identically pathed files in a
Razor Class Library, so you can replace the package's layout by adding your own `_ViewStart.cshtml` at `Areas/Bluesky/Pages/_ViewStart.cshtml`, pointing at whichever
layout you want to use.

```c#
@{
    Layout = "/Pages/Shared/_Layout.cshtml";
}
```

`Samples.AspNetAuthentication` does exactly this, which is why its login and logout pages carry the same navigation bar as the rest of the sample.

The markup in the pages uses Bootstrap class names, so an application whose layout loads Bootstrap will style them without any further work. The package's own
stylesheet is only referenced by the package's layout, so once you supply your own layout it is never loaded and cannot conflict with your styles.

### Stores

Bluesky authentication requires two different stores, an identity store and a correlation state store. The identity store keeps the tokens issued by Bluesky for
authentication. The correlation state store keeps the information needed during the OAuth login flow.

By default two ephemeral, in memory, stores are used, so you can develop your application without standing up any extra infrastructure. These stores are not
suitable for production use as they store a limited number of identities and are not persistent, so when your application restarts any authenticated users will
be logged out.

Persistent store implementations are provided for SQLite, MySQL and Redis, in the `idunno.Bluesky.AspNet.Authentication.SQLite`,
`idunno.Bluesky.AspNet.Authentication.MySQL` and `idunno.Bluesky.AspNet.Authentication.Redis` packages. If none of those suit your deployment you can implement
your own using the `IIdentityStore` and `ICorrelationStateCache` interfaces.

> [!IMPORTANT]
> The SQL stores do not create their own schema. You must create the database and its tables before your application starts, otherwise the first request which
> touches a store will fail.
>
> The `idunno.Bluesky.AspNet.Authentication.SQLite` package includes `New-AuthenticationDatabase.ps1`, which creates an empty database named
> `idunno.Bluesky.AspNet.Authentication.db` in the directory you give it. It needs `sqlite3` on your `PATH`.
>
> ```powershell
> ./New-AuthenticationDatabase.ps1 -OutputDirectory ./App_Data
> ```
>
> The `idunno.Bluesky.AspNet.Authentication.MySQL` package includes `schema.sql`, which you apply to an existing database with the MySQL client of your choice.
>
> Both files are added to your project as content when you install the package. Redis needs no schema.

To use a persistent store configure it during authentication configuration in `Program.cs`. For example, to use SQLite,

```c#
using idunno.Bluesky.AspNet.Authentication;
using idunno.Bluesky.AspNet.Authentication.SQLite;

string connectionString = builder.Configuration.GetConnectionString("BlueskyAuthentication")!;

builder.Services
    .AddAuthentication(BlueskyAuthenticationDefaults.AuthenticationScheme)
    .AddBluesky(options =>
    {
        options.IdentityStore = new SqliteIdentityStore(connectionString);
        options.CorrelationStateCache = new SqliteCorrelationStateCache(connectionString);
    });
```

MySQL and Redis are configured the same way, using `MySqlIdentityStore` and `MySqlCorrelationStateCache` from the
`idunno.Bluesky.AspNet.Authentication.MySQL` namespace, or `RedisIdentityStore` and `RedisCorrelationStateCache` from the
`idunno.Bluesky.AspNet.Authentication.Redis` namespace.

#### Expiry and housekeeping

Entries in both stores expire. Expiry is applied as a filter when an entry is read, so an expired entry is never returned, and the SQL stores also delete
expired rows so the tables do not grow without limit. Each store constructor takes optional settings which control this.

| Setting | Applies to | Default | Purpose |
| --- | --- | --- | --- |
| `entryTimeToLive` | Identity stores | 7 days | How long a stored identity lives before it is treated as expired. |
| `refreshLockLength` | Identity stores | 90 seconds | How long a token refresh lock is held before another instance may take it over. |
| `entryTimeToLive` | Correlation state caches | 15 minutes | How long a login in flight may take before its state is discarded. |
| `expiredEntrySweepInterval` | SQLite and MySQL stores | 5 minutes | How often expired rows are deleted. |

For example,

```c#
options.IdentityStore = new SqliteIdentityStore(
    connectionString,
    entryTimeToLive: TimeSpan.FromDays(30),
    expiredEntrySweepInterval: TimeSpan.FromMinutes(15));
```

Deletion of expired rows happens as part of a write, throttled to no more than once per `expiredEntrySweepInterval`, so a store which is never written to is
never swept. Setting `expiredEntrySweepInterval` to `TimeSpan.Zero` turns deletion off completely. Expired entries are still never returned, but removing them
becomes your responsibility. The Redis stores have no sweep setting, as Redis expires keys itself.

> [!NOTE]
> The `IdentityStoreEntryTimeToLive` and `RefreshLockLength` properties on `BlueskyAuthenticationOptions` apply to the default in memory store and to
> `DistributedCacheIdentityStore`. The SQLite, MySQL and Redis stores take their lifetimes from their own constructors, so if you supply one of those these
> properties are ignored and you should set the lifetimes on the constructor instead.
>
> `IdentityStoreEntryTimeToLive` defaults to the authentication cookie's `ExpireTimeSpan`. If you shorten a lifetime so that stored credentials expire before
> the cookie does, users will be signed out early, and a warning is logged.

> [!IMPORTANT]
> Information persisted in the identity store is sensitive and is, by default, protected using the ASP.NET Core Data Protection API.
> This means that if you run your application on multiple servers you must configure data protection to use a common key store, a static
> application name and if you change the key store or run your application on a different server all previously persisted identities will be invalidated,
> causing your users to be logged out.
> 
> In addition you should use data protection providers that match the authentication persistence providers such as the generic
> [Entity Framework](https://learn.microsoft.com/en-us/aspnet/core/security/data-protection/configuration/overview) provider or a more specific
> provider such as [AspNetCore.DataProtection.MySql](https://www.nuget.org/packages/AspNetCore.DataProtection.MySql).
> 
> To configure data protection see the [Microsoft documentation](https://learn.microsoft.com/en-us/aspnet/core/security/data-protection/configuration/overview).
>
> You can turn off [data protection](https://learn.microsoft.com/en-us/aspnet/core/security/data-protection/) by overriding the identity store
> and correlation state cache events. This is not recommended as your users' access, refresh and DPoP tokens will no longer be protected at rest.
> ```c#
> 
> using idunno.Bluesky.AspNet.Authentication.Events;
> 
> builder.Services.AddAuthentication()
>     .AddBluesky(options =>
>     {
>         options.IdentityStoreEvents = new IdentityStoreEvents();
>         options.CorrelationStateCacheEvents = new CorrelationStateCacheEvents();
>     });
>```

### Using claims transformation to supplement the identity

`BlueskyClaimsTransformer` is provided to enhance the `ClaimsIdentity` issued during authentication. It requests the profile for the authenticated user, and caches it, adding
claims from the profile to the identity. The potential claims are defined in `idunno.Bluesky.ClaimTypes`. They include

* `idunno.Bluesky.ClaimTypes.Handle` - the user's Bluesky handle,
* `idunno.Bluesky.ClaimTypes.DisplayName` - the user's display name,
* `idunno.Bluesky.ClaimTypes.Description` - the user's description, from their profile,
* `idunno.Bluesky.ClaimTypes.Pronouns` - the user's pronouns, from their profile,
* `idunno.Bluesky.ClaimTypes.Website` - the user's website, from their profile,
* `idunno.Bluesky.ClaimTypes.Avatar` - a URI to the user's uploaded avatar and
* `idunno.Bluesky.ClaimTypes.Banner` - a URI to the user's profile banner.

[!include[Untrusted data warning](includes/untrusted-data-warning.md)]

To use the transformer configure it in your application in `Program.cs` like so;

```c#
builder.Services
    .AddBlueskyClaimsTransformer();
```

`AddBlueskyClaimsTransformer()` registers the transformer as an `IClaimsTransformation` for you, so you do not need to register it yourself.

### OAuth and DPoP

Bluesky authentication is OAuth authentication with some extra layers, including a version of
dynamic client registration requiring a [client metadata document](https://atproto.com/specs/oauth#client-id-metadata-document),
and [DPoP](https://datatracker.ietf.org/doc/html/rfc9449) to allow for protection against token theft.
This does mean you can't use the in-box OAuth authentication handlers for ASP.NET Core.

DPoP and the OAuth flow both need state to be kept on the server, and that state is held in the two stores described in [Stores](#stores):

* The **identity store** holds the access token, the refresh token and the DPoP proof key for an authenticated user.
* The **correlation state cache** holds the PKCE verifier and the DPoP private key for a login which is still in flight.

A login in flight is tied to its correlation state by a short lived cookie. That cookie is named per authentication scheme and per login, so a user can have
more than one login in flight at once, in two browser tabs for example, without one overwriting another. The name of a login's cookie is derived from the OAuth
`state` parameter, which is why a callback which does not carry exactly one `state` parameter is rejected. The cookie's contents, not its name, decide which
correlation state is used, so the name being derived from the request does not let a caller reach a login it did not start.

> [!NOTE]
> Cookies for abandoned logins are not deleted when a new login starts, as deleting them would break any login still in flight. They expire on their own,
> at the same time as the correlation state they point at, controlled by the correlation state cache's `entryTimeToLive`.

Both are encrypted at rest with the ASP.NET Core Data Protection API. If you run on more than one server, choose store implementations which every instance can
reach, and configure data protection with a shared key store, otherwise a login which starts on one server cannot be completed on another.

> [!TIP]
> If your application already has an `IDistributedCache` you can use it for either store, with `DistributedCacheIdentityStore` and
> `DistributedCacheCorrelationStateCache`, rather than adding SQLite, MySQL or Redis specifically for authentication.

> [!TIP]
> Bluesky special cases the `http://localhost` ClientId, to allow for local development of client applications, including
> web clients. If you want to test your application whilst running as `localhost`, for example, within Visual Studio,
> you must set the `ReturnUri` to "http://127.0.0.1" in configuration. For example
> ```json
>"BlueskyAgent": {
>  "OAuthOptions": {
>    "ClientId": "http://localhost",
>    "ReturnUri": "http://127.0.0.1",
>    "Scopes": [ "transition:generic" ]
>  }
>}
> ```
> You should also change your `applicationUrl` in `launchSettings.json` from `localhost` to `127.0.0.1`. For example
> ```json
> {
>   "$schema": "https://json.schemastore.org/launchsettings.json",
>   "profiles": {
>     "http": {
>       "commandName": "Project",
>       "dotnetRunMessages": true,
>       "launchBrowser": true,
>       "applicationUrl": "http://127.0.0.1:5145",
>       "environmentVariables": {
>         "ASPNETCORE_ENVIRONMENT": "Development"
>       }
>     }
>   }
> }
>```

### Debugging OAuth Pushed Authorization Requests

Bluesky uses Pushed Authorization Requests ([PAR](https://datatracker.ietf.org/doc/html/rfc9126)) as a security measure.
PAR works by pushing the authorization via a back channel to the authorization server rather than using the browser.

You can intercept and monitor PAR requests by using a proxy like [Fiddler](https://www.telerik.com/fiddler).

To configure the Bluesky agent to use a proxy for outgoing requests, including PAR requests use your
app configuration, for example

```json
{
    // Other settings removed for clarity

    "BlueskyAgent": {
        "EnableBackgroundTokenRefresh": false,

        "HttpClientOptions": {
            "ProxyUri": "http://localhost:8866/",
            "CheckCertificateRevocationList" : true
        }
    }
}
```

> [!WARNING]
> You may need to set `CheckCertificateRevocationList` to `false`, depending on how
> your proxy generates proxy HTTP certificates. This does introduce a security vulnerability and should not be
> used on production systems, only during development.

## Enabling Logging

ASP.NET's dependency injection allows you to inject an `ILoggerFactory` into your page constructors or services.
You should then pass this into the `BlueskyAgent` constructor.

For example, with Razor Pages this could look like the following

```c#
public class MyPage_Model(ILoggerFactory loggerFactory) : PageModel
{
    /// Code removed

    public async Task<IActionResult> OnPost()
    {
        /// After validating the model, create a BlueskyAgent to do something

        using (var agent = new BlueskyAgent(
            options: new BlueskyAgentOptions()
            {
                LoggerFactory = loggerFactory,
            }))
        {
            /// Now do what you need to do with agent.
        }

        return Page();
    }
}
```

## Using appsettings.json

You can bind a configuration section into the asp.net dependency injection container using `builder.Services.AddBlueskyAgentOptions()`.

For example:

```
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddBlueskyAgentOptions();
```

By default the configuration section is named `BlueskyAgent`, and will use the configuration Logging settings.

An example `appsettings.json` file might look like the following:

```json
{
    "Logging": {
        "LogLevel": {
            "Default": "Information",
            "idunno.AtProto": "Debug",
            "idunno.Bluesky" : "Debug"
        }
    },

    "Console": {
        "FormatterName": "Simple",
        "FormatterOptions": {
            "SingleLine": true,
            "IncludeScopes": true,
            "TimestampFormat": "HH:mm:ss ",
            "UseUtcTimestamp": true
        },
        "IncludeScopes": true
    },

    "BlueskyAgent": {
        "EnableBackgroundTokenRefresh": false
    }
}
```

> [!TIP]
> You cannot set the `IFacetExtractor` or `JsonSerializerOptions` from an appsettings file.
> If you want to override either of these manually, create an instance of `BlueskyAgentOptions`
> and pass it to `builder.Services.AddBlueskyAgentOptions()`.
