// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Net;
using System.Net.Sockets;

using Microsoft.Extensions.Logging;

namespace idunno.AtProto.OAuthCallback.Test;

[Collection("CallbackServer")]
public class CallbackServerFactoryTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AnAddressCollisionIsRetriedAndTheFailedServerIsDisposed(bool ipv6)
    {
        Assert.SkipWhen(ipv6 && !Socket.OSSupportsIPv6, "The machine does not support IPv6.");

        using TcpListener holder = new(ipv6 ? IPAddress.IPv6Loopback : IPAddress.Loopback, 0);
        holder.Start();
        int heldPort = ((IPEndPoint)holder.LocalEndpoint).Port;
        int attempts = 0;
        List<CallbackServer> servers = [];

        await using CallbackServer server = await CallbackServerFactory.CreateAsync(
            path: "oauth/callback",
            configure: candidate =>
            {
                candidate.SuccessBody = "<p>configured</p>";
                servers.Add(candidate);
            },
            getPort: () => ++attempts == 1 ? heldPort : CallbackServer.GetRandomUnusedPort());

        Assert.InRange(attempts, 2, CallbackServerFactory.MaximumAttempts);
        Assert.Equal("/oauth/callback", server.Uri.AbsolutePath);

        foreach (CallbackServer failed in servers.SkipLast(1))
        {
            Assert.True(failed.Startup.IsFaulted);
            Assert.Throws<ObjectDisposedException>(() =>
            {
                _ = failed.WaitForCallbackAsync(cancellationToken: TestContext.Current.CancellationToken);
            });
        }

        Assert.True(server.Startup.IsCompletedSuccessfully);
        Task<string> waiting = server.WaitForCallbackAsync(cancellationToken: TestContext.Current.CancellationToken);
        using HttpClient client = new();
        using HttpResponseMessage response = await client.GetAsync(
            new Uri($"{server.Uri}?code=abc"), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("configured", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), StringComparison.Ordinal);
        Assert.Equal("?code=abc", await waiting.WaitAsync(TimeSpan.FromSeconds(15), TestContext.Current.CancellationToken));

        // An IPv6 collision occurs after IPv4 has bound. Cleanup must release that partial bind.
        if (ipv6)
        {
            using TcpListener probe = new(IPAddress.Loopback, heldPort);
            probe.Start();
        }
    }

    [Fact]
    public async Task ExhaustedCollisionsReportThePortsAndFinalExceptionAndDisposeEveryServer()
    {
        using TcpListener holder = new(IPAddress.Loopback, 0);
        holder.Start();
        int heldPort = ((IPEndPoint)holder.LocalEndpoint).Port;
        List<CallbackServer> servers = [];

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CallbackServerFactory.CreateAsync(configure: servers.Add, getPort: () => heldPort));

        Assert.Equal(CallbackServerFactory.MaximumAttempts, servers.Count);
        Assert.Contains($"Attempted ports: {string.Join(", ", Enumerable.Repeat(heldPort, servers.Count))}", exception.Message, StringComparison.Ordinal);
        Assert.NotNull(exception.InnerException);
        Assert.Same(servers[^1].Startup.Exception!.InnerException, exception.InnerException);

        foreach (CallbackServer server in servers)
        {
            Assert.Throws<ObjectDisposedException>(() =>
            {
                _ = server.WaitForCallbackAsync(cancellationToken: TestContext.Current.CancellationToken);
            });
        }
    }

    [Fact]
    public async Task ANonCollisionStartupFailureIsPropagatedWithoutRetrying()
    {
        InvalidOperationException failure = new("Deliberate startup failure.");
        using ILoggerFactory loggerFactory = LoggerFactory.Create(builder => builder.AddProvider(new FailingStartupLoggerProvider(failure)));
        List<CallbackServer> servers = [];

        AggregateException exception = await Assert.ThrowsAsync<AggregateException>(() =>
            CallbackServerFactory.CreateAsync(loggerFactory: loggerFactory, configure: servers.Add));

        Assert.Same(failure, Assert.Single(exception.InnerExceptions));
        CallbackServer server = Assert.Single(servers);
        Assert.True(server.Startup.IsFaulted);
        Assert.Same(server.Startup.Exception!.InnerException, exception);
        Assert.Throws<ObjectDisposedException>(() =>
        {
            _ = server.WaitForCallbackAsync(cancellationToken: TestContext.Current.CancellationToken);
        });
    }

    [Fact]
    public async Task AConfigurationFailureDisposesTheServerWithoutRetrying()
    {
        InvalidOperationException failure = new("Deliberate configuration failure.");
        List<CallbackServer> servers = [];

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CallbackServerFactory.CreateAsync(configure: server =>
            {
                servers.Add(server);
                throw failure;
            }));

        Assert.Same(failure, exception);
        CallbackServer server = Assert.Single(servers);
        Assert.Throws<ObjectDisposedException>(() =>
        {
            _ = server.WaitForCallbackAsync(cancellationToken: TestContext.Current.CancellationToken);
        });
    }

    private sealed class FailingStartupLoggerProvider(Exception failure) : ILoggerProvider
    {
        public ILogger CreateLogger(string categoryName) => new FailingStartupLogger(categoryName, failure);

        public void Dispose()
        {
        }

        private sealed class FailingStartupLogger(string categoryName, Exception failure) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                if (categoryName == "Microsoft.Hosting.Lifetime")
                {
                    throw failure;
                }
            }
        }
    }
}
