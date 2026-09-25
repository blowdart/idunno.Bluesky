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

## Documentation
[Documentation](https://bluesky.idunno.dev/) is available, including API references.

