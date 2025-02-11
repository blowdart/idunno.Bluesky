# <a name="connecting">Connecting to Bluesky</a>

## <a name="usernamesAndPasswords">Authenticating with handles and passwords</a>

As you can see from the [Hello World](../index.md#gettingStarted) example connecting to Bluesky consists of creating an instance of a `BlueskyAgent`
and then calling the login method.

```c#
using (BlueskyAgent agent = new ())
{
    var loginResult =  await agent.Login(handles, password);
    if (loginResult.Succeeded && agent.Session is not null)
    {
        // Do your Bluesky thing
    }
}
```

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
`ResolveDIDDocument` method in the `DirectoryServer` to discover the location of their PDS. Once you have a user's PDS all write, update and delete operations
should be performed against that PDS. The `AtProtoAgent` `Login` method does this behind the scenes so you don't have to.

## <a name="oauth">Authenticating with OAuth</a>

A more secure alternative to handles and passwords is OAuth. OAuth is a standard that allows users to grant access to their resources without having to share
their credentials with an application. To use OAuth your application prepares a login URI and redirects the user to it, or in the case of desktop apps,
opens a browser window to it. The user logs into Bluesky if needed then authorizes your application to access their resources. 

[Bluesky's OAuth implementation](https://docs.bsky.app/docs/advanced-guides/oauth-client) is, at the time of writing, very much under development.
Currently Bluesky has three different resources, or "[scopes](https://atproto.com/specs/oauth#authorization-scopes)",

* `atproto`: which gives your application the unique identifier for the user. This could be used for "Login with Bluesky" type functionality. This scope must always be requested.
* `transition:generic`: which gives your application access to all permissions except for direct message access.
* `transition:chat.bsky`: which gives your application direct message access.

Your application must have a client id and publish a metadata document in the format required by the [ATProto OAuth specification](https://atproto.com/specs/oauth#clients).
For development a client it of `http://localhost` is special cased by the specification, allowing you to develop your application without the need to have
published your application metadata file.

To use OAuth first configure the OAuth options for your agent. The options require the application `ClientId` and the `Scopes` your application requires,
and the the `ReturnUri` from which your application will process OAuth logins. For web applications this will be a web page, for desktop applications
this is typically a custom uri scheme you have registered with the OS.

```c#
var agent = new BlueskyAgent(
    new BlueskyAgentOptions()
    {
        OAuth = new BlueskyOAuthOptions()
        {
            ClientId = "https://example.com",
            Scopes = new[] { "atproto", "transition:generic" },
            ReturnUri = new Uri("https://example.com/oauth/callback")
        }
    });
```

To start an OAuth login process you must first create an instance of `OAuthClient` build a URI to send the user to,

```c#
OAuthClient oAuthClient = agent.CreateOAuthClient();

Uri startUri = await agent.BuildOAuth2LoginUri(oAuthClient, handle, cancellationToken: cancellationToken);

// Send the user to the startUri in a way suitable for your application, a redirection for web application or spawning a browser for a desktop application.
```

Once the login process has completed you should then call 




To generate the OAuth flow start URI create an `OAuthClient` using `CreateOAuthClient()`

## Configuring the agent's HTTP settings

The constructor for the agents take optional parameters that allow you to configure the underlying
[HttpClient](https://learn.microsoft.com/en-us/dotnet/api/system.net.http.httpclient) used to make requests and receive response.

### <a name="configuringTimeouts">Configuring HTTP timeouts</a>

Use the `timeout` parameter when creating an instance of the agent and provide a [TimeSpan](https://learn.microsoft.com/en-us/dotnet/api/system.timespan)
to set the amount of time to wait before the request times out. For example, the following code will configure the agent to wait one minute for any server
it makes requests against to respond.

```c#
using (var agent = new BlueskyAgent(timeout : new Timeout(0,1,0))
{
}
```

### <a name="settingUserAgent">Setting the user agent</a>

Each request the agent makes is stamped with a string indicating the identity of the software making the request. By default this value is set to
`idunno.AtProto/x.x.x`, where x.x.x is the version of the library being used. This is sent as the
[UserAgent HTTP header](https://datatracker.ietf.org/doc/html/rfc7231#section-5.5.3) in every request.
You should set the user agent to be a value indicating your own software's identity.

### <a name="usingAProxy">Using a proxy server</a>

The agent constructor `proxyUri` parameter allows you to set an `HttpProxy` to be used by the agent when making outgoing HTTP requests.
If you are using a debugging proxy such as [Fiddler](https://www.telerik.com/fiddler) or [Burp Suite](https://portswigger.net/burp) it is
likely that you will also need to set the `checkCertificateRevocationList` to `false`, 

> [!CAUTION]
> Setting `checkCertificateRevocationList` to `false` is dangerous, as the client will no longer check if the HTTPS certificate on any server
> it connects to has been revoked.
> 
> Only use set this to `false` when you are using a debugging proxy which does not support CRLs.

```c#
// Disabling certification revocation list checks can introduce security vulnerabilities.
// Only use this setting when using a debugging proxy such as Fiddler or Burp Suite.

using (var agent = new BlueskyAgent(
    proxyUri : new Uri("http://localhost:8866"),
    checkCertificateRevocationList: false )
{
}
```

### <a name="disablingTokenRefresh">Disabling token refresh</a>

If you want to disable automatic authentication token refresh in an agent you can do that by the `EnableTokenRefresh` property in options to false.
Eventually the access token will expire and APIs will start returning errors. You can call `RefreshToken()` to refresh the access token manually.

```c#
var options = new BlueskyAgentOptions() { EnableBackgroundTokenRefresh = false };

using (BlueskyAgent agent = new (options)
{   
    // No token refresh will occur, so eventually API calls will fail.
}
```
