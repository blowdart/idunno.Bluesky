# <a name="gettingStarted">Getting started</a>

As is now tradition we start with "Hello Word".

```c#
using idunno.Bluesky.

using (BlueskyAgent agent = new ())
{
    var loginResult = await agent.Login(username, password);
    if (loginResult.Succeeded)
    {
        var response = 
            await agent.Post("Hello World");

       if (response.Succeeded)
       {
          // It worked.
       }
   }
}
```
## <a name="makingRequests">Making requests to Bluesky and understanding the results</a>

All the supported Bluesky operations are contained in the `BlueskyAgent` class, which takes care of session management for you.

Most requests to Bluesky are made over [HTTP](https://docs.bsky.app/docs/category/http-reference) and the results are wrapped up in an `HttpResult` instance, which contains any successful result returned, along with the `200 - Ok` status code, or an HTTP status code indicating an error, and, optionally, some error details. More detailed information on error handling can be found in [Error Handling](#errorHandling)

## <a name="connecting">Connecting to Bluesky</a>

As you can see from the [Hello World](#gettingStarted) example connecting to Bluesky consists of creating an instance of a `BlueskyAgent` and then calling the login method.

```c#
using (BlueskyAgent agent = new ())
{
    HttpResult<bool> loginResult =  await agent.Login(username, password);
    if (loginResult && agent.Session is not null)
    {
        // Do your Bluesky thing
    }
}
```

When a login is successful the agent will store the information needed for subsequent API calls in its `Session` property.
API calls that require authentication will use this information and the access tokens will, by default, refresh automatically.

If a user has email based two-factor authentication logins need an extra step.
The first login attempt will fail with an `AuthFactorTokenRequired` error, at which point you should prompt the user to enter their sign-in code,
and call `Login` again, this time with the username, password and the sign-in code.

```c#
HttpResult<bool> loginResult = await agent.Login(username, password);

if (!loginResult &&
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

Note that app passwords don't currently require authentication codes.

Some users run their own Personal Data Servers, and/or have an [decentralized identifier](https://www.w3.org/TR/did-core/) (DID) that isn't part
of the [directory](https://directory.plc) Bluesky runs. Whilst you can simply ask a user for their PDS location
you can also use the `ResolveHandle` method in the `AtProtoAgent` class to resolve a handle to a DID, and then use the
`ResolveDIDDocument` method in the `DirectoryServer` to discover the location of their PDS. Once you have a user's PDS all write and delete operations
should be performed against that PDS. The `AtProtoAgent` `Login` method does this behind the scenes so you don't have to.

### <a name="usingAProxy">Using a proxy server<a>

The agent constructors can take an `HttpClient` that you can use to configure a proxy for each request the agent makes.
For example, to use Fiddler as a proxy you would initialize the agent using the following code.

```c#
var proxy = new WebProxy
{
    Address = new Uri("http://localhost:8866"),
    BypassProxyOnLocal = true,
    UseDefaultCredentials = true
};

var proxyClientHandler = new HttpClientHandler
{
    Proxy = proxy,
    UseProxy = true,
    ServerCertificateCustomValidationCallback =  HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
};

using (var proxyClient = new HttpClient(handler: proxyClientHandler, disposeHandler: true))
{
    BlueskyAgent agent = new (proxyClient)
    {
        HttpResult<Did> did = await agent.ResolveHandle("blowdart.me");
    }
}
```

### <a name="disablingTokenRefresh">Disabling token refresh</a>

If you want to disable automatic token refresh you can do that by the `EnableTokenRefresh` property in options to false.

```c#
var options = new BlueskyAgentOptions() { EnableBackgroundTokenRefresh = false };

using (BlueskyAgent agent = new (options)
{   
    HttpResult<Did> did = await agent.ResolveHandle("blowdart.me");
}
```

## <a name="errorHandling">Error Handling</a>
Every call through an agent returns an `AtProtoHttpResult<T>`. This approach, which you may recognise from ASP.NET, avoids the use of exceptions should the HTTP call fail,
and also allows you to view any extra error information the Bluesky APIs may return.

The success or fail possibilities are wrapped together in an `HttpResult<T>` that is returned by every API call.

For example, a login call returns an `AtProtoHttpResult<bool>`. To check the operation succeeded you would

1. Check the that the `AtProtoHttpResult<T>` is true, which indicates the underlying request returned a `200 - Ok` status code, and a result is available.
2. If it's `true`, you can use the `Result` property and continue on your way.
   If it's `false` you can use the `StatusCode` property to examine the HTTP status code returned by the API and, if the API has given a detailed error response, you can use the `Error` property to view any extended error information returned, which may have an `Error` and a `Message` set.
1. If the `StatusCode` properly is `HttpStatusCode.OK` then the API call succeeded but no result was returned, which shouldn't happen.

When calling an API you should use the following pattern to check for errors.

```c#
// Make an API call to get the timeline for the current user.
HttpResult<Timeline> timelineResult = await agent.GetTimeline();

if (timelineResult)
{
    // Everything was successful, continue on with your code
}
else
{
    Console.WriteLine($"getTimelineResult failed: {timelineResult.StatusCode}.");
    Console.Write($"\t{timelineResult.Error.Error} : ");
    Console.WriteLine($"\t{timelineResult.Error.Message}");
}
```

The`AtProtoHttpResult<T>` `StatusCode` property exposes the HTTP status code the API endpoint returned. If the `StatusCode` is not `Succeeded` then an HTTP error occurred during the API fall.
Further error information returned from the API may be present in the `Error` property.

## <a name="atURIs">AT URIs</a>

You may notice from the [timeline sample](#timeline) that `FeedView` has a `Uri` property. This is not an HTTPS URI, it is an [AT URI](https://atproto.com/specs/at-uri-scheme). 

An AT URI is a unique reference to an individual record on the network, and actions that work on records, such as liking a post, require the AT URI for the record they're acting on or for.

## Actors and profiles

## Resolving a Handle to a DID

