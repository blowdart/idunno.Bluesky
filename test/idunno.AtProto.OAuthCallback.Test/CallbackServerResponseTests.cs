// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Net;

using Microsoft.Extensions.Logging;

namespace idunno.AtProto.OAuthCallback.Test;

[Collection("CallbackServer")]
public class CallbackServerResponseTests
{
    [Theory]
    [InlineData("access_denied", "The user declined consent.")]
    [InlineData("<script>alert(\"error\")</script>&", "<img src=x onerror='alert(1)'>&")]
    [InlineData("&lt;script&gt;", "A < B & B > C")]
    [InlineData("access_denied", null)]
    [InlineData(null, "Consent was declined.")]
    [InlineData("", "")]
    [InlineData(null, null)]
    public async Task FailurePagesAppendOnlyPresentHtmlEncodedOAuthErrorValues(string? error, string? description)
    {
        CancellationToken testToken = TestContext.Current.CancellationToken;
        await using CallbackServer server = await CallbackServerFactory.CreateAsync(
            configure: server => server.FailureBody = "<h1>Failure</h1>");
        string query = "?state=xyz";
        if (error is not null)
        {
            query += $"&error={Uri.EscapeDataString(error)}";
        }
        if (description is not null)
        {
            query += $"&error_description={Uri.EscapeDataString(description)}";
        }
        Task<string> callback = server.WaitForCallbackAsync(timeoutInSeconds: 300, cancellationToken: testToken);
        using HttpClient client = new();
        using HttpResponseMessage response = await client.GetAsync(new Uri($"{server.Uri}{query}"), testToken);
        string body = await response.Content.ReadAsStringAsync(testToken);

        string expectedError = string.IsNullOrEmpty(error) ? string.Empty : $"<h2>{WebUtility.HtmlEncode(error)}</h2>";
        string expectedDescription = string.IsNullOrEmpty(description) ? string.Empty : $"<p>{WebUtility.HtmlEncode(description)}</p>";
        string expectedDetails = string.IsNullOrEmpty(error) && string.IsNullOrEmpty(description)
            ? string.Empty
            : $"<div class=\"oauth-error-details\">{expectedError}{expectedDescription}</div>";
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains($"<h1>Failure</h1>{expectedDetails}</body>", body, StringComparison.Ordinal);
        Assert.DoesNotContain("<script>", body, StringComparison.Ordinal);
        Assert.DoesNotContain("<img src=x", body, StringComparison.Ordinal);
        Assert.Equal(query, await callback);
    }

    [Fact]
    public async Task SuccessPagesDoNotDisplayOAuthErrorParameters()
    {
        CancellationToken testToken = TestContext.Current.CancellationToken;
        await using CallbackServer server = await CallbackServerFactory.CreateAsync();
        Task<string> callback = server.WaitForCallbackAsync(timeoutInSeconds: 300, cancellationToken: testToken);
        const string query = "?code=abc&state=xyz&error=access_denied&error_description=DoNotDisplay";
        using HttpClient client = new();
        using HttpResponseMessage response = await client.GetAsync(new Uri($"{server.Uri}{query}"), testToken);
        string body = await response.Content.ReadAsStringAsync(testToken);

        Assert.Contains("Login completed.", body, StringComparison.Ordinal);
        Assert.DoesNotContain("access_denied", body, StringComparison.Ordinal);
        Assert.DoesNotContain("DoNotDisplay", body, StringComparison.Ordinal);
        Assert.Equal(query, await callback);
    }

    [Fact]
    public void DefaultSuccessAndFailureBodiesUseTheSameLogoBeforeTheHeadingText()
    {
        const string imageStart = "<img ";
        const string imageEnd = "/>";
        string successBody = Resources.SuccessBody;
        string failureBody = Resources.FailureBody;
        int start = successBody.IndexOf(imageStart, StringComparison.Ordinal);
        int end = successBody.IndexOf(imageEnd, start, StringComparison.Ordinal) + imageEnd.Length;
        string logo = successBody[start..end];

        Assert.StartsWith("<h1><img src=\"data:image/png;base64,", successBody, StringComparison.Ordinal);
        Assert.StartsWith("<h1><img src=\"data:image/png;base64,", failureBody, StringComparison.Ordinal);
        Assert.Contains($"{logo}Login completed.", successBody, StringComparison.Ordinal);
        Assert.Contains($"{logo}Login was not completed.", failureBody, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(".")]
    [InlineData("..")]
    [InlineData("a/../b")]
    [InlineData("./callback")]
    [InlineData("callback/.")]
    public void ConstructorRejectsPathsWhichAUriWouldResolveAway(string path)
    {
        Assert.Throws<ArgumentException>(() => new CallbackServer(12345, path));
    }

    [Fact]
    public async Task ACallbackCarryingAnAuthorizationCodeRendersTheSuccessPage()
    {
        CancellationToken testToken = TestContext.Current.CancellationToken;

        await using CallbackServer server = await CallbackServerFactory.CreateAsync(configure: server =>
        {
            server.SuccessBody = "<p>login worked</p>";
            server.SuccessTitle = "<title>worked</title>";
            server.FailureBody = "<p>login did not work</p>";
            server.FailureTitle = "<title>did not work</title>";
        });

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

        await using CallbackServer server = await CallbackServerFactory.CreateAsync(configure: server =>
        {
            server.SuccessBody = "<p>login worked</p>";
            server.SuccessTitle = "<title>worked</title>";
            server.FailureBody = "<p>login did not work</p>";
            server.FailureTitle = "<title>did not work</title>";
        });

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

        await using CallbackServer server = await CallbackServerFactory.CreateAsync();

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

        await using CallbackServer server = await CallbackServerFactory.CreateAsync(
            configure: server => server.ContentSecurityPolicy = "default-src 'self'");

        _ = server.WaitForCallbackAsync(timeoutInSeconds: 300, cancellationToken: testToken);

        using HttpClient client = new();
        using HttpResponseMessage response = await client.GetAsync(new Uri($"{server.Uri}?code=abc"), testToken);

        Assert.Equal("default-src 'self'", Assert.Single(response.Headers.GetValues("Content-Security-Policy")));
    }

    [Fact]
    public async Task NoContentSecurityPolicyIsSentWhenItIsCleared()
    {
        CancellationToken testToken = TestContext.Current.CancellationToken;

        await using CallbackServer server = await CallbackServerFactory.CreateAsync(
            configure: server => server.ContentSecurityPolicy = null);

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

        await using CallbackServer server = await CallbackServerFactory.CreateAsync(loggerFactory: loggerFactory);

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
