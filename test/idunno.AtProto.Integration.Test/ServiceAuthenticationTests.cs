// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

using idunno.AtProto.Authentication;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Time.Testing;

using static idunno.AtProto.Integration.Test.FirehoseTestData;

namespace idunno.AtProto.Integration.Test;

[ExcludeFromCodeCoverage]
public class ServiceAuthenticationTests
{
    private static readonly AtProtoServiceReference s_audience = new(new Did(OtherDid), "atproto_labeler");
    private static readonly Nsid s_method = new("com.atproto.label.applyLabels");

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReportingCompatibilityRetainsSignedAudienceWithoutWeakeningExactVerification(bool legacy)
    {
        FakeTimeProvider clock = CreateClock();
        using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        JsonObject payload = Payload(clock);
        string audience = legacy ? s_audience.Did.Value : s_audience.ToString();
        payload["aud"] = audience;
        payload["lxm"] = "com.atproto.moderation.createReport";
        string token = Sign(key, """{"typ":"JWT","alg":"ES256"}""", payload.ToJsonString());
        using TestServer server = Resolver(DidDocumentFor(TestDid, "atproto", key));
        using HttpClient client = server.CreateClient();
        ModerationReportAuthenticationResult result = await ServiceAuthentication.VerifyModerationReportAsync(
            token: token,
            labeler: s_audience.Did,
            httpClient: client,
            timeProvider: clock,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(new Did(TestDid), result.Reporter);
        Assert.Equal(s_audience.Did, result.Labeler);
        Assert.Equal(audience, result.Audience);
        Assert.Equal("unique-token", result.TokenId);
        Assert.Equal(clock.GetUtcNow().AddSeconds(60), result.ExpiresAt);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(Encoding.ASCII.GetBytes(token))), result.TokenHash);
        if (legacy)
        {
            await Assert.ThrowsAsync<InvalidDataException>(() => ServiceAuthentication.VerifyAsync(
                token: token,
                audience: s_audience,
                method: new("com.atproto.moderation.createReport"),
                httpClient: client,
                timeProvider: clock,
                cancellationToken: TestContext.Current.CancellationToken));
        }
    }

    [Theory]
    [InlineData("aud", "\"did:plc:other\"")]
    [InlineData("aud", "\"did:plc:g6ylltenitt4tp27bpwalh7b#labeler_admin\"")]
    [InlineData("aud", "[\"did:plc:g6ylltenitt4tp27bpwalh7b\"]")]
    [InlineData("lxm", "\"dev.idunno.labeler.admin.issueLabel\"")]
    [InlineData("lxm", "null")]
    [InlineData("jti", "null")]
    [InlineData("exp", "0")]
    public async Task ReportingCompatibilityRejectsUnrelatedAudienceMethodAndInvalidClaims(string claim, string value)
    {
        FakeTimeProvider clock = CreateClock();
        using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        JsonObject payload = Payload(clock);
        payload["aud"] = s_audience.Did.Value;
        payload["lxm"] = "com.atproto.moderation.createReport";
        payload[claim] = JsonNode.Parse(value);
        string token = Sign(key, """{"typ":"JWT","alg":"ES256"}""", payload.ToJsonString());
        using TestServer server = Resolver(DidDocumentFor(TestDid, "atproto", key));
        using HttpClient client = server.CreateClient();
        await Assert.ThrowsAsync<InvalidDataException>(() => ServiceAuthentication.VerifyModerationReportAsync(
            token: token,
            labeler: s_audience.Did,
            httpClient: client,
            timeProvider: clock,
            cancellationToken: TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task ReportingChecksSignatureAndExpiryAfterResolution(bool legacy, bool expiresDuringResolution)
    {
        FakeTimeProvider clock = CreateClock();
        using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using ECDsa otherKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        JsonObject payload = Payload(clock);
        payload["aud"] = legacy ? s_audience.Did.Value : s_audience.ToString();
        payload["lxm"] = "com.atproto.moderation.createReport";
        string token = Sign(key, """{"typ":"JWT","alg":"ES256"}""", payload.ToJsonString());
        using TestServer server = Resolver(
            DidDocumentFor(TestDid, "atproto", expiresDuringResolution ? key : otherKey),
            expiresDuringResolution ? () => clock.Advance(TimeSpan.FromSeconds(66)) : null);
        using HttpClient client = server.CreateClient();
        await Assert.ThrowsAsync<InvalidDataException>(() => ServiceAuthentication.VerifyModerationReportAsync(
            token: token,
            labeler: s_audience.Did,
            httpClient: client,
            timeProvider: clock,
            cancellationToken: TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(-5)]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(5)]
    public async Task DefaultAcquisitionLeavesHeadroomForIndependentPdsClock(int clientOffsetSeconds)
    {
        FakeTimeProvider pdsClock = CreateClock();
        FakeTimeProvider clientClock = new(pdsClock.GetUtcNow().AddSeconds(clientOffsetSeconds));
        using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using TestServer pds = TestServerBuilder.CreateServer(TestServerBuilder.DefaultDomainName, async context =>
        {
            Assert.Equal("/xrpc/com.atproto.server.getServiceAuth", context.Request.Path);
            long expiry = long.Parse(context.Request.Query["exp"].ToString(), System.Globalization.CultureInfo.InvariantCulture);
            Assert.Equal(clientClock.GetUtcNow().AddSeconds(895).ToUnixTimeSeconds(), expiry);
            JsonObject claims = Payload(pdsClock);
            claims["exp"] = expiry;
            string token = Sign(key, """{"typ":"JWT","alg":"ES256"}""", claims.ToJsonString());
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync($$"""{"token":"{{token}}"}""", TestContext.Current.CancellationToken);
        });
        using HttpClient pdsClient = pds.CreateClient();
        AccessCredentials credentials = new(
            TestServerBuilder.DefaultUri,
            AuthenticationType.UsernamePassword,
            JwtBuilder.CreateJwt(new Did(TestDid), timeProvider: clientClock),
            "refresh-token");
        AtProtoHttpResult<ServiceCredential> acquired = await AtProtoServer.GetServiceAuthWithTimeProvider(
            timeProvider: clientClock,
            audience: s_audience.ToString(),
            expiry: null,
            lxm: s_method,
            service: TestServerBuilder.DefaultUri,
            accessCredentials: credentials,
            httpClient: pdsClient,
            cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(acquired.Succeeded);
        using TestServer resolver = Resolver(DidDocumentFor(TestDid, "atproto", key));
        using HttpClient resolverClient = resolver.CreateClient();
        ServiceAuthenticationResult verified = await ServiceAuthentication.VerifyAsync(
            token: acquired.Result.AccessJwt,
            audience: s_audience,
            method: s_method,
            httpClient: resolverClient,
            timeProvider: pdsClock,
            cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(clientClock.GetUtcNow().AddSeconds(895), verified.ExpiresAt);
    }

    [Theory]
    [InlineData("1.2.840.10045.3.1.7", "ES256", false)]
    [InlineData("1.2.840.10045.3.1.7", "ES256", true)]
    [InlineData("1.3.132.0.10", "ES256K", false)]
    [InlineData("1.3.132.0.10", "ES256K", true)]
    public async Task VerifiesMatchingCurveAndReturnsAuthenticatedClaims(string curve, string algorithm, bool includeKeyId)
    {
        using ECDsa key = ECDsa.Create(ECCurve.CreateFromValue(curve));
        FakeTimeProvider time = CreateClock();
        JsonObject header = new() { ["typ"] = "JWT", ["alg"] = algorithm };
        if (includeKeyId)
        {
            header["kid"] = "#atproto";
        }

        string token = Sign(key, header.ToJsonString(), Payload(time).ToJsonString());
        using TestServer server = Resolver(DidDocumentFor(TestDid, "atproto", key));
        using HttpClient client = server.CreateClient();
        ServiceAuthenticationResult result = await ServiceAuthentication.VerifyAsync(
            token: token,
            audience: s_audience,
            method: s_method,
            httpClient: client,
            timeProvider: time,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(new Did(TestDid), result.Issuer);
        Assert.Same(s_audience, result.Audience);
        Assert.Equal("unique-token", result.TokenId);
        Assert.Equal(time.GetUtcNow().AddSeconds(60), result.ExpiresAt);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(Encoding.ASCII.GetBytes(token))), result.TokenHash);
    }

    [Theory]
    [InlineData("aud", "\"did:plc:other#atproto_labeler\"")]
    [InlineData("aud", "[\"did:plc:other#atproto_labeler\"]")]
    [InlineData("aud", "[\"did:plc:g6ylltenitt4tp27bpwalh7b#atproto_labeler\"]")]
    [InlineData("aud", "\"did:plc:g6ylltenitt4tp27bpwalh7b\"")]
    [InlineData("aud", "null")]
    [InlineData("lxm", "\"com.atproto.repo.getRecord\"")]
    [InlineData("lxm", "true")]
    [InlineData("iss", "\"did:plc:example#atproto\"")]
    [InlineData("iss", "\"did:plc:example/path\"")]
    [InlineData("iss", "\"not-a-did\"")]
    [InlineData("jti", "\"   \"")]
    [InlineData("jti", "42")]
    [InlineData("iat", "1.1")]
    [InlineData("iat", "1e9")]
    [InlineData("iat", "\"1767225600\"")]
    [InlineData("exp", "null")]
    [InlineData("exp", "1e999")]
    [InlineData("exp", "9223372036854775807")]
    [InlineData("nbf", "null")]
    [InlineData("nbf", "\"1767225600\"")]
    [InlineData("nbf", "true")]
    [InlineData("nbf", "[]")]
    [InlineData("nbf", "1.1")]
    [InlineData("nbf", "1e999")]
    [InlineData("nbf", "9223372036854775807")]
    public async Task RejectsInvalidScalarClaims(string claim, string value)
    {
        FakeTimeProvider time = CreateClock();
        JsonObject payload = Payload(time);
        payload[claim] = JsonNode.Parse(value);
        await AssertRejected(payload: payload.ToJsonString(), time: time);
    }

    [Theory]
    [InlineData("iss")]
    [InlineData("aud")]
    [InlineData("lxm")]
    [InlineData("jti")]
    [InlineData("iat")]
    [InlineData("exp")]
    public async Task RejectsMissingRequiredClaims(string claim)
    {
        FakeTimeProvider time = CreateClock();
        JsonObject payload = Payload(time);
        payload.Remove(claim);
        await AssertRejected(payload: payload.ToJsonString(), time: time);
    }

    [Theory]
    [InlineData("typ", "\"at+jwt\"")]
    [InlineData("typ", "\"jwt\"")]
    [InlineData("alg", "\"none\"")]
    [InlineData("alg", "\"HS256\"")]
    [InlineData("kid", "\"#atproto_label\"")]
    [InlineData("kid", "\"did:plc:example#atproto\"")]
    [InlineData("kid", "null")]
    [InlineData("jku", "\"https://attacker.test/keys\"")]
    [InlineData("x5u", "\"https://attacker.test/keys\"")]
    [InlineData("jwk", "{}")]
    [InlineData("crit", "[]")]
    [InlineData("b64", "false")]
    public async Task RejectsUnsupportedHeaders(string name, string value)
    {
        FakeTimeProvider time = CreateClock();
        JsonObject header = new() { ["typ"] = "JWT", ["alg"] = "ES256" };
        header[name] = JsonNode.Parse(value);
        await AssertRejected(payload: Payload(time).ToJsonString(), time: time, header: header.ToJsonString());
    }

    [Theory]
    [InlineData("""{"typ":"\uD800","alg":"ES256"}""", false)]
    [InlineData("""{"typ":"JWT","alg":"\uDC00"}""", false)]
    [InlineData("""{"typ":"JWT","alg":"ES256","kid":"\uD800"}""", false)]
    [InlineData("""{"typ":"JWT","alg":"ES256","\uD800":0}""", false)]
    [InlineData("""{"typ":"JWT","alg":"ES256","extra":{"nested":["\uDC00"]}}""", false)]
    [InlineData("""{"iss":"\uD800"}""", true)]
    [InlineData("""{"jti":"\uDC00"}""", true)]
    [InlineData("""{"extra":[{"\uD800":"value"}]}""", true)]
    public async Task RejectsMalformedUnicodeBeforeIdentityResolution(string json, bool isPayload)
    {
        FakeTimeProvider time = CreateClock();
        using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using TestServer server = Resolver(null, () => Assert.Fail("Malformed Unicode must be rejected before DID resolution."));
        using HttpClient client = server.CreateClient();
        string token = Sign(
            key,
            isPayload ? """{"typ":"JWT","alg":"ES256"}""" : json,
            isPayload ? json : Payload(time).ToJsonString());
        InvalidDataException exception = await Assert.ThrowsAsync<InvalidDataException>(() => ServiceAuthentication.VerifyAsync(
            token: token,
            audience: s_audience,
            method: s_method,
            httpClient: client,
            timeProvider: time,
            cancellationToken: TestContext.Current.CancellationToken));
        Assert.IsType<InvalidOperationException>(exception.InnerException);
    }

    [Theory]
    [InlineData(0, 60, true)]
    [InlineData(5, 60, true)]
    [InlineData(6, 60, false)]
    [InlineData(0, 900, true)]
    [InlineData(0, 901, false)]
    [InlineData(0, 0, false)]
    [InlineData(0, -1, false)]
    [InlineData(-65, 60, true)]
    [InlineData(-66, 60, false)]
    public async Task EnforcesLifetimeAndClockSkew(int issuedOffset, int lifetime, bool succeeds)
    {
        FakeTimeProvider time = CreateClock();
        JsonObject payload = Payload(time);
        long issuedAt = time.GetUtcNow().AddSeconds(issuedOffset).ToUnixTimeSeconds();
        payload["iat"] = issuedAt;
        payload["exp"] = issuedAt + lifetime;
        using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using TestServer server = Resolver(DidDocumentFor(TestDid, "atproto", key));
        using HttpClient client = server.CreateClient();
        string token = Sign(key, """{"typ":"JWT","alg":"ES256"}""", payload.ToJsonString());
        if (succeeds)
        {
            await ServiceAuthentication.VerifyAsync(
                token: token,
                audience: s_audience,
                method: s_method,
                httpClient: client,
                timeProvider: time,
                cancellationToken: TestContext.Current.CancellationToken);
        }
        else
        {
            await Assert.ThrowsAsync<InvalidDataException>(() => ServiceAuthentication.VerifyAsync(
                token: token,
                audience: s_audience,
                method: s_method,
                httpClient: client,
                timeProvider: time,
                cancellationToken: TestContext.Current.CancellationToken));
        }
    }

    [Theory]
    [InlineData(-30, true)]
    [InlineData(0, true)]
    [InlineData(5, true)]
    [InlineData(6, false)]
    [InlineData(30, false)]
    [InlineData(60, false)]
    [InlineData(61, false)]
    public async Task EnforcesOptionalNotBeforeWithClockSkew(int offsetSeconds, bool succeeds)
    {
        FakeTimeProvider time = CreateClock();
        JsonObject payload = Payload(time);
        payload["nbf"] = time.GetUtcNow().AddSeconds(offsetSeconds).ToUnixTimeSeconds();
        using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using TestServer server = Resolver(DidDocumentFor(TestDid, "atproto", key));
        using HttpClient client = server.CreateClient();
        string token = Sign(key, """{"typ":"JWT","alg":"ES256"}""", payload.ToJsonString());
        if (succeeds)
        {
            ServiceAuthenticationResult result = await ServiceAuthentication.VerifyAsync(
                token: token,
                audience: s_audience,
                method: s_method,
                httpClient: client,
                timeProvider: time,
                cancellationToken: TestContext.Current.CancellationToken);
            Assert.Equal(new Did(TestDid), result.Issuer);
        }
        else
        {
            await Assert.ThrowsAsync<InvalidDataException>(() => ServiceAuthentication.VerifyAsync(
                token: token,
                audience: s_audience,
                method: s_method,
                httpClient: client,
                timeProvider: time,
                cancellationToken: TestContext.Current.CancellationToken));
        }
    }

    [Fact]
    public async Task RechecksNotBeforeAfterResolutionWhenTheClockMovesBackwards()
    {
        ResolutionClock time = new(CreateClock());
        JsonObject payload = Payload(time);
        payload["iat"] = time.GetUtcNow().AddSeconds(-30).ToUnixTimeSeconds();
        payload["nbf"] = time.GetUtcNow().ToUnixTimeSeconds();
        using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using TestServer server = Resolver(
            DidDocumentFor(TestDid, "atproto", key), time.MoveBackwards);
        using HttpClient client = server.CreateClient();
        string token = Sign(key, """{"typ":"JWT","alg":"ES256"}""", payload.ToJsonString());
        await Assert.ThrowsAsync<InvalidDataException>(() => ServiceAuthentication.VerifyAsync(
            token: token,
            audience: s_audience,
            method: s_method,
            httpClient: client,
            timeProvider: time,
            cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task EnforcesConfiguredMaximumLifetime()
    {
        FakeTimeProvider time = CreateClock();
        JsonObject payload = Payload(time);
        payload["exp"] = time.GetUtcNow().AddMinutes(2).ToUnixTimeSeconds();
        using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using TestServer server = Resolver(DidDocumentFor(TestDid, "atproto", key));
        using HttpClient client = server.CreateClient();
        string token = Sign(key, """{"typ":"JWT","alg":"ES256"}""", payload.ToJsonString());

        await Assert.ThrowsAsync<InvalidDataException>(() => ServiceAuthentication.VerifyAsync(
            token: token,
            audience: s_audience,
            method: s_method,
            httpClient: client,
            timeProvider: time,
            cancellationToken: TestContext.Current.CancellationToken,
            maximumLifetime: TimeSpan.FromMinutes(1)));
    }

    [Theory]
    [InlineData("wrong-key")]
    [InlineData("wrong-algorithm")]
    [InlineData("wrong-document")]
    [InlineData("wrong-fragment")]
    [InlineData("duplicate-key")]
    [InlineData("null-method")]
    [InlineData("no-document")]
    [InlineData("corrupt-signature")]
    [InlineData("short-signature")]
    public async Task RequiresAuthenticSignatureFromMatchingDocument(string failure)
    {
        FakeTimeProvider time = CreateClock();
        using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using ECDsa otherKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        DidDocument document = DidDocumentFor(
            failure == "wrong-document" ? OtherDid : TestDid,
            failure == "wrong-fragment" ? "atproto_label" : "atproto",
            failure == "wrong-key" ? otherKey : key);
        if (failure == "duplicate-key")
        {
            document = new DidDocument(
                id: document.Id,
                context: null,
                alsoKnownAs: null,
                verificationMethods: [.. document.VerificationMethods!, .. document.VerificationMethods!],
                services: null);
        }
        else if (failure == "null-method")
        {
            document = document with { VerificationMethods = [null!] };
        }

        using TestServer server = Resolver(failure == "no-document" ? null : document);
        using HttpClient client = server.CreateClient();
        string header = failure == "wrong-algorithm" ? """{"typ":"JWT","alg":"ES256K"}""" : """{"typ":"JWT","alg":"ES256"}""";
        string token = Sign(key, header, Payload(time).ToJsonString());
        if (failure == "corrupt-signature")
        {
            string[] parts = token.Split('.');
            byte[] signature = new byte[64];
            parts[2] = Encode(signature);
            token = string.Join('.', parts);
        }
        else if (failure == "short-signature")
        {
            token = token[..(token.LastIndexOf('.') + 1)] + "AA";
        }

        await Assert.ThrowsAsync<InvalidDataException>(() => ServiceAuthentication.VerifyAsync(
            token: token,
            audience: s_audience,
            method: s_method,
            httpClient: client,
            timeProvider: time,
            cancellationToken: TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("header")]
    [InlineData("payload")]
    [InlineData("nested")]
    [InlineData("escaped")]
    public async Task RejectsDuplicateJsonProperties(string location)
    {
        FakeTimeProvider time = CreateClock();
        string header = location == "header"
            ? """{"typ":"JWT","alg":"ES256","alg":"ES256"}"""
            : """{"typ":"JWT","alg":"ES256"}""";
        string payload = Payload(time).ToJsonString();
        payload = location switch
        {
            "payload" => payload[..^1] + ""","jti":"another-token"}""",
            "nested" => payload[..^1] + ""","extra":[{"a":1,"a":2}]}""",
            "escaped" => payload[..^1] + ""","\u006ati":"another-token"}""",
            _ => payload
        };
        await AssertRejected(payload: payload, time: time, header: header);
    }

    [Theory]
    [InlineData("")]
    [InlineData("one.two")]
    [InlineData("one.two.three.four")]
    [InlineData("e30=.e30.AA")]
    [InlineData("e30.e30.A+")]
    [InlineData("e30.e30.AB")]
    public async Task RejectsMalformedCompactTokens(string token)
    {
        using TestServer server = Resolver(null);
        using HttpClient client = server.CreateClient();
        await Assert.ThrowsAsync<InvalidDataException>(() => ServiceAuthentication.VerifyAsync(
            token: token,
            audience: s_audience,
            method: s_method,
            httpClient: client,
            timeProvider: CreateClock(),
            cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task RejectsUnboundedTokensAndIdentifiers()
    {
        FakeTimeProvider time = CreateClock();
        using TestServer server = Resolver(null);
        using HttpClient client = server.CreateClient();
        await Assert.ThrowsAsync<InvalidDataException>(() => ServiceAuthentication.VerifyAsync(
            token: new string('a', 8193),
            audience: s_audience,
            method: s_method,
            httpClient: client,
            timeProvider: time,
            cancellationToken: TestContext.Current.CancellationToken));
        JsonObject payload = Payload(time);
        payload["jti"] = new string('a', 257);
        await AssertRejected(payload: payload.ToJsonString(), time: time);
    }

    [Fact]
    public async Task RechecksExpiryAfterResolvingIssuer()
    {
        FakeTimeProvider time = CreateClock();
        using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        string token = Sign(key, """{"typ":"JWT","alg":"ES256"}""", Payload(time).ToJsonString());
        using TestServer server = Resolver(DidDocumentFor(TestDid, "atproto", key), () => time.Advance(TimeSpan.FromSeconds(66)));
        using HttpClient client = server.CreateClient();
        await Assert.ThrowsAsync<InvalidDataException>(() => ServiceAuthentication.VerifyAsync(
            token: token,
            audience: s_audience,
            method: s_method,
            httpClient: client,
            timeProvider: time,
            cancellationToken: TestContext.Current.CancellationToken));
    }

    /// <summary>Switches between fixed fake clocks at the explicit resolution boundary to simulate a wall-clock correction.</summary>
    private sealed class ResolutionClock(FakeTimeProvider initialClock) : TimeProvider
    {
        private FakeTimeProvider _clock = initialClock;

        public override DateTimeOffset GetUtcNow() => _clock.GetUtcNow();

        /// <summary>Moves the reported wall clock backwards without relying on scheduler timing.</summary>
        internal void MoveBackwards() => _clock = new FakeTimeProvider(_clock.GetUtcNow().AddSeconds(-6));
    }

    /// <summary>
    /// Creates the shared fixed clock used to issue and validate tokens.
    /// </summary>
    private static FakeTimeProvider CreateClock() => new(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));

    /// <summary>
    /// Creates a complete valid service claim set using the test clock.
    /// </summary>
    private static JsonObject Payload(TimeProvider time) => new()
    {
        ["iss"] = TestDid,
        ["aud"] = s_audience.ToString(),
        ["lxm"] = s_method.ToString(),
        ["iat"] = time.GetUtcNow().ToUnixTimeSeconds(),
        ["exp"] = time.GetUtcNow().AddSeconds(60).ToUnixTimeSeconds(),
        ["jti"] = "unique-token"
    };

    /// <summary>
    /// Encodes unpadded JWT segments.
    /// </summary>
    private static string Encode(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    /// <summary>
    /// Signs the exact supplied JSON representations as a compact JWT.
    /// </summary>
    private static string Sign(ECDsa key, string header, string payload)
    {
        string unsigned = $"{Encode(Encoding.UTF8.GetBytes(header))}.{Encode(Encoding.UTF8.GetBytes(payload))}";
        byte[] signature = key.SignData(Encoding.ASCII.GetBytes(unsigned), HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        return $"{unsigned}.{Encode(signature)}";
    }

    /// <summary>
    /// Resolves a controlled DID document through the production HTTP resolution path.
    /// </summary>
    private static TestServer Resolver(DidDocument? document, Action? onResolution = null) =>
        TestServerBuilder.CreateServer("plc.directory", async context =>
        {
            Assert.Equal($"/{TestDid}", context.Request.Path);
            onResolution?.Invoke();
            if (document is null)
            {
                context.Response.StatusCode = 404;
                return;
            }

            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(
                JsonSerializer.Serialize(document, AtProtoServer.AtProtoJsonSerializerOptions),
                TestContext.Current.CancellationToken);
        });

    /// <summary>
    /// Verifies that correctly signed but invalid claims or headers cannot authenticate.
    /// </summary>
    private static async Task AssertRejected(
        string payload,
        FakeTimeProvider time,
        string header = """{"typ":"JWT","alg":"ES256"}""")
    {
        using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using TestServer server = Resolver(DidDocumentFor(TestDid, "atproto", key));
        using HttpClient client = server.CreateClient();
        string token = Sign(key, header, payload);
        await Assert.ThrowsAsync<InvalidDataException>(() => ServiceAuthentication.VerifyAsync(
            token: token,
            audience: s_audience,
            method: s_method,
            httpClient: client,
            timeProvider: time,
            cancellationToken: TestContext.Current.CancellationToken));
    }
}
