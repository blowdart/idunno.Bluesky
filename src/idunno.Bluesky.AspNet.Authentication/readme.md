# idunno.Bluesky.AspNet.Authentication

## About

ASP.NET Core authentication using the [Bluesky social network](https://bsky.social/).

## Key Features

* ASP.NET authentication using the Bluesky OAuth flow.
* Claims transformation to read enhanced user information from their Bluesky profile.

## Version History

A full [version history](https://github.com/blowdart/idunno.Bluesky/blob/main/CHANGELOG.md) can be found on the project's
[GitHub](https://github.com/blowdart/idunno.Bluesky/) repository.

## How to Use

```c#
builder.Services
    .AddAuthentication(BlueskyAuthenticationDefaults.AuthenticationScheme)
    .AddBluesky()
    .AddBlueskyAuthenticationUI();

builder.Services
    .AddBlueskyClaimsTransformer()
    .AddBlueskyAgentFactory();
```

## OAuth client metadata

Configure `BlueskyAgentOptions.OAuthOptions` with the site's public HTTPS metadata URL as `ClientId` and its HTTPS callback as
`ReturnUri`. Register `builder.Services.AddBlueskyOAuthClientMetadata()` and call `app.UseBlueskyOAuthClientMetadata()` before
authentication, authorization and static files to publish a generated public web-client metadata document at that URL.
Optional `OAuthOptions.ClientName`, `ClientUri`, `TosUri` and `PolicyUri` configure the client name, homepage, terms and privacy URLs through the existing agent configuration section.
An optional registration delegate configures the logo URL and `AdditionalScopes` to advertise scopes the application might request later,
without changing its initial login scopes. Configured scopes and permission sets are included automatically.

The metadata is validated at pipeline configuration time. Localhost development IDs use authorization-server-generated virtual
metadata instead; leave publication disabled for those IDs. Existing static metadata hosting remains unchanged.
See the [ASP.NET documentation](https://bluesky.idunno.dev/docs/asp.net.html) for configuration examples and URL requirements.
To generate JSON without serving it, use `BlueskyOAuthClientMetadataOptions.GenerateJson(oAuthOptions)`.

## Related Packages

* [idunno.Bluesky.AspNet.Authentication.UI](https://www.nuget.org/packages/idunno.Bluesky.AspNet.Authentication.UI) which provides a UI for the Bluesky authentication flow.
* [idunno.Bluesky.AspNet.Authentication.MySQL](https://www.nuget.org/packages/idunno.Bluesky.AspNet.Authentication.MySQL) which provides a MySQL database implementation for storing Bluesky authentication tokens with this package.
* [idunno.Bluesky.AspNet.Authentication.SQLite](https://www.nuget.org/packages/idunno.Bluesky.AspNet.Authentication.SQLite) which provides a SQLite database implementation for storing Bluesky authentication tokens with this package.
* [idunno.Bluesky.AspNet.Authentication.Redis](https://www.nuget.org/packages/idunno.Bluesky.AspNet.Authentication.Redis) which provides a Redis cache implementation for storing Bluesky authentication tokens with this package.
* [idunno.Bluesky](https://www.nuget.org/packages/idunno.Bluesky) for interacting with the [Bluesky social network](https://docs.bsky.app/).


## Documentation
[Documentation](https://bluesky.idunno.dev/) is available, including API references.
