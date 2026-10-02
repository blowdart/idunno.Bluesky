// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

namespace idunno.AtProto.OAuthCallback.Test;

[Collection("CallbackServer")]
public class CallbackServerTests
{
    private static readonly TimeSpan s_completionBudget = TimeSpan.FromSeconds(15);

    [Fact]
    public async Task WaitForCallbackAsyncCompletesWhenTheCallersTokenIsCancelled()
    {
        CancellationToken testToken = TestContext.Current.CancellationToken;

        await using CallbackServer server = new(CallbackServer.GetRandomUnusedPort());

        using CancellationTokenSource cts = new();

        Task<string> waiting = server.WaitForCallbackAsync(timeoutInSeconds: 300, cancellationToken: cts.Token);

        await cts.CancelAsync();

        OperationCanceledException exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await waiting.WaitAsync(s_completionBudget, testToken));

        Assert.Equal(cts.Token, exception.CancellationToken);
    }

    [Fact]
    public async Task WaitForCallbackAsyncCompletesWhenTheCallersTokenIsAlreadyCancelled()
    {
        CancellationToken testToken = TestContext.Current.CancellationToken;

        await using CallbackServer server = new(CallbackServer.GetRandomUnusedPort());

        using CancellationTokenSource cts = new();
        await cts.CancelAsync();

        Task<string> waiting = server.WaitForCallbackAsync(timeoutInSeconds: 300, cancellationToken: cts.Token);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await waiting.WaitAsync(s_completionBudget, testToken));
    }

    [Fact]
    public async Task WaitForCallbackAsyncCompletesWhenTheTimeoutExpires()
    {
        CancellationToken testToken = TestContext.Current.CancellationToken;

        await using CallbackServer server = new(CallbackServer.GetRandomUnusedPort());

        Task<string> waiting = server.WaitForCallbackAsync(timeoutInSeconds: 1, cancellationToken: testToken);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await waiting.WaitAsync(s_completionBudget, testToken));
    }

    [Fact]
    public async Task DisposingWhilstWaitingCompletesThePendingCallback()
    {
        CancellationToken testToken = TestContext.Current.CancellationToken;

        CallbackServer server = new(CallbackServer.GetRandomUnusedPort());

        Task<string> waiting = server.WaitForCallbackAsync(timeoutInSeconds: 300, cancellationToken: testToken);

        await server.DisposeAsync();

        await Assert.ThrowsAsync<ObjectDisposedException>(
            async () => await waiting.WaitAsync(s_completionBudget, testToken));
    }

    [Fact]
    public async Task WaitForCallbackAsyncThrowsOnceTheServerHasBeenDisposed()
    {
        CancellationToken testToken = TestContext.Current.CancellationToken;

        CallbackServer server = new(CallbackServer.GetRandomUnusedPort());
        await server.DisposeAsync();

        Assert.Throws<ObjectDisposedException>(() =>
        {
            _ = server.WaitForCallbackAsync(cancellationToken: testToken);
        });
    }

    [Fact]
    public async Task DisposingTwiceDoesNotThrow()
    {
        CallbackServer server = new(CallbackServer.GetRandomUnusedPort());

        await server.DisposeAsync();
        await server.DisposeAsync();
    }

    [Fact]
    public async Task RepeatedWaitForCallbackAsyncCallsReturnTheSameTask()
    {
        CancellationToken testToken = TestContext.Current.CancellationToken;

        await using CallbackServer server = new(CallbackServer.GetRandomUnusedPort());

        Task<string> first = server.WaitForCallbackAsync(timeoutInSeconds: 300, cancellationToken: testToken);
        Task<string> second = server.WaitForCallbackAsync(timeoutInSeconds: 300, cancellationToken: testToken);

        Assert.Same(first, second);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(65536)]
    public void ConstructorRejectsPortsOutsideTheValidRange(int port)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new CallbackServer(port));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MaxValue)]
    public async Task WaitForCallbackAsyncRejectsTimeoutsOutsideTheValidRange(int timeoutInSeconds)
    {
        CancellationToken testToken = TestContext.Current.CancellationToken;

        await using CallbackServer server = new(CallbackServer.GetRandomUnusedPort());

        Assert.Throws<ArgumentOutOfRangeException>(() =>
        {
            _ = server.WaitForCallbackAsync(timeoutInSeconds, cancellationToken: testToken);
        });
    }

    [Fact]
    public async Task ACallbackReturnsTheQueryStringOnlyAfterTheResponseHasBeenWritten()
    {
        CancellationToken testToken = TestContext.Current.CancellationToken;

        await using CallbackServer server = new(CallbackServer.GetRandomUnusedPort());

        Task<string> waiting = server.WaitForCallbackAsync(timeoutInSeconds: 300, cancellationToken: testToken);

        using HttpClient client = new();
        using HttpResponseMessage response = await client.GetAsync(new Uri($"{server.Uri}?code=abc&state=xyz"), testToken);

        string body = await response.Content.ReadAsStringAsync(testToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("</html>", body, StringComparison.Ordinal);

        string queryString = await waiting.WaitAsync(s_completionBudget, testToken);

        Assert.Equal("?code=abc&state=xyz", queryString);
    }

    [Fact]
    public async Task ABlockingContinuationOnTheCallbackDoesNotStallTheHttpResponse()
    {
        CancellationToken testToken = TestContext.Current.CancellationToken;

        await using CallbackServer server = new(CallbackServer.GetRandomUnusedPort());

        Task<string> waiting = server.WaitForCallbackAsync(timeoutInSeconds: 300, cancellationToken: testToken);

        using ManualResetEventSlim continuationEntered = new();
        using ManualResetEventSlim releaseContinuation = new();

        // If the completion source ran continuations inline, or published the result before the page was
        // written, this continuation would occupy the Kestrel request thread and hold up the response.
        Task continuation = waiting.ContinueWith(
            _ =>
            {
                continuationEntered.Set();
                releaseContinuation.Wait(TimeSpan.FromSeconds(30));
            },
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);

        try
        {
            using HttpClient client = new();

            long startedAt = Stopwatch.GetTimestamp();
            using HttpResponseMessage response = await client.GetAsync(new Uri($"{server.Uri}?code=abc"), testToken);
            _ = await response.Content.ReadAsStringAsync(testToken);
            TimeSpan elapsed = Stopwatch.GetElapsedTime(startedAt);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.True(
                elapsed < TimeSpan.FromSeconds(10),
                $"The response took {elapsed.TotalSeconds:F1}s, which means the blocking continuation stalled it.");
        }
        finally
        {
            releaseContinuation.Set();
            await continuation;
        }

        Assert.True(continuationEntered.IsSet);
    }

    [Fact]
    public async Task TheServerListensOnLoopbackOnly()
    {
        CancellationToken testToken = TestContext.Current.CancellationToken;

        int port = CallbackServer.GetRandomUnusedPort();
        await using CallbackServer server = new(port);

        Assert.Equal(IPAddress.Loopback.ToString(), server.Uri.Host);

        IPAddress? externalAddress = FindNonLoopbackAddress();

        Assert.SkipWhen(externalAddress is null, "The machine has no non loopback IPv4 address to probe.");

        // If the server had bound a wildcard address this connection would succeed.
        Assert.False(await CanConnectAsync(externalAddress!, port, testToken));
    }

    [Fact]
    public async Task AmbientKestrelConfigurationCannotMoveTheServerOffLoopback()
    {
        CancellationToken testToken = TestContext.Current.CancellationToken;

        int wildcardPort = CallbackServer.GetRandomUnusedPort();

        // WebApplication.CreateBuilder() reads ASPNETCORE_ prefixed environment variables, and a Kestrel
        // endpoint found there replaces anything configured in code. The callback server must ignore it.
        Environment.SetEnvironmentVariable("ASPNETCORE_Kestrel__Endpoints__Ambient__Url", $"http://0.0.0.0:{wildcardPort}");

        try
        {
            await using CallbackServer server = new(CallbackServer.GetRandomUnusedPort());

            Task<string> waiting = server.WaitForCallbackAsync(timeoutInSeconds: 300, cancellationToken: testToken);

            using HttpClient client = new();
            using HttpResponseMessage response = await client.GetAsync(new Uri($"{server.Uri}?code=abc"), testToken);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("?code=abc", await waiting.WaitAsync(s_completionBudget, testToken));

            Assert.False(await CanConnectAsync(IPAddress.Loopback, wildcardPort, testToken));
        }
        finally
        {
            Environment.SetEnvironmentVariable("ASPNETCORE_Kestrel__Endpoints__Ambient__Url", null);
        }
    }

    [Fact]
    public async Task RequestsWithAForgedHostHeaderAreRejected()
    {
        CancellationToken testToken = TestContext.Current.CancellationToken;

        await using CallbackServer server = new(CallbackServer.GetRandomUnusedPort());

        using HttpClient client = new();
        using HttpRequestMessage request = new(HttpMethod.Get, new Uri($"{server.Uri}?code=abc"));
        request.Headers.Host = "evil.example";

        using HttpResponseMessage response = await client.SendAsync(request, testToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostRequestsAreNotAllowed()
    {
        CancellationToken testToken = TestContext.Current.CancellationToken;

        await using CallbackServer server = new(CallbackServer.GetRandomUnusedPort());

        using StringContent content = new(string.Empty);
        using HttpClient client = new();
        using HttpResponseMessage response = await client.PostAsync(server.Uri, content, testToken);

        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
    }

    private static IPAddress? FindNonLoopbackAddress()
    {
        return Array.Find(
            Dns.GetHostAddresses(Dns.GetHostName()),
            address => address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(address));
    }

    private static async Task<bool> CanConnectAsync(IPAddress address, int port, CancellationToken cancellationToken)
    {
        using TcpClient client = new();

        try
        {
            using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(2));

            await client.ConnectAsync(address, port, timeout.Token);

            return client.Connected;
        }
        catch (Exception exception) when (exception is SocketException or OperationCanceledException)
        {
            return false;
        }
    }
}
