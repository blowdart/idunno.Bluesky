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

## Related Packages

* [idunno.Bluesky.AspNet.Authentication.UI](https://www.nuget.org/packages/idunno.Bluesky.AspNet.Authentication.UI) which provides a UI for the Bluesky authentication flow.
* [idunno.Bluesky.AspNet.Authentication.MySQL](https://www.nuget.org/packages/idunno.Bluesky.AspNet.Authentication.MySQL) which provides a MySQL database implementation for storing Bluesky authentication tokens with this package.
* [idunno.Bluesky.AspNet.Authentication.SQLite](https://www.nuget.org/packages/idunno.Bluesky.AspNet.Authentication.SQLite) which provides a SQLite database implementation for storing Bluesky authentication tokens with this package.
* [idunno.Bluesky.AspNet.Authentication.Redis](https://www.nuget.org/packages/idunno.Bluesky.AspNet.Authentication.Redis) which provides a Redis cache implementation for storing Bluesky authentication tokens with this package.
* [idunno.Bluesky](https://www.nuget.org/packages/idunno.Bluesky) for interacting with the [Bluesky social network](https://docs.bsky.app/).


## Documentation
[Documentation](https://bluesky.idunno.dev/) is available, including API references.

