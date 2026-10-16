// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Net;
using System.Net.Sockets;

namespace idunno.AtProto.OAuthCallback.Test;

[Collection("CallbackServer")]
public class CallbackServerTests
{
    private static readonly TimeSpan s_completionBudget = TimeSpan.FromSeconds(15);

    internal sealed class CallbackClock : TimeProvider
    {
        private CallbackTimer? _timer;
        private TimeSpan _elapsed;

        internal bool TimerDisposed => _timer?.Disposed is true;
        internal int TimerCreationCount { get; private set; }

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            Assert.Null(_timer);
            Assert.Equal(Timeout.InfiniteTimeSpan, period);
            TimerCreationCount++;
            _timer = new CallbackTimer(this, callback, state);
            _timer.Change(dueTime, period);

            return _timer;
        }

        internal void Advance(TimeSpan elapsed)
        {
            _elapsed += elapsed;
            _timer?.Fire();
        }

        private sealed class CallbackTimer(CallbackClock clock, TimerCallback callback, object? state) : ITimer
        {
            private TimeSpan? _dueAt;

            internal bool Disposed { get; private set; }

            public bool Change(TimeSpan dueTime, TimeSpan period)
            {
                Assert.Equal(Timeout.InfiniteTimeSpan, period);
                ObjectDisposedException.ThrowIf(Disposed, this);
                _dueAt = dueTime == Timeout.InfiniteTimeSpan ? null : clock._elapsed + dueTime;

                return true;
            }

            internal void Fire()
            {
                if (!Disposed && _dueAt is TimeSpan dueAt && clock._elapsed >= dueAt)
                {
                    _dueAt = null;
                    callback(state);
                }
            }

            public void Dispose() => Disposed = true;

            public ValueTask DisposeAsync()
            {
                Dispose();

                return ValueTask.CompletedTask;
            }
        }
    }

    [Fact]
    public async Task WaitForCallbackAsyncCompletesWhenTheCallersTokenIsCancelled()
    {
        CancellationToken testToken = TestContext.Current.CancellationToken;

        await using CallbackServer server = await CallbackServerFactory.CreateAsync();

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

        await using CallbackServer server = await CallbackServerFactory.CreateAsync();

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

        CallbackClock clock = new();
        await using CallbackServer server = await CallbackServerFactory.CreateAsync(configure: server => server.TimeProvider = clock);

        Task<string> waiting = server.WaitForCallbackAsync(timeoutInSeconds: 1, cancellationToken: testToken);

        Assert.False(waiting.IsCompleted);
        clock.Advance(TimeSpan.FromSeconds(1));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await waiting.WaitAsync(s_completionBudget, testToken));
        Assert.True(clock.TimerDisposed);
    }

    [Fact]
    public async Task TheInternalClockRejectsNull()
    {
        await using CallbackServer server = await CallbackServerFactory.CreateAsync();

        Assert.Throws<ArgumentNullException>(() => server.TimeProvider = null!);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CallerCancellationReleasesTheCallbackTimeoutTimer(bool alreadyCancelled)
    {
        CallbackClock clock = new();
        await using CallbackServer server = await CallbackServerFactory.CreateAsync(configure: server => server.TimeProvider = clock);
        using CancellationTokenSource cancellation = new();

        if (alreadyCancelled)
        {
            await cancellation.CancelAsync();
        }

        Task<string> waiting = server.WaitForCallbackAsync(cancellationToken: cancellation.Token);
        await cancellation.CancelAsync();

        OperationCanceledException exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await waiting.WaitAsync(s_completionBudget, TestContext.Current.CancellationToken));

        Assert.Equal(cancellation.Token, exception.CancellationToken);
        Assert.True(clock.TimerDisposed);
    }

    [Fact]
    public async Task DisposingWhilstWaitingCompletesThePendingCallback()
    {
        CancellationToken testToken = TestContext.Current.CancellationToken;

        CallbackServer server = await CallbackServerFactory.CreateAsync();

        Task<string> waiting = server.WaitForCallbackAsync(timeoutInSeconds: 300, cancellationToken: testToken);

        await server.DisposeAsync();

        await Assert.ThrowsAsync<ObjectDisposedException>(
            async () => await waiting.WaitAsync(s_completionBudget, testToken));
    }

    [Fact]
    public async Task WaitForCallbackAsyncThrowsOnceTheServerHasBeenDisposed()
    {
        CancellationToken testToken = TestContext.Current.CancellationToken;

        CallbackServer server = await CallbackServerFactory.CreateAsync();
        await server.DisposeAsync();

        Assert.Throws<ObjectDisposedException>(() =>
        {
            _ = server.WaitForCallbackAsync(cancellationToken: testToken);
        });
    }

    [Fact]
    public async Task DisposingTwiceDoesNotThrow()
    {
        CallbackServer server = await CallbackServerFactory.CreateAsync();

        await server.DisposeAsync();
        await server.DisposeAsync();
    }

    [Fact]
    public async Task RepeatedWaitForCallbackAsyncCallsReturnTheSameTask()
    {
        CancellationToken testToken = TestContext.Current.CancellationToken;

        await using CallbackServer server = await CallbackServerFactory.CreateAsync();

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

        await using CallbackServer server = await CallbackServerFactory.CreateAsync();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
        {
            _ = server.WaitForCallbackAsync(timeoutInSeconds, cancellationToken: testToken);
        });
    }

    [Fact]
    public async Task ACallbackReturnsTheQueryStringOnlyAfterTheResponseHasBeenWritten()
    {
        CancellationToken testToken = TestContext.Current.CancellationToken;

        await using CallbackServer server = await CallbackServerFactory.CreateAsync();

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

        await using CallbackServer server = await CallbackServerFactory.CreateAsync();

        Task<string> waiting = server.WaitForCallbackAsync(timeoutInSeconds: 300, cancellationToken: testToken);

        TaskCompletionSource continuationEntered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        using ManualResetEventSlim releaseContinuation = new();

        // If the completion source ran continuations inline, or published the result before the page was
        // written, this continuation would occupy the Kestrel request thread and hold up the response.
        Task continuation = waiting.ContinueWith(
            _ =>
            {
                continuationEntered.TrySetResult();
                releaseContinuation.Wait();
            },
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);

        try
        {
            using HttpClient client = new();

            Task<HttpResponseMessage> request = client.GetAsync(new Uri($"{server.Uri}?code=abc"), testToken);
            await continuationEntered.Task.WaitAsync(s_completionBudget, testToken);
            using HttpResponseMessage response = await request.WaitAsync(s_completionBudget, testToken);
            _ = await response.Content.ReadAsStringAsync(testToken);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.False(continuation.IsCompleted);
        }
        finally
        {
            releaseContinuation.Set();
            await continuation.WaitAsync(s_completionBudget, testToken);
        }
    }

    [Fact]
    public async Task TheServerListensOnLoopbackOnly()
    {
        CancellationToken testToken = TestContext.Current.CancellationToken;

        await using CallbackServer server = await CallbackServerFactory.CreateAsync();

        Assert.Equal(IPAddress.Loopback.ToString(), server.Uri.Host);

        IPAddress? externalAddress = FindNonLoopbackAddress();

        Assert.SkipWhen(externalAddress is null, "The machine has no non loopback IPv4 address to probe.");

        // If the server had bound a wildcard address this connection would succeed.
        Assert.False(await CanConnectAsync(externalAddress!, server.Uri.Port, testToken));
    }

    [Fact]
    public async Task AmbientKestrelConfigurationCannotMoveTheServerOffLoopback()
    {
        CancellationToken testToken = TestContext.Current.CancellationToken;

        using TcpListener holder = new(IPAddress.Loopback, 0);
        holder.Start();
        int wildcardPort = ((IPEndPoint)holder.LocalEndpoint).Port;

        // WebApplication.CreateBuilder() reads ASPNETCORE_ prefixed environment variables, and a Kestrel
        // endpoint found there replaces anything configured in code. The callback server must ignore it.
        Environment.SetEnvironmentVariable("ASPNETCORE_Kestrel__Endpoints__Ambient__Url", $"http://0.0.0.0:{wildcardPort}");

        try
        {
            await using CallbackServer server = await CallbackServerFactory.CreateAsync();

            Task<string> waiting = server.WaitForCallbackAsync(timeoutInSeconds: 300, cancellationToken: testToken);

            using HttpClient client = new();
            using HttpResponseMessage response = await client.GetAsync(new Uri($"{server.Uri}?code=abc"), testToken);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("?code=abc", await waiting.WaitAsync(s_completionBudget, testToken));

            Assert.NotEqual(wildcardPort, server.Uri.Port);
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

        await using CallbackServer server = await CallbackServerFactory.CreateAsync();

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

        await using CallbackServer server = await CallbackServerFactory.CreateAsync();

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
