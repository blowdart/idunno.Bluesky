// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Reflection;

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace idunno.AtProto.OAuthCallback.Test;

[Collection("CallbackServer")]
public class CallbackServerFactoryTests
{
    [Fact]
    public async Task AsyncFactoryReturnsAStartedServerOnBothLoopbackAddresses()
    {
        await using CallbackServer server = await CallbackServer.CreateAsync(
            path: "oauth/callback",
            configure: candidate => candidate.SuccessBody = "<p>configured</p>",
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(server.Startup.IsCompletedSuccessfully);
        Assert.Equal("/oauth/callback", server.Uri.AbsolutePath);

        Task<string> waiting = server.WaitForCallbackAsync(cancellationToken: TestContext.Current.CancellationToken);
        using HttpClient client = new();

        string[] hosts = Socket.OSSupportsIPv6
            ? [IPAddress.Loopback.ToString(), IPAddress.IPv6Loopback.ToString(), "localhost"]
            : [IPAddress.Loopback.ToString(), "localhost"];

        foreach (string host in hosts)
        {
            Uri callbackUri = new UriBuilder(server.Uri) { Host = host }.Uri;
            using HttpResponseMessage response = await client.GetAsync(
                new Uri($"{callbackUri}?code=abc"),
                TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("configured", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), StringComparison.Ordinal);
        }

        Assert.Equal("?code=abc", await waiting.WaitAsync(TimeSpan.FromSeconds(15), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task AsyncFactoryHonorsCancellationBeforeAllocatingSockets()
    {
        using CancellationTokenSource cancellationTokenSource = new();
        cancellationTokenSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            CallbackServer.CreateAsync(cancellationToken: cancellationTokenSource.Token));
    }

    [Fact]
    public async Task ConcurrentAsyncFactoryCallsReceiveDifferentReadyPorts()
    {
        ConcurrentBag<CallbackServer> servers = [];

        try
        {
            await Task.WhenAll(
                Enumerable.Range(0, 8)
                    .Select(_ => CallbackServer.CreateAsync(
                        configure: servers.Add,
                        cancellationToken: TestContext.Current.CancellationToken)));

            Assert.All(servers, server => Assert.True(server.Startup.IsCompletedSuccessfully));
            Assert.Equal(servers.Count, servers.Select(server => server.Uri.Port).Distinct().Count());
        }
        finally
        {
            foreach (CallbackServer server in servers)
            {
                await server.DisposeAsync();
            }
        }
    }

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
            getPort: () => ++attempts == 1 ? heldPort : 0);

        Assert.InRange(attempts, 2, CallbackServerFactory.MaximumAttempts);
        Assert.Equal("/oauth/callback", server.Uri.AbsolutePath);

        Assert.Single(servers);
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
    public async Task ExhaustedCollisionsReportThePortsWithoutCreatingServers()
    {
        using TcpListener holder = new(IPAddress.Loopback, 0);
        holder.Start();
        int heldPort = ((IPEndPoint)holder.LocalEndpoint).Port;
        List<CallbackServer> servers = [];

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CallbackServerFactory.CreateAsync(configure: servers.Add, getPort: () => heldPort));

        Assert.Empty(servers);
        Assert.NotNull(exception.InnerException);
        Assert.Contains($"Attempted ports: {string.Join(", ", Enumerable.Repeat(heldPort, CallbackServerFactory.MaximumAttempts))}", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ANonCollisionStartupFailureIsPropagatedWithoutRetrying(bool mixedAggregate)
    {
        InvalidOperationException unrelatedFailure = new("Deliberate startup failure.");
        Exception failure = mixedAggregate
            ? new AggregateException(new SocketException((int)SocketError.AddressAlreadyInUse), unrelatedFailure)
            : unrelatedFailure;
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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AddressCollisionExceptionsFromConfigurationAreNotRetried(bool aggregate)
    {
        SocketException collision = new((int)SocketError.AddressAlreadyInUse);
        Exception failure = aggregate ? new AggregateException(collision) : collision;
        List<CallbackServer> servers = [];

        Exception exception = await Assert.ThrowsAnyAsync<Exception>(() =>
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

        using TcpListener probe = new(IPAddress.Loopback, server.Uri.Port);
        probe.Start();
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void MixedAggregatesAreNotAddressCollisions(bool collisionFirst, bool wrapped)
    {
        SocketException collision = new((int)SocketError.AddressAlreadyInUse);
        InvalidOperationException failure = new("Unrelated failure.");
        AggregateException aggregate = collisionFirst
            ? new AggregateException(collision, failure)
            : new AggregateException(failure, collision);
        Exception exception = wrapped ? new IOException("Wrapped failure.", aggregate) : aggregate;

        Assert.False(CallbackServer.IsAddressInUse(exception));
        Assert.True(CallbackServer.IsAddressInUse(new AggregateException(collision, new IOException("Wrapped collision.", collision))));
        Assert.False(CallbackServer.IsAddressInUse(new AggregateException()));
    }

    [Fact]
    public async Task ALoggerFailureAfterHostConstructionDisposesTheHostAndSockets()
    {
        InvalidOperationException failure = new("Deliberate listening log failure.");
        CallbackServer? candidate = null;
        WebApplication? listener = null;
        using ILoggerFactory loggerFactory = LoggerFactory.Create(builder =>
            builder.SetMinimumLevel(LogLevel.Debug).AddProvider(new ListeningFailureLoggerProvider(() =>
            {
                Assert.NotNull(candidate);
                listener = Assert.IsType<WebApplication>(typeof(CallbackServer)
                    .GetField("_listener", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .GetValue(candidate));
                _ = listener.Services.GetRequiredService<IHostApplicationLifetime>();
                throw failure;
            })));

        AggregateException exception = await Assert.ThrowsAsync<AggregateException>(() =>
            CallbackServerFactory.CreateAsync(loggerFactory: loggerFactory, configure: server => candidate = server));

        Assert.Same(failure, Assert.Single(exception.InnerExceptions));
        Assert.NotNull(listener);
        Assert.Throws<ObjectDisposedException>(() => listener.Services.GetRequiredService<IHostApplicationLifetime>());
        Assert.NotNull(candidate);
        using TcpListener probe = new(IPAddress.Loopback, candidate.Uri.Port);
        probe.Start();
    }

    private sealed class ListeningFailureLoggerProvider(Action onListening) : ILoggerProvider
    {
        public ILogger CreateLogger(string categoryName) => new ListeningFailureLogger(categoryName, onListening);

        public void Dispose()
        {
        }

        private sealed class ListeningFailureLogger(string categoryName, Action onListening) : ILogger
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
                if (categoryName == typeof(CallbackServer).FullName &&
                    eventId.Id == 1)
                {
                    onListening();
                }
            }
        }
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
