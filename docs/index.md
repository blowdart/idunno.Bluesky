# <a name="gettingStarted">Hello World</a>

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

## <a name="makingRequests">Making requests to Bluesky</a>

All the supported Bluesky operations are contained in the `BlueskyAgent` class, which takes care of session management for you.

Most requests to Bluesky are made over [HTTP](https://docs.bsky.app/docs/category/http-reference) and the results are wrapped up in an `HttpResult` instance,
which contains the HTTP status code returned by the API, and any result or error returned.

## <a name="understandingResults">Understanding AtProtoHttpResult&lt;T&gt;</a>

Every API call through an agent returns an `AtProtoHttpResult<T>`. This approach, which you may recognize from ASP.NET,
avoids the use of exceptions should the HTTP call fail, and allows you to view any extra error information the Bluesky APIs may return.

If a request is successful the API return value will be in the `Result` property of the `AtProtoHttpResult<T>` and
the `Succeeded` property will be `true`. The `StatusCode` property will be `HttpStatusCode.OK`.

If a request has failed, either at the HTTP or the API layer then the `Succeeded` property will be `false`, and
the `Result` property will likely be `null`. You can check the `StatusCode` property to see what HTTP status code was
encountered during the API call, and, if the API call reached the API endpoint the `Error` property will contain any
error message returned by the endpoint.

For example, a login call returns an `AtProtoHttpResult<bool>`. To check the operation succeeded you would

1. Check the that the `Succeeded` property is true, which indicates the underlying request returned a `HttpStatusCode.OK` status code, and an available result.
2. If `Succeeded` is `true`, you can use the `Result` property and continue on your way.

   If `Succeeded` is `false` you use the `StatusCode` property to examine the HTTP status code returned by the API, then
   1. If the `StatusCode` property is `HttpStatusCode.OK` then the API call succeeded but no result was returned.
   2. If the `Error` property to view any extended error information returned by the API, which may have an `Error` and a `Message` set.

When calling an API you use the following pattern to check for errors.

```c#
// Make an API call to get the timeline for the current user.
var result = await agent.GetTimeline();

if (result.Succeeded)
{
    // Everything was successful, continue on with your code
}
else
{
    // React to the error.
    // Check the StatusCode property to check if there were any HTTP errors
    // And the Error property to check if the API returned any errors.
}
```

The `EnsureSucceeded()` method on `AtProtoHttpResult<T>` will throw an `AtProtoHttpRequestException` if the `Succeeded` property is false.

