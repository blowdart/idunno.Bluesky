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

## Logging out

To log the authenticated user off Bluesky call the `Logout()` method. This revokes the refresh token (and,
if the agent was authenticated via OAuth, the access token), and clears the now revoked credentials from the agent.


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

Your application must have a client id and publish a metadata document in the format required by the [ATProto OAuth specification](https://atproto.com/specs/oauth#clients).
For development a client ID of `http://localhost` is special cased by the specification, allowing you to develop your application without the need to have
published your application metadata file.

To use OAuth first configure the OAuth options for your agent. The options require the application `ClientId` and the `Scopes` your application requires,
and the `ReturnUri` from which your application will process OAuth logins. For web applications this will be a web page, for desktop applications
this is typically a custom uri scheme you have registered with the OS.

```c#
using idunno.AtProto.Authentication;
using idunno.Bluesky;

var agent = new BlueskyAgent(new BlueskyAgentOptions()
    {
        OAuthOptions = new OAuthOptions()
        {
            ClientId = "https://example.com/oauth-client-metadata.json",
            Scopes = ["atproto"],
            ReturnUri = new Uri("https://example.com/oauth/callback")
        }
    });
```

To act on the user's behalf, choose only the permissions required for your features. See [OAuth permissions](oauth/oauthPermissions.md) for granular scopes and typed permission sets.

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
> OAuth discovery follows endpoints associated with a user-supplied handle. Keep discovery validation enabled and review the
> [discovery validation guidance](productionConfiguration.md#discovery-validation) before deploying.

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

For ASP.NET applications, the [ASP.NET authentication package](asp.net.md) manages the login flow and can
generate and serve the client metadata document.

### Typed permission sets

See [OAuth permissions](oauth/oauthPermissions.md#typed-permission-sets) for built-in sets, custom audiences and scope overrides.

### Progressive authorization

See [Progressive authorization](oauth/progressiveAuthorization.md) to request more permissions when a user enables a feature.

### Testing OAuth locally with localhost

See [Local OAuth development](oauth/localOAuthDevelopment.md) for the callback server and a complete local-login example.

## Production Configuration

See [Production configuration](productionConfiguration.md) for deployment URLs, persistent stores, trusted proxies, signing keys and operational checks.

## Configuring the agent's HTTP settings

See [Agent HTTP configuration](httpConfiguration.md) for outgoing request settings.

### <a name="configuringTimeouts">Configuring HTTP timeouts</a>

See [Configuring HTTP timeouts](httpConfiguration.md#configuringTimeouts).

### <a name="settingUserAgent">Setting the user agent</a>

See [Setting the user agent](httpConfiguration.md#settingUserAgent).

### <a name="usingAProxy">Using a proxy server</a>

See [Using a proxy server](httpConfiguration.md#usingAProxy), including certificate-revocation cautions.

### <a name="disablingTokenRefresh">Disabling token refresh</a>

See [Automatic and manual token refresh](savingAndRestoringAuthentication.md#disablingTokenRefresh).
