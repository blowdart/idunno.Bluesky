# Using idunno.Bluesky with ASP.NET

## Enabling Logging

ASP.NET's dependency injection allows you to inject an `ILoggerFactory` into your page constructors or services.
You should then pass this into the `BlueskyAgent` constructor.

For example, with Razor Pages this would look like the following

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

        "HttpClientOptions": {
            "ProxyUri": "http://localhost:8866/",
            "CheckCertificateRevocationList" : false
        }
    }
}

```

> [!TIP]
> You cannot set the `IFacetExtractor` or `JsonSerializerOptions` from an appsettings file.
> If you want to override either of these manually create and instance `BlueskyAgentOptions`
> and pass it to `builder.Services.AddBlueskyAgentOptions()`.

