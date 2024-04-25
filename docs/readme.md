# idunno.AtProto.

## A .NET class library for using Bluesky

This library contains classes for posting to, and reading the [Bluesky social network](https://bsky.app/), using the from the [atproto protocol](https://docs.bsky.app/docs/api/at-protocol-xrpc-api). Bluesky's HTTP Reference can be found in their [documentation](https://docs.bsky.app/docs/category/http-reference).

You can view a list of [completed APIs Bluesky and AT Proto endpoints](endpointStatus.md).

## <a name="gettingStarted">Getting started</a>

As is now tradition let us start with "Hello Word".

```c#
BlueskyAgent agent = new ();
HttpResult<bool> loginResult = await agent.Login(username, password);
if (loginResult.Succeeded && agent.Session is not null)
{
    HttpResult<CreateRecordResponse> response = await agent.CreatePost("Hello World");
    if (response.Succeeded)
    {
       // It worked.
       // To get the results of the post creation you can check response.Result
    }
}
```

## Making requests to Bluesky and understanding the results

All the supported Bluesky operations are contained in the `BlueskyAgent` class, which takes care of session management for you.

Requests to Bluesky are made over [HTTP](https://docs.bsky.app/docs/category/http-reference) and the results are wrapped up in an `HttpResult` instance, which contains any successful result returned, along with the `200 - Ok` status code, or an HTTP status code indicating an error, and, optionally, some error details. 

These two possibilities are wrapped together in an `HttpResult` that is returned by every API call.

For example, a login call returns an `HttpResult<bool>`. To check the operation succeeded you would

1. Check the `Succeeded` property on the `HttpResult`.
2. If `Succeded` is `true` you can use the `Result` property and continue on your way.
3. If `Succeded` is `false` you can use the `StatusCode` property to examine the HTTP status code returned by the API and, if the API has given a detailed error response, you can use the `Error` property to view any extended error information returned, which may have an `Error` and a `Message` set.

For a successful login `Succeded` will be `true`, and the `Result` property is available. 

For a failed login `Succeded` will be `false`, the `StatusCode` property will be `HttpStatusCode.Unauthorized`, and, as this API returns a detailed error, the `Error` property will be populated, with an `Error` of "AuthenticationRequired" and `Message` of "Invalid identifier or password".

## Connecting to Bluesky

As you can see from the [Hello World](#gettingStarted) example connecting to Bluesky consists of creating an instance of a `BlueskyAgent` and calling the login method.

```c#
using (BlueskyAgent agent = new ())
{
    HttpResult<bool> loginResult = await agent.Login(username, password);
    if (loginResult.Succeeded && agent.Session is not null)
    {
        // Do your Bluesky thing
    }
}
```

When a login is successful the agent will store the information needed for subsequent API calls in its `Session` property. API calls that require authentication will use this information automatically. The session tokens will also be refreshed automatically.

### Using a proxy server

The agent constructors can take an `HttpClientHandler` that you can use to configure a proxy for each request the agent makes. For example, to use Fiddler as a proxy you would initialize the agent using the following code.

```c#
var proxy = new WebProxy
{
    Address = new Uri($"http://127.0.0.1:8888"),
    BypassProxyOnLocal = false,
    UseDefaultCredentials = false,
};

using (var httpClientHandler = new HttpClientHandler { Proxy = proxy })
{
    using (BlueskyAgent agent = 
        new (httpClientHandler : httpClientHandler))
    {
        // Send a sample request through the configured agent.
        HttpResult<Did>did = await agent.ResolveHandle("blowdart.me");
    }
};
```

### Overriding the service used for API calls

If you are using a private or test instance of Bluesky you can tell the agent to use a specific service URI with the `service` constructor parameter. 

```c#
using (BlueskyAgent agent = 
  new (service: new Uri("https://sandbox.mybluesky.local")))
{
    Console.WriteLine($"Connecting to {agent.DefaultService}");
    // Send a sample request through the configured agent.
    HttpResult<Did>did = await agent.ResolveHandle("blowdart.me");
}
```

### Disabling token refresh

If you want to disable automatic token refresh you can do that by the `enableTokenRefresh` constructor parameter to false.

```c#
using (BlueskyAgent agent = new (enableTokenRefresh: false)
{   
    HttpResult<Did>did = await agent.ResolveHandle("blowdart.me");
}
```



## Creating and replying to posts

## Reading your timeline

## Checking your notifications

## Resolving DIDs



