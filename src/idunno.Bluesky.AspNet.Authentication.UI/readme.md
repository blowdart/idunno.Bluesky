# idunno.Bluesky.AspNet.Authentication.UI

## About

A default UI for ASP.NET Core authentication using the [Bluesky social network](https://bsky.social/).

## Version History

A full [version history](https://github.com/blowdart/idunno.Bluesky/blob/main/CHANGELOG.md) can be found on the project's
[GitHub](https://github.com/blowdart/idunno.Bluesky/) repository.

## How to Use

```c#
builder.Services
    .AddAuthentication(BlueskyAuthenticationDefaults.AuthenticationScheme)
    .AddBluesky()
    .AddBlueskyAuthenticationUI();
```

## Documentation
[Documentation](https://bluesky.idunno.dev/) is available, including API references.

