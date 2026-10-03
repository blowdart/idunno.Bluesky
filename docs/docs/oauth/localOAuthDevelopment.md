# Local OAuth development

For the basic login flow, see [Connecting to Bluesky](../connecting.md#oauth).

## Testing OAuth locally with localhost

The `idunno.AtProto.OAuthCallback` NuGet package contains a simple web server that can be used to test OAuth logins locally. To use it add a reference
to the package, set the  ClientId in options to "`http://localhost`" but do not set the ReturnUri, then create an instance of the callback server
before you build the login URI, use the callback server uri when creating the login URI, and finally await the callback,
which will return the callback data as a string.
Use `CallbackServer.CreateAsync()` to reserve the loopback sockets and wait until the listener is ready before building the login URI.

```c#

using idunno.AtProto.Authentication;
using idunno.AtProto.OAuthCallback;
using idunno.Bluesky;
using idunno.Bluesky.Authentication;

var agent = new BlueskyAgent(new BlueskyAgentOptions()
    {
        OAuthOptions = new OAuthOptions()
        {
            ClientId = "http://localhost",
            Scopes = ["atproto"],
            PermissionSets = [BlueskyOAuthPermissionSets.FullApp],
        }
    });

string callbackData;
OAuthClient oAuthClient = agent.CreateOAuthClient();

await using var callbackServer = await CallbackServer.CreateAsync(
    loggerFactory: loggerFactory,
    cancellationToken: cancellationToken);
{
    // We dynamically set the return URI as the callback server will listen on a random free port.
    Uri startUri = await agent.BuildOAuth2LoginUri(
        oAuthClient,
        handle,
        returnUri: callbackServer.Uri,
        cancellationToken: cancellationToken);

    // Start the browser. If you are running Linux you need XDG installed.
    OAuthClient.OpenBrowser(startUri);

    callbackData = await callbackServer.WaitForCallbackAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
}

if (!string.IsNullOrEmpty(callbackData))
{
    await agent.ProcessOAuth2LoginResponse(oAuthClient, callbackData, cancellationToken);
}
else
{
    // The process timed out, or another error occurred.
}
```

The [OAuth Sample](https://github.com/blowdart/idunno.Bluesky/tree/main/samples/Samples.OAuth) shows how to use the callback server and login,
and logout with OAuth.

The [OAuth Permission Sets Sample](https://github.com/blowdart/idunno.Bluesky/tree/main/samples/Samples.OAuthPermissionSets)
requests the published create-posts and delete-content sets, creates a text post, refreshes credentials, and deletes
the same post using the refreshed credentials.

For permission selection, see [OAuth permissions](oauthPermissions.md). For adding permissions to an existing session, see [Progressive authorization](progressiveAuthorization.md).

To test an ASP.NET confidential client with public HTTPS URLs, follow the [tunnel sample setup](https://github.com/blowdart/idunno.Bluesky/tree/main/samples/Samples.AspNetTunnelAuthentication).
