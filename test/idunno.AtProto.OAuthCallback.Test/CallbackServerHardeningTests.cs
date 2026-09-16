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
