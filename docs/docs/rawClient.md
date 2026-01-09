# Sending raw AT Protocol requests

Whilst the agents make it easy to interact with the Bluesky APIs, generating the requests and parsing the responses into typed classes, 
there may be times when you want to send "raw" JSON requests and receive "raw" JSON responses, for example, you may want to send a
request to an API endpoint that is not yet supported by the agents.

The non-generic `AtProtoHttpClient` class allows you to do just that.

To use the `AtProtoHttpClient` class you will need to manually construct the endpoint, and provide the endpoint and the service to an
instance of `AtProtoHttpClient`, as well as the request payload, and credentials, if any. You can then use the `Get()` and `Post()` methods
to send requests to the endpoint.

## Making GET requests

For example, if you wanted to call the [getRecord](https://docs.bsky.app/docs/api/com-atproto-repo-get-record) endpoint on the on the PDS hosted at `gomphidius.us-west.host.bsky.network`
you would do:

```csharp
var result = await client.Get(
             service: "https://gomphidius.us-west.host.bsky.network",
             endpoint: "/xrpc/com.atproto.repo.getRecord?repo=did:plc:3iicfxmgcfr32lansi4ju7oa&collection=app.bsky.feed.post&rkey=3ltpnkmxfns2w");
```

The result will be an instance of `AtProtoHttpResult<string>`, where the `Result` property contains the raw JSON response from the API,
for example

```json
{
    "uri": "at://did:plc:3iicfxmgcfr32lansi4ju7oa/app.bsky.feed.post/3ltpnkmxfns2w",
    "cid": "bafyreieva4sr4r7n3euw45xisx5zmw5npjlzmd2fsnos3z3owoawcnopou",
    "value": {
        "text": "Sorry about that rich tomato sauce followers.\n\nYou should now be receiving the beans facts you deserve.",
        "$type": "app.bsky.feed.post",
        "langs": [
            "en"
        ],
        "createdAt": "2025-07-11T20:08:55.356Z"
    }
}
```

### Making POST requests

You can also send raw JSON payloads using the `Post()` method. Most posts will need authentication.

For example, to create a new Bluesky post, using the [createRecord](https://docs.bsky.app/docs/api/com-atproto-repo-create-record) API
on the PDS hosted at `gomphidius.us-west.host.bsky.network`, for which you have an access token for the `did:plc:ec72yg6n2sydzjvtovvdlxrk` actor
you could do something like this:

```csharp
var credentials = new AccessTokenCredential("*** Replace with a valid access jwt ***");

string json = """
{
    "collection" : "app.bsky.feed.post",
    "repo": "did:plc:ec72yg6n2sydzjvtovvdlxrk",
    "record" : {
        "$type": "app.bsky.feed.post",
        "text": "Manual post record creation",
        "createdAt": "2025-01-01T00:00:00.000Z"
    }
}
""";

var result = await client.Post(
    service: "https://gomphidius.us-west.host.bsky.network",
    endpoint: "/xrpc/com.atproto.repo.createRecord",
    body: json,
    credentials: credentials);
```

AtProto and Bluesky payload formats are defined in the [AtProto and Bluesky lexicons](https://github.com/bluesky-social/atproto/tree/main/lexicons).
