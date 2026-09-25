// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text;
using System.Text.Json;

using idunno.AtProto.Authentication;

using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.JsonWebTokens;

namespace idunno.AtProto.Test;

[ExcludeFromCodeCoverage]
public class OAuthAccessTokenValidationTests
{
    private static readonly Uri s_authority = new("https://authority.test/");

    private static OAuthClient CreateOAuthClient(TimeSpan? clockSkew = null, ILoggerFactory? loggerFactory = null)
    {
        OAuthOptions options = new("https://client.test/clientMetadata.json");

        if (clockSkew is not null)
        {
            options.ClockSkew = clockSkew.Value;
        }

        return new OAuthClient(
            httpClientConfigurator: httpClient => httpClient,
            innerHandlerFactory: () => new HttpClientHandler(),
            loggerFactory: loggerFactory,
            options: options);
    }

    private static string Base64UrlEncode(string value) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(value)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static JsonWebToken CreateToken(
        string? issuer = "https://authority.test/",
        string? audience = "did:plc:testing",
        string? subject = "did:plc:abcdefghijklmnopqrstuvwx",
        string? scope = "atproto",
        DateTimeOffset? notBefore = null,
        DateTimeOffset? expires = null,
        bool includeNotBefore = true,
        bool includeExpiry = true)
    {
        Dictionary<string, object> payload = [];

        if (issuer is not null)
        {
            payload["iss"] = issuer;
        }

        if (audience is not null)
        {
            payload["aud"] = audience;
        }

        if (subject is not null)
        {
            payload["sub"] = subject;
        }

        if (scope is not null)
        {
            payload["scope"] = scope;
        }

        if (includeNotBefore)
        {
            payload["nbf"] = (notBefore ?? DateTimeOffset.UtcNow.AddMinutes(-1)).ToUnixTimeSeconds();
        }

        if (includeExpiry)
        {
            payload["exp"] = (expires ?? DateTimeOffset.UtcNow.AddMinutes(30)).ToUnixTimeSeconds();
        }

        return new JsonWebToken($"{Base64UrlEncode("{\"alg\":\"none\"}")}.{Base64UrlEncode(JsonSerializer.Serialize(payload))}.");
    }

    [Fact]
    public void ValidTokenPassesValidation()
    {
        CreateOAuthClient().ValidateAccessToken(CreateToken(), s_authority, Guid.NewGuid());
    }

    [Theory]
    [InlineData("atproto")]
    [InlineData("atproto transition:generic")]
    [InlineData("transition:generic atproto")]
    public void ScopeContainingAtProtoAsADiscreteEntryPassesValidation(string scope)
    {
        CreateOAuthClient().ValidateAccessToken(CreateToken(scope: scope), s_authority, Guid.NewGuid());
    }

    [Theory]
    [InlineData("notatproto")]
    [InlineData("atproto-lite")]
    [InlineData("xatprotox")]
    [InlineData("transition:atprotogeneric")]
    [InlineData("ATPROTO")]
    [InlineData("transition:generic")]
    public void ScopeMerelyContainingAtProtoAsASubstringFailsValidation(string scope)
    {
        OAuthException exception = Assert.Throws<OAuthException>(
            () => CreateOAuthClient().ValidateAccessToken(CreateToken(scope: scope), s_authority, Guid.NewGuid()));

        Assert.Equal("Issued token does not contain atproto in scope.", exception.Message);
    }

    [Fact]
    public void TokenWithNoScopeClaimThrowsOAuthException()
    {
        OAuthException exception = Assert.Throws<OAuthException>(
            () => CreateOAuthClient().ValidateAccessToken(CreateToken(scope: null), s_authority, Guid.NewGuid()));

        Assert.Equal("Issued token does not contain atproto in scope.", exception.Message);
    }

    [Fact]
    public void TokenWithNoNotBeforeClaimPassesValidation()
    {
        CreateOAuthClient().ValidateAccessToken(CreateToken(includeNotBefore: false), s_authority, Guid.NewGuid());
    }

    [Fact]
    public void AnAbsentLifetimeClaimConvertsToNullRatherThanBeingTreatedAsALocalTime()
    {
        // DateTime.MinValue with an unspecified kind cannot be converted to a DateTimeOffset in any time zone
        // east of UTC, so it must never reach the DateTimeOffset constructor. This assertion is independent of
        // the time zone of the machine running the test.
        Assert.Null(OAuthClient.ToUtcDateTimeOffset(DateTime.MinValue));
    }

    [Fact]
    public void APresentLifetimeClaimConvertsToTheSameInstantInUtc()
    {
        DateTime value = new(2026, 9, 15, 21, 43, 5, DateTimeKind.Utc);

        DateTimeOffset? converted = OAuthClient.ToUtcDateTimeOffset(value);

        Assert.NotNull(converted);
        Assert.Equal(TimeSpan.Zero, converted.Value.Offset);
        Assert.Equal(value, converted.Value.UtcDateTime);
    }

    [Fact]
    public void TokenWithNoExpiryClaimThrowsOAuthException()
    {
        OAuthException exception = Assert.Throws<OAuthException>(
            () => CreateOAuthClient().ValidateAccessToken(CreateToken(includeExpiry: false), s_authority, Guid.NewGuid()));

        Assert.Equal("Issued token does not contain exp.", exception.Message);
    }

    [Fact]
    public void TokenIssuedOutsideOfTheConfiguredClockSkewIsNotYetValid()
    {
        OAuthException exception = Assert.Throws<OAuthException>(
            () => CreateOAuthClient(TimeSpan.FromMinutes(1)).ValidateAccessToken(
                CreateToken(notBefore: DateTimeOffset.UtcNow.AddMinutes(5)),
                s_authority,
                Guid.NewGuid()));

        Assert.Equal("Issued token is not yet valid.", exception.Message);
    }

    [Fact]
    public void TokenIssuedWithinTheConfiguredClockSkewPassesValidation()
    {
        CreateOAuthClient(TimeSpan.FromMinutes(10)).ValidateAccessToken(
            CreateToken(notBefore: DateTimeOffset.UtcNow.AddMinutes(5)),
            s_authority,
            Guid.NewGuid());
    }

    [Fact]
    public void TokenExpiredOutsideOfTheConfiguredClockSkewThrowsOAuthException()
    {
        OAuthException exception = Assert.Throws<OAuthException>(
            () => CreateOAuthClient(TimeSpan.FromMinutes(1)).ValidateAccessToken(
                CreateToken(notBefore: DateTimeOffset.UtcNow.AddMinutes(-30), expires: DateTimeOffset.UtcNow.AddMinutes(-5)),
                s_authority,
                Guid.NewGuid()));

        Assert.Equal("Issued token has already expired.", exception.Message);
    }

    [Fact]
    public void TokenExpiredWithinTheConfiguredClockSkewPassesValidation()
    {
        CreateOAuthClient(TimeSpan.FromMinutes(10)).ValidateAccessToken(
            CreateToken(notBefore: DateTimeOffset.UtcNow.AddMinutes(-30), expires: DateTimeOffset.UtcNow.AddMinutes(-5)),
            s_authority,
            Guid.NewGuid());
    }

    [Fact]
    public void ZeroClockSkewRejectsATokenIssuedInTheFuture()
    {
        OAuthException exception = Assert.Throws<OAuthException>(
            () => CreateOAuthClient(TimeSpan.Zero).ValidateAccessToken(
                CreateToken(notBefore: DateTimeOffset.UtcNow.AddMinutes(5)),
                s_authority,
                Guid.NewGuid()));

        Assert.Equal("Issued token is not yet valid.", exception.Message);
    }

    [Fact]
    public void TokenWithNoAudienceThrowsOAuthException()
    {
        OAuthException exception = Assert.Throws<OAuthException>(
            () => CreateOAuthClient().ValidateAccessToken(CreateToken(audience: null), s_authority, Guid.NewGuid()));

        Assert.Equal("Issued token does not contain aud.", exception.Message);
    }

    [Fact]
    public void TokenWithNoIssuerThrowsOAuthException()
    {
        OAuthException exception = Assert.Throws<OAuthException>(
            () => CreateOAuthClient().ValidateAccessToken(CreateToken(issuer: null), s_authority, Guid.NewGuid()));

        Assert.Equal("Issued token does not contain a valid iss.", exception.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("not-a-did")]
    [InlineData("did:plc:")]
    [InlineData("https://example.test/")]
    public void TokenWithoutAUsableSubjectThrowsOAuthExceptionRatherThanEscapingAsAnArgumentException(string? subject)
    {
        OAuthException exception = Assert.Throws<OAuthException>(
            () => CreateOAuthClient().ValidateAccessToken(CreateToken(subject: subject), s_authority, Guid.NewGuid()));

        Assert.Equal("Issued token does not contain a valid sub.", exception.Message);
    }

    [Fact]
    public void TokenFromAnUnexpectedIssuerThrowsOAuthException()
    {
        OAuthException exception = Assert.Throws<OAuthException>(
            () => CreateOAuthClient().ValidateAccessToken(
                CreateToken(issuer: "https://attacker.test/"),
                s_authority,
                Guid.NewGuid()));

        Assert.Equal("Unexpected access token issuer", exception.Message);
    }

    [Fact]
    public void ClockSkewDefaultsToFiveMinutes()
    {
        Assert.Equal(TimeSpan.FromMinutes(5), OAuthOptions.DefaultClockSkew);
        Assert.Equal(OAuthOptions.DefaultClockSkew, new OAuthOptions().ClockSkew);
    }

    [Fact]
    public void NegativeClockSkewIsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new OAuthOptions().ClockSkew = TimeSpan.FromSeconds(-1));
    }

    [Fact]
    public void MismatchedIssuerIsLoggedWithTheActualAndExpectedAuthoritiesTheCorrectWayAround()
    {
        CapturingLoggerFactory loggerFactory = new();

        Assert.Throws<OAuthException>(
            () => CreateOAuthClient(loggerFactory: loggerFactory).ValidateAccessToken(
                CreateToken(issuer: "https://attacker.test/"),
                s_authority,
                Guid.NewGuid()));

        string message = Assert.Single(loggerFactory.Messages, m => m.Contains("did not match the expected", StringComparison.Ordinal));

        Assert.Contains(
            "issuer https://attacker.test/ did not match the expected https://authority.test/",
            message,
            StringComparison.Ordinal);
    }

    private sealed class CapturingLoggerFactory : ILoggerFactory
    {
        public List<string> Messages { get; } = [];

        public void AddProvider(ILoggerProvider provider)
        {
        }

        public ILogger CreateLogger(string categoryName) => new CapturingLogger(Messages);

        public void Dispose()
        {
        }

        private sealed class CapturingLogger(List<string> messages) : ILogger
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

                messages.Add(formatter(state, exception));
            }
        }
    }
}
