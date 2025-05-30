# ![The Bluesky butterfly logo, except in purple](docs/docicon.png)idunno.Bluesky

[if you want me to wear 37 pieces of flair, like your pretty boy over there, Brian, why don't you just make the minimum 37 pieces of flair?]: #

[![GitHub License](https://img.shields.io/github/license/blowdart/idunno.Bluesky)](https://github.com/blowdart/idunno.Bluesky/blob/main/LICENSE)
[![Last Commit](https://img.shields.io/github/last-commit/blowdart/idunno.Bluesky)](https://github.com/blowdart/idunno.Bluesky/commits/main/)
[![GitHub Tag](https://img.shields.io/github/v/tag/blowdart/idunno.Bluesky)](https://github.com/blowdart/idunno.Bluesky/tags)
[![NuGet Version](https://img.shields.io/nuget/vpre/idunno.Bluesky)](https://www.nuget.org/packages/idunno.Bluesky/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/idunno.Bluesky)](https://www.nuget.org/packages/idunno.Bluesky/)

A .NET 8 and .NET 9 class library for the [AT Protocol](https://docs.bsky.app/docs/api/at-protocol-xrpc-api) and APIs for the [Bluesky social network](https://bsky.social/).

## Getting Started

Add the `idunno.Bluesky` package to your project, and then 

```c#
BlueskyAgent agent = new ();

var loginResult = await agent.Login(username, password);
if (loginResult.Succeeded)
{
    var response = await agent.CreatePost("Hello World");
    if (response.Succeeded)
    {
    }
}
```

Please see the [documentation](https://bluesky.idunno.dev/) for much more useful documentation and samples.

The [API status page](https://bluesky.idunno.dev/docs/endpointStatus.html) shows what APIs are currently implemented.

## Current Build Status

![Build Status](https://github.com/blowdart/idunno.Bluesky/actions/workflows/ci-build.yml/badge.svg?branch=main)

## Release History

The [releases page](https://github.com/blowdart/idunno.Bluesky/releases) provides details of each release and what was added, changed or removed.

## License

`idunno.Bluesky`, `idunno.AtProto` and `idunno.AtProto.OAuthCallBack` are available under the MIT license, see the [LICENSE](LICENSE) file for more information.

## Planned work

### Major

* Logging in idunno.Bluesky
* GIF attaching
* Firehose support
* Wider test coverage
* More deserialization tests with captured responses

### Minor

* [Live support](https://github.com/bluesky-social/atproto/pull/3824)

### Awaiting external

* Automatic Open Graph card generation when link facets detected.

## Dependencies

`idunno.AtProto` takes a dependency on `System.Text.Json` v9 to support deserializing derived types where the `$type` property is not the
first property in the JSON object. 

### External dependencies

* [Microsoft.Extensions.Logging](https://learn.microsoft.com/en-us/dotnet/core/extensions/logging) - used to provide log messages.
* [Microsoft.IdentityModel.Tokens](https://github.com/AzureAD/azure-activedirectory-identitymodel-extensions-for-dotnet) - used to extract the expiry date and time of the JWT tokens issued by Bluesky.
* [IdentityModel.OidcClient](https://github.com/IdentityModel/IdentityModel.OidcClient) - used to do the OAuth heavy lifting.
* [DnsClient](https://dnsclient.michaco.net/) - used in Handle to DID resolution.
* [ZstdSharp](https://github.com/oleg-st/ZstdSharp) - used in Jetstream decompression.

### External analyzers used during builds
* [DotNetAnalyzers.DocumentationAnalyzers](https://github.com/DotNetAnalyzers/DocumentationAnalyzers) - used to validate XML docs on public types.
* [SonarAnalyzer.CSharp](https://www.sonarsource.com/products/sonarlint/features/visual-studio/) - used for common code smell detection.

### External build &amp; testing tools

* [xunit](https://github.com/xunit/xunit) - used for unit tests.
* [NerdBank.GitVersioning](https://github.com/dotnet/Nerdbank.GitVersioning) - used for version stamping assemblies and packages.
* [DotNet.ReproducibleBuilds](https://github.com/dotnet/reproducible-builds) - used to easily set .NET reproducible build settings.
* [ReportGenerator](https://github.com/danielpalme/ReportGenerator) - used to produce code coverage reports.
* [JunitXml.TestLogger](https://github.com/spekt/junit.testlogger) - used in CI builds to produce test results in a format understood by the [test-summary](https://github.com/test-summary/action) GitHub action.
* [sign](https://github.com/dotnet/sign) - used to code sign assemblies and nuget packages.
* [docfx](https://dotnet.github.io/docfx/) - used to generate the documentation site.

## Other .NET Bluesky libraries and projects

* [FishyFlip](https://github.com/drasticactions/FishyFlip)
* [X.Bluesky](https://github.com/a-gubskiy/X.Bluesky)
* [atprotosharp](https://github.com/taranasus/atprotosharp)
* [atompds](https://github.com/PassiveModding/atompds) - an implementation of an AtProto Personal Data Server in C#
* [AppViewLite](https://github.com/alnkesq/AppViewLite) - an implementation of the Bluesky AppView in C# focused on low resource consumption
