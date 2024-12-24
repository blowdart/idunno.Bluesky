# idunno.AtProto

## About

.NET class libraries for [AT Protocol](https://docs.bsky.app/docs/api/at-protocol-xrpc-api).

## Key Features

* PDS authentication and session management
* List, Create, Get, Put, Delete records
* Blob uploads
* Handle and PDS resolution

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

## Documentation
[Documentation](https://bluesky.idunno.dev/) is available, including API references.

The [API status page](https://bluesky.idunno.dev/docs/endpointStatus.html) shows the current state of API support.
