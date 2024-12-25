# <a name="connecting">Connecting to Bluesky</a>

As you can see from the [Hello World](../index.md#gettingStarted) example connecting to Bluesky consists of creating an instance of a `BlueskyAgent`
and then calling the login method.

```c#
using (BlueskyAgent agent = new ())
{
    var loginResult =  await agent.Login(username, password);
    if (loginResult.Succeeded && agent.Session is not null)
    {
        // Do your Bluesky thing
    }
}
```

When a login is successful the agent will store the information needed for subsequent API calls in its `Session` property, exchanging the handle
and password for tokens. API calls that require authentication will use this information and the access tokens will, by default, refresh automatically.

> [!IMPORTANT]
> If you are writing an application or web service you obviously shouldn't save your users' passwords. The agent has
> [events that allow you to react to logins, logouts and token refreshes](savingAndRestoringAuthentication.md) to allow you
> to save tokens rather than credentials.

If a user has email based two-factor authentication logins need an extra step.
The first login attempt will fail with an `AuthFactorTokenRequired` error, at which point you should prompt the user to enter their sign-in code,
and call `Login` again, this time with the username, password and the sign-in code.

```c#
var loginResult = await agent.Login(username, password);

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
        loginResult = await agent.Login(username, password, emailAuthenticationCode);
    }
}
```

> [!TIP]
> Bluesky allows you to create "[app passwords](https://bsky.app/settings/app-passwords)", which you can use instead of your real password.
> Try using an app password in the sample above. If you have multi-factor authentication enabled on Bluesky app passwords don't require MFA.

Some users run their own Personal Data Servers, and/or have an [decentralized identifier](https://www.w3.org/TR/did-core/) (DID) that isn't part
of the [directory](https://web.plc.directory/) Bluesky runs. Whilst you can simply ask a user for their PDS location
you can also use the `ResolveHandle` method in the `AtProtoAgent` class to resolve a handle to a DID, and then use the
`ResolveDIDDocument` method in the `DirectoryServer` to discover the location of their PDS. Once you have a user's PDS all write and delete operations
should be performed against that PDS. The `AtProtoAgent` `Login` method does this behind the scenes so you don't have to.

## <a name="usingAProxy">Using a proxy server<a>

The agent constructors can take an `HttpClient` if you want to customize agent headers or other http properties.
This can be useful if you want to use a proxy server. The agent classes contain a helper method,
`CreateConfiguredHttpClient()` which will create an `HttpClient` configured to use a proxy server,
and/or set a custom user agent string. You can then pass this `HttpClient` to the agent constructor.

> [!Note]
> If you specify a proxy URI when calling `CreateConfiguredHttpClient()` the HttpClient returned will be configured to not check
> Certificate Revocation lists as typically the HTTPS certificates created by proxy servers like Fiddler or Burp Suite don't have CRL endpoints.

```c#
var httpClient = Agent.CreateHttpClientForProxy("http://localhost:8866", "mydotnetclient/1.0");

BlueskyAgent agent = new (httpClient)
{
    // And now this instance of agent will use your client.
}
```

> [!Important]
> If you are creating your own `HttpClient` make sure you configure the `HttpClientHandler` wrapping it to enable `AutomaticDecompression`.

```c#
HttpClientHandler handler = new ()
{
    AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
};

HttpClient httpClient = new (handler);

BlueskyAgent agent = new (httpClient)
{
    // And now this instance of agent will use your handler.
}
```

## <a name="disablingTokenRefresh">Authentication State</a>

If you want to disable automatic authentication token refresh in an agent you can do that by the `EnableTokenRefresh` property in options to false.
Eventually the access token will expire and APIs will start returning errors. You can call `RefreshToken()` to refresh the access token manually.

```c#
var options = new BlueskyAgentOptions() { EnableBackgroundTokenRefresh = false };

using (BlueskyAgent agent = new (options)
{   
    // No token refresh will occur, so eventually API calls will fail.
}
```
