# <a name="makingRequests">Making requests to Bluesky</a>

Making requests to Bluesky is done though the `BlueskyAgent` class. Once you authenticate using `agent.Login()` or via OAuth,
the agent manages your "session"", the tokens necessary to make authenticated requests are stored, refreshed automatically
and added to any authenticated API requests.

## <a name="understandingResults">Understanding responses from Bluesky</a>

Almost every API call through an agent returns an `AtProtoHttpResult<T>`. This approach, which you may recognize from ASP.NET Core,
avoids the use of exceptions should the HTTP call fail, and allows you to view any extra error information the Bluesky APIs may return.

`AtProtoHttpResult<T>` has properties to help you determine the success or failure of the call. These include

* The `Succeeded` property, a `boolean` indicated whether the API call was sucessful or not,
* The `StatusCode` property containing the HTTP status code from the API call,
* The `Result` property, containing the result of the API call. This may be null if a call was unsucessful,
* The `AtErrorDetail` property, containing any detailed error messages from the API if any were returned.

If a request is **successful** the `Succeeded` property on the returned result instance will be `true`, the `Result` property will not be null, and
the `StatusCode` property will be `HttpStatusCode.OK`.

If a request has **failed**, either at the HTTP or the API layer then the `Succeeded` property on the returned result will be `false`, and
the `Result` property will likely be `null`. The `StatusCode` property will contain the HTTP status code that returned by API call, and,
if the API call reached the API endpoint the `Error` property will probably contain any error message returned by the endpoint.

For example, a login call returns an `AtProtoHttpResult<bool>`. To check the login succeeded you would

1. Check the that the `Succeeded` property is true, which indicates the underlying request returned a `HttpStatusCode.OK` status code, and an available result.
2. If `Succeeded` is `true` you can continue on your way

   If `Succeeded` is `false` you use the `StatusCode` property to examine the HTTP status code returned by the API, then
   1. If the `StatusCode` property is `HttpStatusCode.OK` then the API call succeeded but no result was returned.
   2. If the `Error` property to view any extended error information returned by the API, which may have an `Error` and a `Message` set.

Let's add some basic error checking to the Hello World code you wrote in [getting started.](../index.md)

[!code-csharp[](./code/helloWorldErrorChecked.cs?highlight=9,13-16,19-25)]

* Line 9 you can see a call to `loginResult.Succeeded`, ensuring that any attempt to post only happens if the call to `Login` was successful,
* Lines 13-16 show a reaction if the call to `Post()` failed, and
* Lines 19-25 show how you can check for any API errors that might be returned.

You can download [helloWorldErrorChecked.cs](./code/helloWorldErrorChecked.cs).

> [!TIP]
> `AtProtoHttpResult<T>` has an `EnsureSucceeded()` method which will throw an `AtProtoHttpRequestException` if the `Succeeded` property is false.
> This can be used while testing code, or even during development before you implement error handling.
