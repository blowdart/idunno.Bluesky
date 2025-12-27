# idunno.AtProto

## About

.NET class libraries for [AT Protocol](https://docs.bsky.app/docs/api/at-protocol-xrpc-api).

## Key Features

* PDS authentication via handle/password and OAuth
* List, Create, Get, Put, Delete records
* Blob uploads
* Self labelling
* Handle and PDS resolution
* AT Protocol Jetstream support

* Trimming is supported for applications targeting .NET 9.0 or later.

## Version History

A full [version history](https://github.com/blowdart/idunno.Bluesky/blob/main/CHANGELOG.md) can be found on the project's
[GitHub](https://github.com/blowdart/idunno.Bluesky/) repository.

## How to Use

```c#
AtProtoAgent agent = new(new Uri("https://bsky.social"));

var loginResult = await agent.Login(username, password);
if (loginResult.Succeeded)
{
    var did = await agent.ResolveHandle("blowdart.me", cancellationToken);

    if (did is not null)
    {
        var pds = await agent.ResolvePds(did);

        if (pds is not null)
        {
            var listRecordsResult = await agent.ListRecords<AtProtoRecord> (
                did,
                collection: "app.bsky.feed.post",
                service : pds);

            if (listRecordsResult.Succeeded)
            {
                // listRecords.Result contains a paged list of bluesky posts for blowdart.me
            }
        }
    }
}
```

## Related Packages

* [idunno.Bluesky](https://www.nuget.org/packages/idunno.Bluesky) for interacting with the [Bluesky social network](https://docs.bsky.app/).
* [idunno.AtProto.OAuthCallback](https://www.nuget.org/packages/idunno.AtProto.OAuthCallback) which provides a local callback server for OAuth authentication.
* [idunno.AtProto.Types](https://www.nuget.org/packages/idunno.AtProto.Types) which contains base types for the AtProto network.

## Documentation
[Documentation](https://bluesky.idunno.dev/) is available, including API references.

The [API status page](https://bluesky.idunno.dev/docs/endpointStatus.html) shows the current state of API support.
