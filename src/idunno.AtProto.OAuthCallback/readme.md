# idunno.AtProto.OAuthCallback

## About

OAuth libraries for the [idunno.AtProto](https://www.nuget.org/packages/idunno.AtProto).

## Key Features

* Local callback server for console applications and local testing.

* Trimming is supported for applications targeting .NET 9.0 or later.

## Version History

A full [version history](https://github.com/blowdart/idunno.Bluesky/blob/main/history.md) can be found on the project's
[GitHub](https://github.com/blowdart/idunno.Bluesky/) repository.

## How to Use

```c#
using (var agent = new BlueskyAgent(
    options: new BlueskyAgentOptions()
    {
        OAuthOptions = new OAuthOptions()
        {
            ClientId = "http://localhost",
            Scopes = ["atproto"]
        }
    }))
    {

        await using var callbackServer = new CallbackServer(
            CallbackServer.GetRandomUnusedPort());
        {
            string callbackData;

            OAuthClient oAuthClient = agent.CreateOAuthClient();

            Uri startUri = await agent.BuildOAuth2LoginUri(oAuthClient, loginHandle, returnUri: callbackServer.Uri, cancellationToken: cancellationToken);

            OAuthClient.OpenBrowser(startUri);

            callbackData = await callbackServer.WaitForCallbackAsync(cancellationToken: cancellationToken).ConfigureAwait(false);

            await agent.ProcessOAuth2LoginResponse(oAuthClient, callbackData, cancellationToken);

            await agent.ProcessOAuth2LoginResponse(oAuthClient, callbackData, cancellationToken);
        }
    }
```

## Related Packages

* [idunno.AtProto](https://www.nuget.org/packages/idunno.AtProto) for interacting with an ATProto service.
* [idunno.Bluesky](https://www.nuget.org/packages/idunno.Bluesky) for interacting with the [Bluesky social network](https://docs.bsky.app/).

## Documentation
[Documentation](https://bluesky.idunno.dev/) is available, including API references.
