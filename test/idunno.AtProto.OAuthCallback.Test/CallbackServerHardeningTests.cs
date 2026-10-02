// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Net;
using System.Net.Sockets;
using System.Reflection;

namespace idunno.AtProto.OAuthCallback.Test;

[Collection("CallbackServer")]
public class CallbackServerHardeningTests
{
    private static readonly TimeSpan s_completionBudget = TimeSpan.FromSeconds(15);

    [Fact]
    public async Task ACallbackResponseSuppressesTheRefererAndCaching()
    {
        CancellationToken testToken = TestContext.Current.CancellationToken;

        await using CallbackServer server = new(CallbackServer.GetRandomUnusedPort());

        _ = server.WaitForCallbackAsync(timeoutInSeconds: 300, cancellationToken: testToken);

        using HttpClient client = new();
        using HttpResponseMessage response = await client.GetAsync(new Uri($"{server.Uri}?code=abc&state=xyz"), testToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("no-referrer", Assert.Single(response.Headers.GetValues("Referrer-Policy")));
        Assert.True(response.Headers.CacheControl!.NoStore);
    }

    [Fact]
    public async Task ErrorResponsesAlsoSuppressTheRefererAndCaching()
    {
        CancellationToken testToken = TestContext.Current.CancellationToken;

        await using CallbackServer server = new(CallbackServer.GetRandomUnusedPort());

        using HttpClient client = new();

        using StringContent content = new(string.Empty);
        using HttpResponseMessage methodNotAllowed = await client.PostAsync(server.Uri, content, testToken);
        using HttpResponseMessage fallback = await client.GetAsync(new Uri($"{server.Uri}not-the-callback"), testToken);

        Assert.Equal(HttpStatusCode.MethodNotAllowed, methodNotAllowed.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, fallback.StatusCode);

        foreach (HttpResponseMessage response in new[] { methodNotAllowed, fallback })
        {
            Assert.Equal("no-referrer", Assert.Single(response.Headers.GetValues("Referrer-Policy")));
            Assert.True(response.Headers.CacheControl!.NoStore);
        }
    }

    [Fact]
    public async Task ResponsesDeclareTheCharacterSet()
    {
        CancellationToken testToken = TestContext.Current.CancellationToken;

        await using CallbackServer server = new(CallbackServer.GetRandomUnusedPort());

        _ = server.WaitForCallbackAsync(timeoutInSeconds: 300, cancellationToken: testToken);

        using HttpClient client = new();
        using HttpResponseMessage response = await client.GetAsync(new Uri($"{server.Uri}?code=abc"), testToken);

        Assert.Equal("text/html", response.Content.Headers.ContentType!.MediaType);
        Assert.Equal("utf-8", response.Content.Headers.ContentType.CharSet);
    }

    [Fact]
    public async Task ResponsesForbidContentTypeSniffing()
    {
        CancellationToken testToken = TestContext.Current.CancellationToken;

        await using CallbackServer server = new(CallbackServer.GetRandomUnusedPort());

        _ = server.WaitForCallbackAsync(timeoutInSeconds: 300, cancellationToken: testToken);

        using HttpClient client = new();
        using HttpResponseMessage response = await client.GetAsync(new Uri($"{server.Uri}?code=abc"), testToken);

        Assert.Equal("nosniff", Assert.Single(response.Headers.GetValues("X-Content-Type-Options")));
    }

    [Theory]
    [InlineData("image")]
    [InlineData("empty")]
    [InlineData("script")]
    [InlineData("iframe")]
    public async Task ARequestWhichIsNotANavigationDoesNotConsumeTheCallback(string fetchDestination)
    {
        CancellationToken testToken = TestContext.Current.CancellationToken;

        await using CallbackServer server = new(CallbackServer.GetRandomUnusedPort());

        Task<string> waiting = server.WaitForCallbackAsync(timeoutInSeconds: 300, cancellationToken: testToken);

        using HttpClient client = new();

        using HttpRequestMessage crossSite = new(HttpMethod.Get, new Uri($"{server.Uri}?code=stolen&state=stolen"));
        crossSite.Headers.Add("Sec-Fetch-Dest", fetchDestination);

        using HttpResponseMessage rejected = await client.SendAsync(crossSite, testToken);

        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
        Assert.False(waiting.IsCompleted);

        // The callback the authorization server actually makes still has to work afterwards.
        using HttpRequestMessage navigation = new(HttpMethod.Get, new Uri($"{server.Uri}?code=abc&state=xyz"));
        navigation.Headers.Add("Sec-Fetch-Dest", "document");

        using HttpResponseMessage accepted = await client.SendAsync(navigation, testToken);

        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        Assert.Equal("?code=abc&state=xyz", await waiting.WaitAsync(s_completionBudget, testToken));
    }

    [Theory]
    [InlineData("")]
    [InlineData("?")]
    [InlineData("?somethingElse=1")]
    public async Task ARequestWhichDoesNotLookLikeAnOAuthResponseDoesNotConsumeTheCallback(string query)
    {
        CancellationToken testToken = TestContext.Current.CancellationToken;

        await using CallbackServer server = new(CallbackServer.GetRandomUnusedPort());

        Task<string> waiting = server.WaitForCallbackAsync(timeoutInSeconds: 300, cancellationToken: testToken);

        using HttpClient client = new();
        using HttpResponseMessage response = await client.GetAsync(new Uri($"{server.Uri}{query}"), testToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.False(waiting.IsCompleted);

        using HttpResponseMessage accepted = await client.GetAsync(new Uri($"{server.Uri}?code=abc"), testToken);

        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        Assert.Equal("?code=abc", await waiting.WaitAsync(s_completionBudget, testToken));
    }

    [Theory]
    [InlineData("error")]
    [InlineData("state")]
    public async Task AnOAuthResponseWithoutACodeIsStillACallback(string parameterName)
    {
        CancellationToken testToken = TestContext.Current.CancellationToken;

        await using CallbackServer server = new(CallbackServer.GetRandomUnusedPort());

        Task<string> waiting = server.WaitForCallbackAsync(timeoutInSeconds: 300, cancellationToken: testToken);

        using HttpClient client = new();
        using HttpResponseMessage response = await client.GetAsync(new Uri($"{server.Uri}?{parameterName}=abc"), testToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal($"?{parameterName}=abc", await waiting.WaitAsync(s_completionBudget, testToken));
    }

    [Fact]
    public async Task MethodsOtherThanGetAreNotAllowed()
    {
        CancellationToken testToken = TestContext.Current.CancellationToken;

        await using CallbackServer server = new(CallbackServer.GetRandomUnusedPort());

        using HttpClient client = new();

        foreach (HttpMethod method in new[] { HttpMethod.Post, HttpMethod.Put, HttpMethod.Delete, HttpMethod.Patch, HttpMethod.Head })
        {
            using HttpRequestMessage request = new(method, new Uri($"{server.Uri}?code=abc"));
            using HttpResponseMessage response = await client.SendAsync(request, testToken);

            Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
        }
    }

    [Theory]
    [InlineData("{id}")]
    [InlineData("callback/{*rest}")]
    [InlineData("call?back")]
    [InlineData("call#back")]
    [InlineData("call back")]
    [InlineData("call\\back")]
    public void ConstructorRejectsPathsWhichWouldNotMeanTheSameThingInARouteAndAUri(string path)
    {
        int port = CallbackServer.GetRandomUnusedPort();

        Assert.Throws<ArgumentException>("path", () => new CallbackServer(port, path));
    }

    [Theory]
    [InlineData("callback")]
    [InlineData("/callback")]
    [InlineData("oauth/callback")]
    [InlineData("call-back_1.0~x")]
    public async Task ConstructorAcceptsPathsMadeOfUnreservedCharacters(string path)
    {
        CancellationToken testToken = TestContext.Current.CancellationToken;

        await using CallbackServer server = new(CallbackServer.GetRandomUnusedPort(), path);

        Task<string> waiting = server.WaitForCallbackAsync(timeoutInSeconds: 300, cancellationToken: testToken);

        using HttpClient client = new();
        using HttpResponseMessage response = await client.GetAsync(new Uri($"{server.Uri}?code=abc"), testToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("?code=abc", await waiting.WaitAsync(s_completionBudget, testToken));
    }

    [Fact]
    public void GetRandomUnusedPortReturnsAPortWhichIsFreeOnBothLoopbackFamilies()
    {
        int port = CallbackServer.GetRandomUnusedPort();

        using TcpListener ipv4 = new(IPAddress.Loopback, port);
        ipv4.Start();
        ipv4.Stop();

        if (Socket.OSSupportsIPv6)
        {
            using TcpListener ipv6 = new(IPAddress.IPv6Loopback, port);
            ipv6.Start();
            ipv6.Stop();
        }
    }

    [Fact]
    public async Task APortHandedOutWhilstItIsHeldOnIPv6IsNotReturned()
    {
        Assert.SkipUnless(Socket.OSSupportsIPv6, "The machine does not support IPv6.");

        CancellationToken testToken = TestContext.Current.CancellationToken;

        // Holding the IPv6 half of a port makes it exactly the case the probe has to notice, because the IPv4 bind
        // GetRandomUnusedPort relies on still succeeds.
        int held = CallbackServer.GetRandomUnusedPort();

        using TcpListener holder = new(IPAddress.IPv6Loopback, held);
        holder.Start();

        try
        {
            Assert.False(IsFreeOnIPv6Loopback(held));
            Assert.True(IsFreeOnIPv6Loopback(CallbackServer.GetRandomUnusedPort()));

            // A server on a port the probe hands out has to actually start.
            await using CallbackServer server = new(CallbackServer.GetRandomUnusedPort());

            Task<string> waiting = server.WaitForCallbackAsync(timeoutInSeconds: 300, cancellationToken: testToken);

            using HttpClient client = new();
            using HttpResponseMessage response = await client.GetAsync(new Uri($"{server.Uri}?code=abc"), testToken);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("?code=abc", await waiting.WaitAsync(s_completionBudget, testToken));
        }
        finally
        {
            holder.Stop();
        }
    }

    /// <summary>
    /// Calls the private probe GetRandomUnusedPort uses to reject a port which is only free on IPv4. The port
    /// GetRandomUnusedPort draws cannot be steered from a test, so the probe is exercised directly rather than by
    /// hoping the operating system hands out one particular port out of the whole ephemeral range.
    /// </summary>
    private static bool IsFreeOnIPv6Loopback(int port)
    {
        MethodInfo probe = typeof(CallbackServer).GetMethod("IsFreeOnIPv6Loopback", BindingFlags.Static | BindingFlags.NonPublic)!;

        return (bool)probe.Invoke(null, [port])!;
    }

    [Fact]
    public async Task RepeatedWaitsDoNotRearmTheTimeoutWhenTheCallersTokenIsAlreadyCancelled()
    {
        await using CallbackServer server = new(CallbackServer.GetRandomUnusedPort());

        using CancellationTokenSource cts = new();
        await cts.CancelAsync();

        // Registering on an already cancelled token hands back a default CancellationTokenRegistration,
        // so a registration based "already armed" check does not trip and the second call replaces the
        // timeout source created by the first without disposing it.
        Task<string> first = server.WaitForCallbackAsync(timeoutInSeconds: 300, cancellationToken: cts.Token);
        CancellationTokenSource? armed = GetTimeoutSource(server);

        Task<string> second = server.WaitForCallbackAsync(timeoutInSeconds: 300, cancellationToken: cts.Token);

        Assert.NotNull(armed);
        Assert.Same(first, second);
        Assert.Same(armed, GetTimeoutSource(server));
    }

    [Fact]
    public async Task DisposingWithoutEverAwaitingDoesNotRaiseAnUnobservedTaskException()
    {
        List<Exception> unobserved = [];

        void OnUnobserved(object? sender, UnobservedTaskExceptionEventArgs args)
        {
            foreach (Exception exception in args.Exception.InnerExceptions)
            {
                if (exception is ObjectDisposedException disposed && disposed.ObjectName == nameof(CallbackServer))
                {
                    lock (unobserved)
                    {
                        unobserved.Add(exception);
                    }
                }
            }

            args.SetObserved();
        }

        TaskScheduler.UnobservedTaskException += OnUnobserved;

        try
        {
            await CreateAndDisposeWithoutAwaitingAsync();

            for (int i = 0; i < 3; i++)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }

            lock (unobserved)
            {
                Assert.Empty(unobserved);
            }
        }
        finally
        {
            TaskScheduler.UnobservedTaskException -= OnUnobserved;
        }
    }

    [Fact]
    public async Task TheServerAnswersRequestsAddressedToLocalhost()
    {
        CancellationToken testToken = TestContext.Current.CancellationToken;

        int port = CallbackServer.GetRandomUnusedPort();
        await using CallbackServer server = new(port);

        Task<string> waiting = server.WaitForCallbackAsync(timeoutInSeconds: 300, cancellationToken: testToken);

        using HttpClient client = new();
        using HttpResponseMessage response = await client.GetAsync(new Uri($"http://localhost:{port}/?code=abc"), testToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("?code=abc", await waiting.WaitAsync(s_completionBudget, testToken));
    }

    [Fact]
    public async Task TheServerAnswersRequestsOnIPv6Loopback()
    {
        CancellationToken testToken = TestContext.Current.CancellationToken;

        Assert.SkipUnless(Socket.OSSupportsIPv6, "The machine does not support IPv6.");

        int port = CallbackServer.GetRandomUnusedPort();
        await using CallbackServer server = new(port);

        Task<string> waiting = server.WaitForCallbackAsync(timeoutInSeconds: 300, cancellationToken: testToken);

        using HttpClient client = new();
        using HttpResponseMessage response = await client.GetAsync(new Uri($"http://[{IPAddress.IPv6Loopback}]:{port}/?code=abc"), testToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("?code=abc", await waiting.WaitAsync(s_completionBudget, testToken));
    }

    private static async Task CreateAndDisposeWithoutAwaitingAsync()
    {
        for (int i = 0; i < 10; i++)
        {
            CallbackServer server = new(CallbackServer.GetRandomUnusedPort());
            await server.DisposeAsync();
        }
    }

    private static CancellationTokenSource? GetTimeoutSource(CallbackServer server)
    {
        FieldInfo field = typeof(CallbackServer).GetField("_timeoutCancellationSource", BindingFlags.Instance | BindingFlags.NonPublic)!;

        return (CancellationTokenSource?)field.GetValue(server);
    }
}
