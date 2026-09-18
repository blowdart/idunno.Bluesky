// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Net;

using Microsoft.Extensions.Logging;

namespace idunno.AtProto.OAuthCallback.Test;

[Collection("CallbackServer")]
public class CallbackServerResponseTests
{
    [Theory]
    [InlineData(".")]
    [InlineData("..")]
    [InlineData("a/../b")]
    [InlineData("./callback")]
    [InlineData("callback/.")]
    public void ConstructorRejectsPathsWhichAUriWouldResolveAway(string path)
    {
        Assert.Throws<ArgumentException>(() => new CallbackServer(CallbackServer.GetRandomUnusedPort(), path));
    }

    [Fact]
    public async Task ACallbackCarryingAnAuthorizationCodeRendersTheSuccessPage()
    {
        CancellationToken testToken = TestContext.Current.CancellationToken;

        await using CallbackServer server = new(CallbackServer.GetRandomUnusedPort())
        {
            SuccessBody = "<p>login worked</p>",
            SuccessTitle = "<title>worked</title>",
            FailureBody = "<p>login did not work</p>",
            FailureTitle = "<title>did not work</title>"
        };

        Task<string> callback = server.WaitForCallbackAsync(timeoutInSeconds: 300, cancellationToken: testToken);

        using HttpClient client = new();
        using HttpResponseMessage response = await client.GetAsync(new Uri($"{server.Uri}?code=abc&state=xyz"), testToken);

        string body = await response.Content.ReadAsStringAsync(testToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("login worked", body, StringComparison.Ordinal);
        Assert.Contains("<title>worked</title>", body, StringComparison.Ordinal);
        Assert.DoesNotContain("login did not work", body, StringComparison.Ordinal);

        Assert.Equal("?code=abc&state=xyz", await callback);
    }

    [Theory]
    [InlineData("?error=access_denied&state=xyz")]
    [InlineData("?error=server_error")]
    [InlineData("?state=xyz")]
    public async Task ACallbackCarryingNoAuthorizationCodeRendersTheFailurePage(string query)
    {
        CancellationToken testToken = TestContext.Current.CancellationToken;

        await using CallbackServer server = new(CallbackServer.GetRandomUnusedPort())
        {
            SuccessBody = "<p>login worked</p>",
            SuccessTitle = "<title>worked</title>",
            FailureBody = "<p>login did not work</p>",
            FailureTitle = "<title>did not work</title>"
        };

        Task<string> callback = server.WaitForCallbackAsync(timeoutInSeconds: 300, cancellationToken: testToken);

        using HttpClient client = new();
        using HttpResponseMessage response = await client.GetAsync(new Uri($"{server.Uri}{query}"), testToken);

        string body = await response.Content.ReadAsStringAsync(testToken);

        // Telling the person at the browser the login completed when no authorization code was issued would be a lie,
        // whatever the waiting caller goes on to do with the query string.
        Assert.Contains("login did not work", body, StringComparison.Ordinal);
        Assert.Contains("<title>did not work</title>", body, StringComparison.Ordinal);
        Assert.DoesNotContain("login worked", body, StringComparison.Ordinal);

        Assert.Equal(query, await callback);
    }

    [Fact]
    public async Task ResponsesCarryAContentSecurityPolicyByDefault()
    {
        CancellationToken testToken = TestContext.Current.CancellationToken;

        await using CallbackServer server = new(CallbackServer.GetRandomUnusedPort());

        _ = server.WaitForCallbackAsync(timeoutInSeconds: 300, cancellationToken: testToken);

        using HttpClient client = new();
        using HttpResponseMessage response = await client.GetAsync(new Uri($"{server.Uri}?code=abc"), testToken);

        string policy = Assert.Single(response.Headers.GetValues("Content-Security-Policy"));

        Assert.Contains("default-src 'none'", policy, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AConfiguredContentSecurityPolicyIsUsed()
    {
        CancellationToken testToken = TestContext.Current.CancellationToken;

        await using CallbackServer server = new(CallbackServer.GetRandomUnusedPort())
        {
            ContentSecurityPolicy = "default-src 'self'"
        };

        _ = server.WaitForCallbackAsync(timeoutInSeconds: 300, cancellationToken: testToken);

        using HttpClient client = new();
        using HttpResponseMessage response = await client.GetAsync(new Uri($"{server.Uri}?code=abc"), testToken);

        Assert.Equal("default-src 'self'", Assert.Single(response.Headers.GetValues("Content-Security-Policy")));
    }

    [Fact]
    public async Task NoContentSecurityPolicyIsSentWhenItIsCleared()
    {
        CancellationToken testToken = TestContext.Current.CancellationToken;

        await using CallbackServer server = new(CallbackServer.GetRandomUnusedPort())
        {
            ContentSecurityPolicy = null
        };

        _ = server.WaitForCallbackAsync(timeoutInSeconds: 300, cancellationToken: testToken);

        using HttpClient client = new();
        using HttpResponseMessage response = await client.GetAsync(new Uri($"{server.Uri}?code=abc"), testToken);

        Assert.False(response.Headers.Contains("Content-Security-Policy"));
    }

    [Fact]
    public async Task ARepeatedWaitDoesNotLogATimeoutItWillNotUse()
    {
        CancellationToken testToken = TestContext.Current.CancellationToken;

        RecordingLoggerProvider provider = new();
        using ILoggerFactory loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Debug);
            builder.AddProvider(provider);
        });

        await using CallbackServer server = new(CallbackServer.GetRandomUnusedPort(), loggerFactory: loggerFactory);

        _ = server.WaitForCallbackAsync(timeoutInSeconds: 300, cancellationToken: testToken);
        _ = server.WaitForCallbackAsync(timeoutInSeconds: 17, cancellationToken: testToken);

        // The second call's timeout is ignored, so logging it would describe a wait which is not happening.
        Assert.DoesNotContain(provider.Messages, message => message.Contains("17 seconds", StringComparison.Ordinal));
        Assert.Contains(provider.Messages, message => message.Contains("300 seconds", StringComparison.Ordinal));
    }

    private sealed class RecordingLoggerProvider : ILoggerProvider
    {
        private readonly List<string> _messages = [];
        private readonly object _lock = new();

        internal IReadOnlyList<string> Messages
        {
            get
            {
                lock (_lock)
                {
                    return [.. _messages];
                }
            }
        }

        public ILogger CreateLogger(string categoryName) => new RecordingLogger(this);

        public void Dispose()
        {
        }

        private void Record(string message)
        {
            lock (_lock)
            {
                _messages.Add(message);
            }
        }

        private sealed class RecordingLogger(RecordingLoggerProvider provider) : ILogger
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
                ArgumentNullException.ThrowIfNull(formatter);

                provider.Record(formatter(state, exception));
            }
        }
    }
}
