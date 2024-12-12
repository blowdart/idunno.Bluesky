# idunno.Bluesky

![GitHub License](https://img.shields.io/github/license/blowdart/idunno.Bluesky)

A .NET 8 class library for the [AT Protocol](https://docs.bsky.app/docs/api/at-protocol-xrpc-api) and APIs for the [Bluesky social network](https://bsky.social/).

## Current Build Status

![Build Status](https://github.com/blowdart/idunno.Bluesky/actions/workflows/ci-build.yml/badge.svg?branch=main)

![Test Results](https://camo.githubusercontent.com/093a129b50ddc14f2e036c983168963591aa1d67eed31f2ae6e364f012f7dc97/68747470733a2f2f7376672e746573742d73756d6d6172792e636f6d2f64617368626f6172642e7376673f703d36343526663d3026733d30)

## Getting Started

Add the `idunno.Bluesky` package to your project and 

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

Please see the [documentation](docs/readme.md) for much more useful documentation and samples.

The [API status page](docs/endpointStatus.md) shows what is currently implemented and what is planned.

## To-dos

### Major

* Consolidate PostRecord and NewPostRecord if possible
* Logging in idunno.Bluesky
* OAuth
* Video uploading and attaching
* GIF attaching
* Profile editing
* AOT & trimming support
* Firehose support

* Create automatic release action, which will
  * Extract SBOM from package and expose as separate asset
  * Upload SBOM via `spdx-dependency-submission-action`
  * Publish to nuget

### Minor

* Wider test coverage
* More deserialization tests with captured responses

### Awaiting external

* Automatic Open Graph card generation when link facets detected.

### Documentation

* Docs accuracy after refactoring
* Docs site generation, including XMLDocs, markdown link checking and publishing

## License

`idunno.Bluesky` and `idunno.AtProto` are available under the MIT license, see the [LICENSE](LICENSE) file for more information.

## Dependencies

`idunno.AtProto` takes a dependency on `System.Text.Json` v9 to support deserializing derived types where the `$type` property is not the
first property in the JSON object.

### External dependencies

* [Microsoft.Extensions.Logging](https://learn.microsoft.com/en-us/dotnet/core/extensions/logging) - used to provide log messages.
* [Microsoft.IdentityModel.Tokens](https://github.com/AzureAD/azure-activedirectory-identitymodel-extensions-for-dotnet) - used to extract the expiry date and time of the JWT tokens issued by Bluesky.
* [DnsClient](https://dnsclient.michaco.net/) - used in Handle to DID resolution.

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

## Other .NET Bluesky libraries

* [FishyFlip](https://github.com/drasticactions/FishyFlip)
* [X.Bluesky](https://github.com/a-gubskiy/X.Bluesky)
* [atprotosharp](https://github.com/taranasus/atprotosharp)
