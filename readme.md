# ![The Bluesky butterfly logo, except in purple](docs/docicon.png)idunno.Bluesky - .NET SDK for Bluesky.

A .NET 8, 9 and 10 library and SDK for [Bluesky](https://bsky.social/) and the [AT Protocol](https://docs.bsky.app/docs/api/at-protocol-xrpc-api)

[if you want me to wear 37 pieces of flair, like your pretty boy over there, Brian, why don't you just make the minimum 37 pieces of flair?]: #

[![GitHub License](https://img.shields.io/github/license/blowdart/idunno.Bluesky)](https://github.com/blowdart/idunno.Bluesky/blob/main/LICENSE)
[![Last Commit](https://img.shields.io/github/last-commit/blowdart/idunno.Bluesky)](https://github.com/blowdart/idunno.Bluesky/commits/main/)
[![GitHub Tag](https://img.shields.io/github/v/tag/blowdart/idunno.Bluesky)](https://github.com/blowdart/idunno.Bluesky/tags)
[![NuGet Version](https://img.shields.io/nuget/vpre/idunno.Bluesky)](https://www.nuget.org/packages/idunno.Bluesky/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/idunno.Bluesky)](https://www.nuget.org/packages/idunno.Bluesky/)

[![OpenSSF Scorecard](https://api.scorecard.dev/projects/github.com/blowdart/idunno.Bluesky/badge)](https://scorecard.dev/viewer/?uri=github.com/blowdart/idunno.Bluesky)
[![OpenSSF Best Practices](https://www.bestpractices.dev/projects/11157/badge)](https://www.bestpractices.dev/projects/11157)

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

## Key Features

* Creating and deleting posts
  * Posting with mentions, links and hashtags
  * Posting images and video, with alt text support.
  * Setting a post's language
  * Gating threads and posts
  * Liking, quoting, and reposting posts
* Viewing a user's timeline and notifications
* Viewing feeds
* Viewing threads
* Viewing notifications
* Viewing and setting preferences for
  * Subscribing to user activities
  * Viewing user profiles
  * Notifications
* Following and unfollowing users
* Muting and blocking users
* Sending, receiving, and deleting direct messages
* Create, deleting and listing a user's bookmarks
* Handle / password and OAuth authentication
* Jetstream support for simple firehose consumption
* Automatic session management with background token refreshes

Trimming is supported for applications targeting .NET 9.0 or later.

## Current Build Status

[![Build Status](https://github.com/blowdart/idunno.Bluesky/actions/workflows/ci-build.yml/badge.svg?branch=main)](https://github.com/blowdart/idunno.Bluesky/actions/workflows/ci-build.yml)
[![CodeQL Scan](https://github.com/blowdart/idunno.Bluesky/actions/workflows/codeql-analysis.yml/badge.svg?branch=main)](https://github.com/blowdart/idunno.Bluesky/actions/workflows/codeql-analysis.yml)
[![Dependency Review](https://github.com/blowdart/idunno.Bluesky/actions/workflows/dependency-review.yml/badge.svg)](https://github.com/blowdart/idunno.Bluesky/actions/workflows/dependency-review.yml)


## License

`idunno.Bluesky`, `idunno.AtProto`, `idunno.AtProto.OAuthCallBack` and `idunno.AtProto.Types` are available under the MIT license,
see the [LICENSE](LICENSE) file for more information.

## Tipping / Sponsoring

If you find this library useful please consider donating to
* a local food bank,
* a local animal rescue or shelter, or
* a national Multiple Sclerosis charity in your country
  * US: [National Multiple Sclerosis Society](https://www.nationalmssociety.org/)
  * UK: [MS Society UK](https://www.mssociety.org.uk/)
  * Canada: [MS Canada](https://mscanada.ca/)

If you want to give me the warm fuzzies, you can tag me on Bluesky at [@blowdart.me](https://bsky.app/profile/blowdart.me) to let me know.

## Release History

The [releases page](https://github.com/blowdart/idunno.Bluesky/releases) provides details of each release and what was added, changed or removed.
The [changelog](CHANGELOG.md) also contains this information, as well as information on upcoming releases.

## Release Verification

The project uses an Authenticode certificate to sign assemblies and to author sign the nupkg packages.
nuget validates the signatures during its publication process.

To validate these signatures use

```
dotnet nuget verify [<package-path(s)>]
```

The subject name of the signing certificate should be

```
Subject Name: CN=Barry Dorrans, O=Barry Dorrans, L=Bothell, S=Washington, C=US
```

In addition, GitHub artifacts are attested during build,
and are also signed with [minisign](https://github.com/jedisct1/minisign) with the following public key.

```
RWTsT4BHHChe/Rj/GBAuZHg3RaZFnfBDqaZ7KzLvr44a7mO6fLCxSAFc
```

To validate a file using an artifact signature from a [release](https://github.com/blowdart/idunno.Bluesky/releases)
download the `.nupkg` from nuget and the appropriate `.minisig` from the release page, then use the following command,
replacing `<package-path>` with the file name you wish to verify.

```
minisign -Vm <package-path> -P RWTsT4BHHChe/Rj/GBAuZHg3RaZFnfBDqaZ7KzLvr44a7mO6fLCxSAFc
```

## Pre-releases

[![Prerelease Version](https://img.shields.io/myget/blowdart/vpre/idunno.Bluesky?label=idunno.Bluesky)](https://www.myget.org/gallery/blowdart)

If you want to test pre-releases you can find them in the [myget feed](https://www.myget.org/gallery/blowdart).

You can add this as a Package Source in [Visual Studio](https://learn.microsoft.com/en-us/nuget/consume-packages/install-use-packages-visual-studio#package-sources)
or through the [command line](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-nuget-add-source), or by using the sample `nuget.config` file shown below:

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
    <add key="blowdart.myget.org" value="https://www.myget.org/F/blowdart/api/v3/index.json" />
  </packageSources>

  <packageSourceMapping>
    <packageSource key="blowdart.myget.org">
      <package pattern="idunno.AtProto" />
      <package pattern="idunno.AtProto.*" />
      <package pattern="idunno.Bluesky" />
    </packageSource>
    <packageSource key="nuget.org">
      <package pattern="*" />
    </packageSource>
  </packageSourceMapping>
</configuration>
```

The package source URI is https://www.myget.org/F/blowdart/api/v3/index.json

To match a pre-release for a specific commit, if a build was produced for that commit

1. Check the version value in [version.json](https://github.com/blowdart/idunno.Bluesky/blob/main/version.json),
1. Check if there was a [Pre-release publish runs](https://github.com/blowdart/idunno.Bluesky/actions/workflows/prerelease-build.yml) for the commit you are interested in,
1. The pre-release package will have the first 10 digits of the commit sha, prefixed with a `g` as the build number.

For example, commit [190d63e](https://github.com/blowdart/idunno.Bluesky/commit/190d63e20d3d59e86912fd8cfe315915d101f6a8)
produced a nightly build and packages. The package build number is be 1.1.0-prerelease.`g190d63e20d`

Please note that nightly builds are signed with [Trusted Signing](https://azure.microsoft.com/en-us/products/trusted-signing),
the signing certificate chain will not match the signing chain of a release build. The subject name remains the same.

## Planned work

### Major

* Logging in idunno.Bluesky
* GIF attaching
* Wider test coverage
* More deserialization tests with captured responses

### Awaiting external

* Automatic Open Graph card generation when link facets detected.

## Dependencies

The .NET 8.0 version of `idunno.AtProto` takes a dependency on `System.Text.Json` v9 to support deserializing derived types
where the `$type` property is not the first property in the JSON object. 

### External dependencies

* [Microsoft.Extensions.Logging](https://learn.microsoft.com/en-us/dotnet/core/extensions/logging) - used to provide log messages.
* [Microsoft.IdentityModel.Tokens](https://github.com/AzureAD/azure-activedirectory-identitymodel-extensions-for-dotnet) - used to extract the expiry date and time of the JWT tokens issued by Bluesky.
* [Duende.IdentityModel.OidcClient](https://github.com/DuendeSoftware/foss/tree/main/identity-model-oidc-client) - used to do the OAuth heavy lifting.
* [DnsClient](https://dnsclient.michaco.net/) - used in Handle to DID resolution.
* [PeterO.CBor](https://github.com/peteroupc/CBOR) - used in Fire Hose decoding.
* [SimpleBase](https://github.com/ssg/SimpleBase) - used in decoding CIDs.
* [ZstdSharp](https://github.com/oleg-st/ZstdSharp) - used in Jetstream decompression.

### External analyzers used during builds
* [DotNetAnalyzers.DocumentationAnalyzers](https://github.com/DotNetAnalyzers/DocumentationAnalyzers) - used to validate XML docs on public types.
* [CommentSense](https://www.nuget.org/packages/CommentSense) - used to validate XML docs on public types.
* [Microsoft.CodeAnalysis.PublicApiAnalyzers](https://github.com/dotnet/roslyn-analyzers/blob/main/src/PublicApiAnalyzers/PublicApiAnalyzers.Help.md) - used to track public API changes.
* [SonarAnalyzer.CSharp](https://www.sonarsource.com/products/sonarlint/features/visual-studio/) - used for common code smell detection.

### External build &amp; testing tools

* [docfx](https://dotnet.github.io/docfx/) - used to generate the documentation site.
* [DotNet.ReproducibleBuilds](https://github.com/dotnet/reproducible-builds) - used to easily set .NET reproducible build settings.
* [Coverlet.Collector](https://github.com/coverlet-coverage/coverlet) - used to produce code coverage files
* [JunitXml.TestLogger](https://github.com/spekt/junit.testlogger) - used in CI builds to produce test results in a format understood by the [test-summary](https://github.com/test-summary/action) GitHub action.
* [NerdBank.GitVersioning](https://github.com/dotnet/Nerdbank.GitVersioning) - used for version stamping assemblies and packages.
* [ReportGenerator](https://github.com/danielpalme/ReportGenerator) - used to produce code coverage reports.
* [sign](https://github.com/dotnet/sign) - used to code sign assemblies and nuget packages.
* [xunit](https://github.com/xunit/xunit) - used for unit tests.

## Other .NET Bluesky libraries and projects

* [FishyFlip](https://github.com/drasticactions/FishyFlip)
* [X.Bluesky](https://github.com/a-gubskiy/X.Bluesky)
* [atprotosharp](https://github.com/taranasus/atprotosharp)
* [atompds](https://github.com/PassiveModding/atompds) - an implementation of an AtProto Personal Data Server in C#
* [AppViewLite](https://github.com/alnkesq/AppViewLite) - an implementation of the Bluesky AppView in C# focused on low resource consumption
