# Unspecced APIs

Bluesky has a number of APIs that you may find useful, but which are in the [unspecced namespace](https://github.com/bluesky-social/atproto/tree/main/lexicons/app/bsky/unspecced).

> [!CAUTION]
> Unspecced APIs are subject to change without warning and their use may return unexpected errors.
> Do not rely on the behavior of unspecced APIs.

All unspecced APIs are marked as [Experimental](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.codeanalysis.experimentalattribute).
To use an unspecced API suppress the warning, either in your `csproj` or `props` file,

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">

    <!-- ... -->

    <PropertyGroup>
        <!-- Suppress warnings and errors for SPC101 -->
        <NoWarn>BSKYUnspecced</NoWarn>
    </PropertyGroup>

    <!-- ... -->

</Project>
```

or by using `#pragma warning disable BSKYUnspecced` in the code where you consume the API.

```c#
#pragma warning disable BSKYUnspecced
var trendingTopicsResult = await BlueskyServer.GetTrendingTopics();
#pragma warning restore BSKYUnspecced 
```
