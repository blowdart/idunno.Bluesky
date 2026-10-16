# Agent HTTP configuration

The constructor for the Bluesky agents takes an instance of `BlueskyAgentOptions` which allows for configuration of the agents. The `BlueskyAgentOptions` class
contains an `HttpClientOptions` property which allows you to specify options for the underlying
[HttpClient](https://learn.microsoft.com/en-us/dotnet/api/system.net.http.httpclient)s used to make requests and receive responses.

## <a name="configuringTimeouts">Configuring HTTP timeouts</a>

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

## <a name="settingUserAgent">Setting the user agent</a>

Each request the agent makes is stamped with a string indicating the identity of the software making the request. By default this value is set to
`idunno.AtProto/x.x.x`, where x.x.x is the version of the library being used. This is sent as the
[UserAgent HTTP header](https://datatracker.ietf.org/doc/html/rfc7231#section-5.5.3) in every request.
You should set the `HttpClientOptions` `HttpUserAgent` property to be a value indicating your own software's identity.

## <a name="usingAProxy">Using a proxy server</a>

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

For automatic and manual credential refresh, see [Saving and restoring sessions](savingAndRestoringAuthentication.md#disablingTokenRefresh).
