# idunno.Bluesky

A .NET 8 class library for the [AT Protocol](https://docs.bsky.app/docs/api/at-protocol-xrpc-api) and APIs for the [Bluesky social network](https://bsky.social/).

## Current Build Status

[![Build Status](https://github.com/blowdart/idunno.atproto/actions/workflows/ci-build.yml/badge.svg)](https://github.com/blowdart/idunno.atproto/actions/workflows/ci-build.yml)

[![Test Results](https://camo.githubusercontent.com/9f508f166f15790248d7986f09e96076b994a0eddb0293c810ed3bbcccdb3ac0/68747470733a2f2f7376672e746573742d73756d6d6172792e636f6d2f64617368626f6172642e7376673f703d31313826663d3026733d30)](https://github.com/blowdart/idunno.atproto/actions/workflows/ci-build.yml)

## Getting Started

Add the `idunno.Bluesky` package to your project and

```c#
BlueskyAgent agent = new ();
HttpResult<bool> loginResult = await agent.Login(username, password);
if (loginResult)
{
    HttpResult<CreateRecordResponse> response = await agent.CreatePost("Hello World");
    if (response.Succeeded)
    {
    }
}
```

Please see the [documentation](docs/readme.md) much more useful documentation and samples.

The [API status page](docs/endpointStatus.md) shows what is currently implemented and what is planned.

## License

`idunno.Bluesky` and `idunno.AtProto` are available under the MIT license, see the [LICENSE](LICENSE) file for more information.

## External dependencies

* [Microsoft.Extensions.Logging](https://learn.microsoft.com/en-us/dotnet/core/extensions/logging) - used to provide log messages.
* [Microsoft.IdentityModel.Tokens](https://github.com/AzureAD/azure-activedirectory-identitymodel-extensions-for-dotnet) - used to extract the expiry date and time of the JWT tokens issued by Bluesky.
* [DnsClient](https://dnsclient.michaco.net/) - used in Handle to DID resolution.
* [Macross.Json.Extensions](https://github.com/Macross-Software/core/tree/develop/ClassLibraries/Macross.Json.Extensions) - used to enable better JSON deserialization of enums.

## Build tools

* [NerdBank.GitVersioning](https://github.com/dotnet/Nerdbank.GitVersioning) - used for version stamping assemblies and packages.
* [DotNet.ReproducibleBuilds](https://github.com/dotnet/reproducible-builds) - used to easily set .NET reproducible build settings.
* [DotNetAnalyzers.DocumentationAnalyzers](https://github.com/DotNetAnalyzers/DocumentationAnalyzers) - used to validate XML docs on public types.
* [xunit](https://github.com/xunit/xunit) - used for unit tests.
* [ReportGenerator](https://github.com/danielpalme/ReportGenerator) - used to produce code coverage reports.
* [JunitXml.TestLogger](https://github.com/spekt/junit.testlogger) - used in CI builds to produce test results in a format understood by the [test-summary](https://github.com/test-summary/action) GitHub action.

## Other .NET Bluesky libraries

* [FishyFlip](https://github.com/drasticactions/FishyFlip)
* [atprotosharp](https://github.com/taranasus/atprotosharp)
