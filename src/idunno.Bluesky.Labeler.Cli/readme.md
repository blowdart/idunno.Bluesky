# idunno.Bluesky.Labeler.Cli

## About

`labeler-admin` is a command-line tool for administering a running AT Protocol labeler.
It is distributed as a standalone executable and included in the labeler host container, not as a NuGet package.

## Key Features

* Protected local access through Windows named pipes or Linux Unix sockets
* Authenticated remote access through Bluesky sign-in
* Label issuance and removal, team management and audit inspection
* Policy publication, signing-key management and labeler retirement
* Verified executable updates and container deployment helpers

## Version History

A full [version history](https://github.com/blowdart/idunno.Bluesky/blob/main/CHANGELOG.md) can be found on the project's
[GitHub](https://github.com/blowdart/idunno.Bluesky/) repository.

## How to Use

You need a running labeler host and an authorized local identity or team account.
Remote access must also be enabled on the host when connecting over HTTPS.

Download and verify the CLI for your platform from the project's
[releases](https://github.com/blowdart/idunno.Bluesky/releases), or run it from source:

```powershell
dotnet run --project src\idunno.Bluesky.Labeler.Cli --framework net10.0 -- --help
```

The host container includes the CLI as `labeler-admin`:

```text
docker exec <container> labeler-admin --help
```

## Related Packages

* [idunno.Bluesky.AspNet.Labeler](https://www.nuget.org/packages/idunno.Bluesky.AspNet.Labeler) provides ASP.NET Core hosting and administration.
* [idunno.Bluesky.AspNet.Labeler.UI](https://www.nuget.org/packages/idunno.Bluesky.AspNet.Labeler.UI) provides the optional Xenon web UI.
* [idunno.Bluesky.Labeler.Agent](https://www.nuget.org/packages/idunno.Bluesky.Labeler.Agent) provides a client library for issuing and removing labels.

## Documentation

See the [CLI guide](https://bluesky.idunno.dev/docs/labeler/cli.html) for installation,
commands and permissions, and the [deployment guide](https://bluesky.idunno.dev/docs/labeler/deployment.html)
for host setup.
