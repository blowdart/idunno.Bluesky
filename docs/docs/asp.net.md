# Using idunno.Bluesky with ASP.NET

<a name="agentFactory></a>## The Bluesky Agent client factory

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

`idunno.Bluesky.AspNet.Authentication` provides an ASP.NET Core authentication handler for Bluesky, which is based on OAuth. Accompaning this is a Razor Pages default
UI package, `idunno.Bluesky.AspNet.Authentication.UI` which provide login and logout pages, as well as code to process the OAuth callback.

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
    .AddTransient<IClaimsTransformation, BlueskyClaimsTransformer>()
    .AddBlueskyAgentFactory()
```

This configures Bluesky authentication with in-memory stores, suitable for development use.

Next, in your `appsettings.json` or `appsettings.Development.json` add configuration for the agent,

```json
    "BlueskyAgent": {
        "EnableBackgroundTokenRefresh": false,

        "OAuthOptions": {
            "ClientId": "http://localhost?redirect_uri=http://127.0.0.1/Bluesky/Callback&scope=atproto%20transition:generic",
            "ReturnUri": "http://127.0.0.1/Bluesky/Callback"
            "Scopes": [ "atproto", "transition:generic" ]
        }
    }
```

Note that the client ID and return URI are both http. When you run the application you should use the http profile, or edit the URIs to match your HTTPS configuration.
In production these will be configured with your production values.

Now add login and logout links. In the standard ASP.NET Razor Pages templates these are addined in `_LoginPartial.cshtml`. For example,

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

Decorate a page with `[Authorize]`, or make your entire app require authentication, and run it. When you hit an endpoint that requires an authenticated users you should be
sent to the login page, where you enter your handle, bounced through the Bluesky OAuth login page, and back to you application where authentication will happen.

If you have injected a Bluesky agent using the [BlueskyAgentFactory](#agentFactory) you will see that it is now authenticated.

### Stores

Bluesky Authentication requires two different stores, an identity store and a correlation state store. The identity store keeps a the tokens issued by Bluesky for authentication.
The correlation state store keeps the information needed during the oauth login flow.

By default two ephemeral, in memory, stores are used to allow you to developer your application , however these stores are not suitable for production use as they store a limtied
number of identities and are not persistent, so when your application restarts any authenticated users will be logged out. For production you should implement your own stores using the
`IIdentityStore` and `ICorrelationStateCache` interfaces, and configure them in your application configuration in `Program.cs`

Store implementations are provided for MySql, SQLite and Redis, in the `idunno.Bluesky.AspNet.Authentication.MySql`, `idunno.Bluesky.AspNet.Authentication.Sqlite` and `idunno.Bluesky.AspNet.Authentication.Redis` packages.
These can be used to store the identity and correlation state in a persistent store and can be added to your application during configuration.
For example, to use SQLite you would add the following to your `Program.cs` file

```c#
string connectionString = builder.Configuration.GetConnectionString("BlueskyAuthentication")!;

builder.Services
    .AddAuthentication()
    .AddBluesky(options =>
    {
        options.IdentityStore = new SqliteIdentityStore(connectionString);
        options.CorrelationCache = new SqliteCorrelationStateCache(connectionString);
    });
```

Note that information persisted in the identity store is sensitive and is, by default, protected using the ASP.NET Core Data Protection API.
This means that if you run your application on multiple servers you must configure data protection to use a common key store, and
if you change the key store or run your application on a different server all previously persisted identities will be invalidated.

To configure data protection see the [Microsoft documentation](https://learn.microsoft.com/en-us/aspnet/core/security/data-protection/configuration/overview).

### Using claims transformation to supplement the identity

`BlueskyClaimsTransformer` is provided to enhance the `ClaimsIdentity` issued during authentication. It requests the profile for the authenticated user, and caches it, adding
claims from the profile to the identity. The potential claims are defined in `Bluesky.ClaimTypes`. They include

* `Bluesky.ClaimTypes.Handle` - the user's Bluesky handle,
* `Bluesky.ClaimTypes.DisplayName` - the user's display name,
* `Bluesky.ClaimTypes.Description` - the user's description, from their profile,
* `Bluesky.ClaimTypes.Pronouns` - the user's pronounces, from their profile,
* `Bluesky.ClaimTypes.Website` - the user's website, from their profile,
* `Bluesky.ClaimTypes.Avatar` - a URI to the user's uploaded avatar and
* `Bluesky.ClaimTypes.Banner` - a URI to the user's profile banner.

> [!WARNING]
> You should validate, and protect against XSS when rendering these values on a web page, as they are freeform text (except for avatar and banner).

To use the transformer configure it in your application in `Program.cs` like so;

```c#
builder.Services
    .AddBlueskyClaimsTransformer()
    .AddTransient<IClaimsTransformation, BlueskyClaimsTransformer>()
```

### OAuth and DPop

Bluesky authentication is OAuth authentication with some extra layers, including a version of
dynamic client registration requiring a [client metadata document](https://atproto.com/specs/oauth#client-id-metadata-document),
and [DPoP](https://datatracker.ietf.org/doc/html/rfc9449) to allow for protection against token theft.
This does mean you can't use the in-box OAuth authentication handlers for ASP.NET Core.

The implementation of Bluesky authentication in `idunno.Bluesky` needs a
[distributed cache](https://learn.microsoft.com/en-us/aspnet/core/performance/caching/distributed). It is used to store
the following items:

* **DPoP keys**. It's recommended that DPoP keys are stored on the server, rather than in the client browser,
  the distributed cache enables that.
* **OAuth State** that is needed to complete the OAuth flow.

> [!TIP]
> Bluesky special cases the `http://localhost` ClientId, to allow for local development of client applications, including
> web clients. If you want to test your application whilst running as `localhost`, for example, within Visual Studio,
> you you must set the `ReturnUri` to "http://127.0.0.1" in configuration. For example
> ```json
>"BlueskyAgent": {
>  "OAuthOptions": {
>    "ClientId": "http://localhost",
>    "ReturnUri": "http://127.0.0.1",
>    "Scopes": [ "transition:generic" ]
>  }
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

Bluesky uses Pushed Authorization Requests ([PAR](https://datatracker.ietf.org/doc/html/rfc9126) as a security measure.
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
        /// After validating the model and then create a BlueskyAgent to do something

        using (var agent = new BlueskyAgent(
            options: new BlueskyAgentOptions()
            {
                LoggerFactory = loggerFactory,
            }));

        /// Now do what you need to do with agent.
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
        "EnableBackgroundTokenRefresh": false,
        }
    }
}
```

> [!TIP]
> You cannot set the `IFacetExtractor` or `JsonSerializerOptions` from an appsettings file.
> If you want to override either of these manually create and instance `BlueskyAgentOptions`
> and pass it to `builder.Services.AddBlueskyAgentOptions()`.
