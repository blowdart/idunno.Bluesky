# <a name="connecting">Connecting to Bluesky</a>

## <a name="usernamesAndPasswords">Authenticating with handles and passwords</a>

As you can see from the [Hello World](../index.md) example connecting to Bluesky consists of creating an instance of a `BlueskyAgent`
and then calling the login method.

```c#
using (BlueskyAgent agent = new ())
{
    var loginResult =  await agent.Login(handle, password);
    if (loginResult.Succeeded)
    {
        // Do your Bluesky thing
    }
}
```

> [!TIP]
> Handles [do not start with an @ sign](https://atproto.com/specs/handle), that's just how the official app and
> other apps choose to display them. When trying to login, or use other methods that take a handle make sure your
> strings do not begin with the @ sign.

When a login is successful the agent will store the information needed for subsequent API calls in its `Credentials` property, exchanging the handle
and password for tokens. API calls that require authentication will use this information and the access tokens will, by default, refresh automatically.

> [!IMPORTANT]
> If you are writing an application or web service you shouldn't save your users' passwords. The agent has
> [events that allow you to react to logins, logouts and token refreshes](savingAndRestoringAuthentication.md) to allow you
> to save authentication tokens rather than credentials.

If a user has email based two-factor authentication logins need an extra step.
The first login attempt will fail with an `AuthFactorTokenRequired` error, at which point you should prompt the user to enter their sign-in code,
and call `Login` again, this time with the username, password and the sign-in code.

```c#
var loginResult = await agent.Login(handle, password);

if (!loginResult.Succeeded &&
    string.Equals(
        loginResult.Error.Error!, 
        "AuthFactorTokenRequired", 
        StringComparison.OrdinalIgnoreCase))
{
    Console.WriteLine("Account requires an email authentication code.");
    Console.WriteLine("Enter the email authentication code or press return to exit:");
    string? emailAuthenticationCode = Console.ReadLine();

    if (!string.IsNullOrEmpty(emailAuthenticationCode))
    {
        // Try again with the auth code.
        loginResult = await agent.Login(handle, password, emailAuthenticationCode);
    }
}
```

> [!TIP]
> Bluesky allows you to create "[app passwords](https://bsky.app/settings/app-passwords)", which you can use instead of your real password.
> Try using an app password in the sample above. If you have multi-factor authentication enabled on Bluesky app passwords don't require MFA.

Some users run their own Personal Data Servers, and/or have an [decentralized identifier](https://www.w3.org/TR/did-core/) (DID) that isn't part
of the [directory](https://web.plc.directory/) Bluesky runs. Whilst you can simply ask a user for their PDS location
you can also use the `ResolveHandle` method in the `AtProtoAgent` class to resolve a handle to a DID, and then use the
`ResolveDidDocument` method in the `DirectoryServer` to discover the location of their PDS. Once you have a user's PDS all write, update and delete operations
should be performed against that PDS. The `AtProtoAgent` `Login` method does this behind the scenes so you don't have to.

## <a name="oauth">Authenticating with OAuth</a>

A more secure alternative to handles and passwords is OAuth. OAuth is a standard that allows users to grant access to their resources without having to share
their credentials with an application. To use OAuth your application prepares a login URI and redirects the user to it, or in the case of desktop apps,
opens a browser window to it. The user logs into Bluesky if needed then authorizes your application to access their resources. 

[Bluesky's OAuth implementation](https://docs.bsky.app/docs/advanced-guides/oauth-client) supports
[granular permissions and published permission sets](https://atproto.com/guides/permission-requests).
The `atproto` identity scope is the minimum scope needed for AT Protocol OAuth and must always be requested.
On its own, it supports a simple "Login with Bluesky" or "Login with your Atmosphere Account" function:
after the user authorizes your application, the authenticated session identifies the account by its DID.
Your application can use that stable identifier to sign the user in without requesting permission to post,
modify their profile, read private preferences, or access their direct messages.

For an application that only needs account authentication, the default `OAuthOptions.Scopes` value of `["atproto"]`
is sufficient; leave `PermissionSets` empty. Request additional permissions only when the application needs to act
on the user's behalf beyond identifying their account.

For access to Bluesky features, prefer published permission sets
or granular scopes that cover only the features your application needs. The legacy transition scopes remain supported
for compatibility, but are not recommended for new applications:

* `transition:generic`: which gives your application broad access to records across applications, except for direct message access.
* `transition:chat.bsky`: which gives your application direct message access; prefer `BlueskyOAuthPermissionSets.FullChatClient`.

Your application must have a client id and publish a metadata document in the format required by the [ATProto OAuth specification](https://atproto.com/specs/oauth#clients).
For development a client ID of `http://localhost` is special cased by the specification, allowing you to develop your application without the need to have
published your application metadata file.

To use OAuth first configure the OAuth options for your agent. The options require the application `ClientId` and the `Scopes` your application requires,
and the `ReturnUri` from which your application will process OAuth logins. For web applications this will be a web page, for desktop applications
this is typically a custom uri scheme you have registered with the OS.

```c#
using idunno.AtProto.Authentication;
using idunno.Bluesky.Authentication;

var agent = new BlueskyAgent(new BlueskyAgentOptions()
    {
        OAuthOptions = new OAuthOptions()
        {
            ClientId = "https://example.com",
            Scopes = ["atproto"],
            PermissionSets = [BlueskyOAuthPermissionSets.FullApp],
            ReturnUri = new Uri("https://example.com/oauth/callback")
        }
    });
```

This example requests access for a full Bluesky social client. For a smaller application, select a narrower set
such as `CreatePosts` or `ManageProfile` instead. `FullApp` is not a drop-in replacement for `transition:generic`:
it covers Bluesky features rather than records across all applications, and does not include blob uploads or chat.
Add an appropriate blob scope, such as `blob:image/*`, only when your application needs to upload media.

### Typed permission sets

Use `OAuthOptions.PermissionSets` to request published sets alongside raw `Scopes`. The default raw scope is `atproto`,
which is still required; permission sets do not include it. For example, a posting application can request:

```c#
using idunno.AtProto.Authentication;
using idunno.Bluesky.Authentication;

OAuthOptions options = new("https://example.com/oauth-client-metadata.json")
{
    ReturnUri = new Uri("https://example.com/oauth/callback"),
    Scopes = ["atproto", "blob:image/*"],
    PermissionSets = [BlueskyOAuthPermissionSets.CreatePosts]
};
```

Assign these options to `BlueskyAgentOptions.OAuthOptions` (or `AtProtoAgentOptions.OAuthOptions`).
`BlueskyOAuthPermissionSets` includes `CreatePosts`, `DeleteContent`, `FullApp`, `ManageFeedDeclarations`,
`ManageLabelerService`, `ManageModeration`, `ManageNotifications`, `ManageProfile`, `ViewAll`, and `FullChatClient`.
Sets requiring inherited RPC audiences use the default Bluesky app view or chat audience. `FullApp` does not
include chat access or blob uploads. Blob, account, and identity permissions must be requested separately.

For custom AT Protocol lexicons or another service audience, construct a permission-set reference:

There are no published `com.atproto.*` permission sets in the upstream lexicons. The `com.example.authBasic`
name below is a placeholder: replace it with a permission-set lexicon you have actually published.

```c#
options.PermissionSets =
[
    new OAuthPermissionSet("com.example.authBasic", "did:web:api.example.com#appview")
];
```

The authorization server resolves the `include:` reference to the published lexicon. `options.GetRequestedScopes()` returns
the combined, deduplicated scope strings; publish the same scopes in your client metadata document.
No metadata generation or serving is performed by these APIs.

Raw scopes remain supported unchanged. An explicit `scopes` argument on an OAuth request overrides both configured
raw scopes and permission sets. The effective client ID and requested scopes are saved with the login state and
issued credentials, so callback processing and subsequent refreshes retain the original request even if options change.
Older saved state and credentials without this context continue to use the configured options.
Typed sets also convert to strings when placed in a raw scope collection:

```c#
string[] scopes = ["atproto", "blob:image/*", BlueskyOAuthPermissionSets.CreatePosts];
```

To start an OAuth login process you must first create an instance of `OAuthClient` build a URI to send the user to, save the state from the
OAuthClient and then send the user to the URI. The user will log into Bluesky and authorize your application, and then be redirected back to your application

```c#
OAuthClient oAuthClient = agent.CreateOAuthClient();
Uri startUri = await agent.BuildOAuth2LoginUri(oAuthClient, handle, cancellationToken: cancellationToken);

// Save the state, and persist it in whatever way is suitable for your application,
// to be used when the response comes back from the OAuth server.
OAuthLoginState oAuthLoginState = oAuthClient.State;

// Send the user to the startUri in a way suitable for your application,
// a redirection for web application or spawning a browser for a desktop application.
```

> [!WARNING]
> `AtProtoAgent.BuildOAuth2LoginUri` uses discovery mechanisms to resolve the PDS `Uri` and the Authorization Server `Uri`
> for the specified handle. A malicious user could supply a handle which returns URIs that point to internal
> host names or malicious authorization servers. A malicious PDS resolution would cause your application to issue
> requests to the `.well-known/oauth-protected-resource` path against a host name they control.
> A malicious authorization server would redirect the user to login on an authorization server under attacker control,
> but at that point the malicious user is redirecting themselves. Neither of these feel particularly concerning,
> but you should be aware of the possibility if you are writing an application that could be hosted with a
> corporate environment.
>
> `AtProtoAgent.BuildOAuth2LoginUri` accepts two optional parameters, `validatePds` and `validateAuthorizationServer` which
> are both callback methods which you can use to validate the URIs discovered during the building of an OAuth2
> login URI. You can use these methods to mitigate against
> [SSRF](https://owasp.org/www-community/attacks/Server_Side_Request_Forgery) attacks and/or to validate
> the authorization server is one you expect.
>
> A default implementation of discovery validation (`SecurityHelpers.DefaultDiscoveryUriValidator`)
> which rejects any PDS or authorization server that doesn't resolve to a public and safe IP address when
> `BuildOAuth2LoginUri` is called without a `validatePds` or `validateAuthorizationServer` callback,
> or with either of those parameters set to null and the `validateDiscoveredEndpoints` option set to `true`.

When the user returns to your application you take the callback data returned from the OAuth server and process it

```
// Create an oauth client using the saved state
OAuthClient oAuthClient = agent.CreateOAuthClient(oAuthLoginState);

// Process the response
bool authenticated = await agent.ProcessOAuth2LoginResponse(oAuthClient, callbackData, cancellationToken);
```

The mechanisms for getting the login callback data, saving the state and restoring it vary due to application type.
Please consult the documentation for your application architecture. The `Uri` returned by `BuildOAuth2LoginUri` will
contain a `state` query parameter, which can use as a primary key as needed for persisting the client state. You can extract this using
`string stateKey = QueryHelpers.ParseQuery(startUri.Query)["state"]!;`.

### Progressive authorization

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

#### Declare the maximum permissions, then request a subset

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

#### Authorize and validate the callback identity

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

#### Upgrade the same authenticated agent

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

### Testing OAuth locally with localhost

The `idunno.AtProto.OAuthCallback` NuGet package contains a simple web server that can be used to test OAuth logins locally. To use it add a reference
to the package, set the  ClientId in options to "`http://localhost`" but do not set the ReturnUri, then create an instance of the callback server
before you build the login URI, use the callback server uri when creating the login URI, and finally await the callback,
which will return the callback data as a string.
Use `CallbackServer.CreateAsync()` to reserve the loopback sockets and wait until the listener is ready before building the login URI.

```c#

using idunno.AtProto.Authentication;
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

## Production Configuration

[!include[Production configuration](includes/production-configuration.md)]

## Logging out

To log the authenticated user off Bluesky call the `Logout()` method. This revokes the refresh token (and,
if the agent was authenticated via OAuth, the access token), and clears the now revoked credentials from the agent.

## Configuring the agent's HTTP settings

The constructor for the Bluesky agents takes an instance of `BlueskyAgentOptions` which allows for configuration of the agents. The `BlueskyAgentOptions` class
contains an `HttpClientOptions` property which allows you to specify options for the underlying
[HttpClient](https://learn.microsoft.com/en-us/dotnet/api/system.net.http.httpclient)s used to make requests and receive responses.

### <a name="configuringTimeouts">Configuring HTTP timeouts</a>

Use the `Timeout` options on `HttpClientOptions` when creating an instance of the agent and provide a [TimeSpan](https://learn.microsoft.com/en-us/dotnet/api/system.timespan)
to set the amount of time to wait before the request times out. For example, the following code will configure the agent to wait one minute for any server
it makes requests against to respond.

```c#
using (var agent = new BlueskyAgent(new BlueskyAgentOptions()
  {
      HttpClientOptions = new HttpClientOptions()
      {
          Timeout = TimeSpan.FromMinutes(1)
      }
  }))
{
}
```

### <a name="settingUserAgent">Setting the user agent</a>

Each request the agent makes is stamped with a string indicating the identity of the software making the request. By default this value is set to
`idunno.AtProto/x.x.x`, where x.x.x is the version of the library being used. This is sent as the
[UserAgent HTTP header](https://datatracker.ietf.org/doc/html/rfc7231#section-5.5.3) in every request.
You should set the `HttpClientOptions` `HttpUserAgent` property to be a value indicating your own software's identity.

### <a name="usingAProxy">Using a proxy server</a>

The `HttpClientOptions` `ProxyUri` property allows you to set a proxy to be used by the agent when making outgoing HTTP requests.
If you are using a debugging proxy such as [Fiddler](https://www.telerik.com/fiddler) or [Burp Suite](https://portswigger.net/burp) it is
likely you may also need to set the `CheckCertificateRevocationList` property to `false`.

> [!CAUTION]
> Setting `CheckCertificateRevocationList` property on `HttpClientOptions` to `false` is dangerous,
> as the client will no longer check if the HTTPS certificate on any server it connects to has been revoked.

```c#
// Disabling certification revocation list checks can introduce security vulnerabilities.
// Only use this setting when using a debugging proxy such as Fiddler or Burp Suite.

using (var agent = new BlueskyAgent(new BlueskyAgentOptions()
    {
        HttpClientOptions = new HttpClientOptions()
        {
            ProxyUri = new Uri("http://localhost:8866"),
            CheckCertificateRevocationList = false
        }
    }))
{
}
```

### <a name="disablingTokenRefresh">Disabling token refresh</a>

If you want to disable automatic authentication token refresh in an agent you can do that by setting the `EnableBackgroundTokenRefresh` property in options to `false`.
Eventually the access token will expire and APIs will start returning errors. You can call `RefreshCredentials()` to refresh the access token manually.

```c#
var options = new BlueskyAgentOptions() { EnableBackgroundTokenRefresh = false };

using (BlueskyAgent agent = new (options))
{   
    // No token refresh will occur, so eventually API calls will fail.
}
```
