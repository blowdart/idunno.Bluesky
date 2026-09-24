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
            <form class="form-inline" asp-area="Bluesky" asp-page="/Logout" asp-route-returnUrl="@logoutReturnUrl" method="post">
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

> [!WARNING]
> You should validate, and protect against XSS when rendering these values on a web page, as they are freeform text (except for avatar and banner).

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
