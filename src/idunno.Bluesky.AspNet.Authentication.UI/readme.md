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

## Changing the appearance of the pages

The pages in this package render inside a plain, self contained layout the package ships, so they work in any application, including a minimal one with no layout of
its own. The accompanying stylesheet is served as a static web asset, so your application must call `app.MapStaticAssets()`, or `app.UseStaticFiles()` on earlier
versions of ASP.NET Core, for the pages to pick up their styling.

To render the pages inside your own layout instead, add a `_ViewStart.cshtml` to your application at `Areas/Bluesky/Pages/_ViewStart.cshtml`. Files in an application
take precedence over identically pathed files in a Razor Class Library, so yours replaces the one this package ships.

```c#
@{
    Layout = "/Pages/Shared/_Layout.cshtml";
}
```

The page markup uses Bootstrap class names, so an application whose layout loads Bootstrap styles them without any further work.

## Documentation
[Documentation](https://bluesky.idunno.dev/) is available, including API references.

