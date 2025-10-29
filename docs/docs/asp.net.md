# Using idunno.Bluesky with ASP.NET

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

## Bluesky Authentication

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
            "CheckCertificateRevocationList" : false
        }
    }
}
```

> [!WARNING]
> It is likely that you will need to set `CheckCertificateRevocationList` to `false`, depending on how
> your proxy generates proxy HTTP certificates. This does introduce a security vulnerability and should not be
> used on production systems, only during development.
