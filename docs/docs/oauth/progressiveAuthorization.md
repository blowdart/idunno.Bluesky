# Progressive authorization

This guide builds on [OAuth login](../connecting.md#oauth) and [OAuth permissions](oauthPermissions.md).
For the loopback callback helper used below, see [Local OAuth development](localOAuthDevelopment.md).

The client metadata `scope` declares the maximum scopes your app may request, not the scopes every session must use.
Keep the client ID fixed and pass an explicit subset to `BuildOAuth2LoginUri` at sign-in. When the user opts into a
feature requiring additional permissions, start a new OAuth flow with the original scopes **plus** the new ones.
Restoring its saved state preserves that flow's client ID and scope request during the token exchange.
Verify that the returned credentials identify the same DID before replacing the old session; do not silently switch
accounts or assume that refreshing the existing grant can add scopes.
Use `agent.ProcessOAuth2LoginResponse(oAuthClient, callbackData, expectedDid, cancellationToken)` to have the SDK
validate the expected DID before calling `Login()`. A mismatch throws `OAuthException` and leaves the existing session unchanged.
See the [AT Protocol progressive scope request guide](https://atproto.com/guides/oauth-patterns#progressive-scope-requests).

For localhost development, declare the maximum scopes once in the client ID's URL-encoded `scope` query parameter
(for example, `http://localhost/?scope=...`) and use that exact ID in both flows.
The SDK expands bare `http://localhost` using the scopes of the current request, so using the bare ID for different
subsets would change the effective client ID. Hosted clients instead declare the maximum in their metadata document.

## Declare the maximum permissions, then request a subset

For example, start with identity and timeline access and add the create-posts and delete-content Permission Sets later.
The granular timeline permission is narrower than `BlueskyOAuthPermissionSets.ViewAll`, which also grants access to
notifications, preferences, and other endpoints.

```c#
using System.Net;

using idunno.AtProto;
using idunno.AtProto.Authentication;
using idunno.AtProto.OAuthCallback;
using idunno.Bluesky;
using idunno.Bluesky.Authentication;

string[] initialScopes =
[
    "atproto",
    "rpc:app.bsky.feed.getTimeline?aud=did%3Aweb%3Aapi.bsky.app%23bsky_appview"
];

OAuthOptions options = new()
{
    Scopes = initialScopes,
    PermissionSets =
    [
        BlueskyOAuthPermissionSets.CreatePosts,
        BlueskyOAuthPermissionSets.DeleteContent
    ]
};
string[] expandedScopes = [.. options.GetRequestedScopes()];
options.ClientId =
    $"http://localhost/?scope={Uri.EscapeDataString(string.Join(" ", expandedScopes))}";

using var agent = new BlueskyAgent(new BlueskyAgentOptions()
{
    OAuthOptions = options
});
```

The configured scopes describe the maximum; passing `initialScopes` explicitly to the authorization request overrides
them for the first consent flow. Both flows still use the same client ID.

## Authorize and validate the callback identity

This local callback helper can be used for both flows. It creates a fresh `OAuthClient` for each authorization and restores
the saved state before processing the callback. The `expectedDid` overload validates the returned account before installing
its credentials on the agent.

```c#
static async Task<bool> Authorize(
    BlueskyAgent agent,
    Handle handle,
    string[] scopes,
    Did expectedDid,
    CancellationToken cancellationToken)
{
    await using var callbackServer = await CallbackServer.CreateAsync(
        cancellationToken: cancellationToken);
    OAuthClient client = agent.CreateOAuthClient();
    Uri loginUri = await agent.BuildOAuth2LoginUri(
        client,
        handle,
        scopes: scopes,
        returnUri: callbackServer.Uri,
        cancellationToken: cancellationToken);

    OAuthLoginState savedState = client.State
        ?? throw new OAuthException("OAuth login state is missing.");
    OAuthClient.OpenBrowser(loginUri);
    string callbackData = await callbackServer.WaitForCallbackAsync(
        cancellationToken: cancellationToken);
    if (string.IsNullOrEmpty(callbackData))
    {
        throw new OAuthException("No OAuth callback was received.");
    }

    OAuthClient restoredClient = agent.CreateOAuthClient(savedState);

    return await agent.ProcessOAuth2LoginResponse(
        restoredClient, callbackData, expectedDid, cancellationToken);
}
```

For a web application, redirect the browser instead of opening it locally. Store the OAuth state and expected DID together
in trusted server-side session storage, then restore both in the callback handler. Do not take the expected DID from
untrusted callback parameters.

## Upgrade the same authenticated agent

The following continues the configuration example, with `handle` and `cancellationToken` supplied by your application.
First authorize only timeline access:

```c#
Did expectedDid = await agent.ResolveHandle(handle, cancellationToken)
    ?? throw new OAuthException("Could not resolve the account DID.");

if (!await Authorize(agent, handle, initialScopes, expectedDid, cancellationToken) ||
    !agent.IsAuthenticated)
{
    throw new OAuthException("Initial authorization failed.");
}

var timeline = await agent.GetTimeline(limit: 5, cancellationToken: cancellationToken);
if (!timeline.Succeeded)
{
    Console.Error.WriteLine(
        $"Timeline failed: HTTP {(int)timeline.StatusCode}, " +
        $"{timeline.AtErrorDetail?.Error}: {timeline.AtErrorDetail?.Message}");
    return;
}
Console.WriteLine($"Read {timeline.Result.Count} timeline entries.");
```

When the user opts into posting, capture the authenticated DID and start another authorization with the expanded scopes:

```c#
Did originalDid = agent.Credentials.Did;

if (!await Authorize(agent, handle, expandedScopes, originalDid, cancellationToken) ||
    !agent.IsAuthenticated)
{
    throw new OAuthException("Progressive authorization failed.");
}

var postResult = await agent.Post(
    "Hello OAuth Permission Sets", cancellationToken: cancellationToken);
if (!postResult.Succeeded)
{
    Console.Error.WriteLine(
        $"Post failed: HTTP {(int)postResult.StatusCode}, " +
        $"{postResult.AtErrorDetail?.Error}: {postResult.AtErrorDetail?.Message}");
    return;
}

var deleteResult = await agent.DeletePost(
    postResult.Result.StrongReference, cancellationToken: cancellationToken);
if (!deleteResult.Succeeded)
{
    Console.Error.WriteLine(
        $"Cleanup failed: HTTP {(int)deleteResult.StatusCode}, " +
        $"{deleteResult.AtErrorDetail?.Error}: {deleteResult.AtErrorDetail?.Message}. " +
        $"Delete {postResult.Result.Uri} manually.");
}

await agent.Logout(cancellationToken: cancellationToken);
```

The successful callback replaces the same agent's credentials; there is no intervening logout. A DID mismatch throws
`OAuthException` before replacement. This flow does not explicitly revoke the previous grant or assume the provider
revokes it automatically. Final logout revokes the current credentials.

Applications normally request more permissions when the user enables a feature, rather than deliberately attempting an
unauthorized write. If you do attempt a write to demonstrate enforcement, inspect its actual HTTP result:

```c#
if (!postResult.Succeeded &&
    postResult.StatusCode == HttpStatusCode.Forbidden &&
    postResult.AtErrorDetail is ScopeMissingError or InsufficientScope)
{
    Console.WriteLine(
        $"Additional permission is required: {postResult.AtErrorDetail.Error}: " +
        $"{postResult.AtErrorDetail.Message}");
}
```

Do not treat every HTTP 403, expired token, rate limit, or network failure as a missing-scope response, and do not retry
an ambiguous write automatically. Use the same record key when retrying a known rejected write to avoid duplicate posts.
The snippets show the authorization sequence; production code should also arrange cleanup and logout on failure or
cancellation, and report any public post that remains after an unsuccessful cleanup.

The [Progressive OAuth Sample](https://github.com/blowdart/idunno.Bluesky/tree/main/samples/Samples.ProgressiveOAuth)
starts with only the granular `getTimeline` RPC permission and `atproto`, displays the authenticated timeline,
and attempts an actual post. It prints the HTTP result and server error before requesting the create/delete Permission
Sets and retrying the same post under the same DID. Unlike the broader `ViewAll` set, the initial scope grants no access
to notifications or preferences. Servers granting broader access initially cannot demonstrate the negative case;
the sample reports this explicitly and avoids creating a second post.
