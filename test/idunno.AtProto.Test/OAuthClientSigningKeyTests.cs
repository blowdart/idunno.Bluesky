// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using Duende.IdentityModel.OidcClient;

using idunno.AtProto.Authentication;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace idunno.AtProto.Test;

[ExcludeFromCodeCoverage]
public class OAuthClientSigningKeyTests
{
    private const string ClientId = "https://client.test/oauth-client-metadata.json";

    private static readonly DateTimeOffset s_now = new(2025, 1, 2, 3, 4, 5, TimeSpan.Zero);

    [Fact]
    public void ClientAssertionClockSkewDefaultsToThirtySeconds()
    {
        OAuthOptions options = new();

        Assert.Equal(TimeSpan.FromSeconds(30), OAuthOptions.DefaultClientAssertionClockSkew);
        Assert.Equal(OAuthOptions.DefaultClientAssertionClockSkew, options.ClientAssertionClockSkew);
    }

    [Fact]
    public void ClientAssertionClockSkewRejectsNegativeValues()
    {
        OAuthOptions options = new();

        Assert.Throws<ArgumentOutOfRangeException>(() => options.ClientAssertionClockSkew = TimeSpan.FromTicks(-1));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(10)]
    [InlineData(30)]
    [InlineData(60)]
    public async Task ConfigureClientAssertionUsesTheConfiguredClockSkew(int clockSkewSeconds)
    {
        OAuthOptions options = new(ClientId)
        {
            ClientSigningKey = CreateKey(),
            ClientAssertionClockSkew = TimeSpan.FromSeconds(clockSkewSeconds)
        };
        FakeTimeProvider timeProvider = new(s_now);
        OAuthClient client = CreateOAuthClient(options, timeProvider);
        OidcClientOptions oidcOptions = new();
        client.ConfigureClientAssertion(oidcOptions, ClientId, new Uri("https://auth.example.com/"));

        Duende.IdentityModel.Client.ClientAssertion first = await oidcOptions.GetClientAssertionAsync();
        Assert.Equal(s_now.ToUnixTimeSeconds() - clockSkewSeconds, long.Parse(GetClaim(first.Value, "iat"), System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal(s_now.ToUnixTimeSeconds() + 60, long.Parse(GetClaim(first.Value, "exp"), System.Globalization.CultureInfo.InvariantCulture));

        timeProvider.Advance(TimeSpan.FromSeconds(5));
        Duende.IdentityModel.Client.ClientAssertion second = await oidcOptions.GetClientAssertionAsync();
        Assert.Equal(s_now.ToUnixTimeSeconds() + 5 - clockSkewSeconds, long.Parse(GetClaim(second.Value, "iat"), System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal(s_now.ToUnixTimeSeconds() + 65, long.Parse(GetClaim(second.Value, "exp"), System.Globalization.CultureInfo.InvariantCulture));
        Assert.NotEqual(GetClaim(first.Value, "jti"), GetClaim(second.Value, "jti"));
    }

    [Fact]
    public void FromPemUsesTheThumbprintOfThePublicKeyAsTheKeyId()
    {
        using ECDsa ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        ECParameters publicParameters = ecdsa.ExportParameters(includePrivateParameters: false);

        OAuthClientSigningKey key = OAuthClientSigningKey.FromPem(ecdsa.ExportPkcs8PrivateKeyPem());

        string x = Base64UrlEncode(publicParameters.Q.X!);
        string y = Base64UrlEncode(publicParameters.Q.Y!);
        string expectedKeyId = Base64UrlEncode(SHA256.HashData(Encoding.UTF8.GetBytes(
            $$"""{"crv":"P-256","kty":"EC","x":"{{x}}","y":"{{y}}"}""")));

        Assert.Equal(x, key.X);
        Assert.Equal(y, key.Y);
        Assert.Equal(expectedKeyId, key.KeyId);
        Assert.Equal("ES256", OAuthClientSigningKey.Algorithm);
        Assert.Equal("P-256", OAuthClientSigningKey.Curve);
    }

    [Fact]
    public void FromPemAcceptsSec1PrivateKeys()
    {
        using ECDsa ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);

        OAuthClientSigningKey key = OAuthClientSigningKey.FromPem(ecdsa.ExportECPrivateKeyPem());

        Assert.Equal(Base64UrlEncode(ecdsa.ExportParameters(false).Q.X!), key.X);
    }

    [Fact]
    public void FromPemFileLoadsTheKeyFromAFile()
    {
        using ECDsa ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        string path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, ecdsa.ExportPkcs8PrivateKeyPem());

            OAuthClientSigningKey key = OAuthClientSigningKey.FromPemFile(path, "custom-kid");

            Assert.Equal(Base64UrlEncode(ecdsa.ExportParameters(false).Q.X!), key.X);
            Assert.Equal("custom-kid", key.KeyId);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void FromPemFileThrowsWhenTheFileDoesNotExist()
    {
        string path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.pem");

        FileNotFoundException exception = Assert.Throws<FileNotFoundException>(() => OAuthClientSigningKey.FromPemFile(path));

        Assert.Equal(path, exception.FileName);
    }

    [Fact]
    public void FromPemUsesTheSpecifiedKeyId()
    {
        using ECDsa ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);

        OAuthClientSigningKey key = OAuthClientSigningKey.FromPem(ecdsa.ExportPkcs8PrivateKeyPem(), "custom-kid");

        Assert.Equal("custom-kid", key.KeyId);
    }

    public static TheoryData<string> InvalidPems()
    {
        using ECDsa p256 = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using ECDsa p384 = ECDsa.Create(ECCurve.NamedCurves.nistP384);
        using RSA rsa = RSA.Create(2048);

        return new TheoryData<string>
        {
            p256.ExportSubjectPublicKeyInfoPem(),
            p384.ExportPkcs8PrivateKeyPem(),
            rsa.ExportPkcs8PrivateKeyPem(),
            p256.ExportEncryptedPkcs8PrivateKeyPem(
                "password",
                new PbeParameters(PbeEncryptionAlgorithm.Aes256Cbc, HashAlgorithmName.SHA256, 1000)),
            "not a pem",
            " "
        };
    }

    [Theory]
    [MemberData(nameof(InvalidPems))]
    public void FromPemRejectsKeysThatAreNotUnencryptedP256PrivateKeys(string pem)
    {
        ArgumentException exception = Assert.ThrowsAny<ArgumentException>(() => OAuthClientSigningKey.FromPem(pem));

        Assert.Equal("pem", exception.ParamName);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void FromPemRejectsAnEmptyKeyId(string keyId)
    {
        using ECDsa ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);

        Assert.ThrowsAny<ArgumentException>(() => OAuthClientSigningKey.FromPem(ecdsa.ExportPkcs8PrivateKeyPem(), keyId));
    }

    [Theory]
    [InlineData("https://auth.example.com", "https://auth.example.com")]
    [InlineData("https://auth.example.com/", "https://auth.example.com")]
    [InlineData("https://auth.example.com:8443/oauth/", "https://auth.example.com:8443")]
    public void CreateClientAssertionProducesAVerifiableEs256Jwt(string authority, string expectedAudience)
    {
        using ECDsa ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        OAuthClientSigningKey key = OAuthClientSigningKey.FromPem(ecdsa.ExportPkcs8PrivateKeyPem());

        string assertion = key.CreateClientAssertion(ClientId, new Uri(authority), s_now, OAuthOptions.DefaultClientAssertionClockSkew);

        string[] parts = assertion.Split('.');
        Assert.Equal(3, parts.Length);

        using JsonDocument header = JsonDocument.Parse(Base64UrlDecode(parts[0]));
        Assert.Equal("ES256", header.RootElement.GetProperty("alg").GetString());
        Assert.Equal("JWT", header.RootElement.GetProperty("typ").GetString());
        Assert.Equal(key.KeyId, header.RootElement.GetProperty("kid").GetString());

        using JsonDocument payload = JsonDocument.Parse(Base64UrlDecode(parts[1]));
        JsonElement claims = payload.RootElement;
        Assert.Equal(ClientId, claims.GetProperty("iss").GetString());
        Assert.Equal(ClientId, claims.GetProperty("sub").GetString());
        Assert.Equal(expectedAudience, claims.GetProperty("aud").GetString());
        Assert.False(string.IsNullOrWhiteSpace(claims.GetProperty("jti").GetString()));
        Assert.Equal(s_now.ToUnixTimeSeconds() - 30, claims.GetProperty("iat").GetInt64());
        Assert.Equal(s_now.ToUnixTimeSeconds() + 60, claims.GetProperty("exp").GetInt64());

        byte[] signature = Base64UrlDecode(parts[2]);
        Assert.Equal(64, signature.Length);
        Assert.True(ecdsa.VerifyData(Encoding.ASCII.GetBytes($"{parts[0]}.{parts[1]}"), signature, HashAlgorithmName.SHA256));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(29)]
    public void CreateClientAssertionIsValidWhenTheClientClockIsSlightlyAhead(int clientClockLeadSeconds)
    {
        OAuthClientSigningKey key = CreateKey();
        DateTimeOffset clientNow = s_now.AddSeconds(clientClockLeadSeconds);

        string assertion = key.CreateClientAssertion(ClientId, new Uri("https://auth.example.com"), clientNow, OAuthOptions.DefaultClientAssertionClockSkew);

        using JsonDocument payload = JsonDocument.Parse(Base64UrlDecode(assertion.Split('.')[1]));
        long issuedAt = payload.RootElement.GetProperty("iat").GetInt64();
        long expiresAt = payload.RootElement.GetProperty("exp").GetInt64();
        Assert.True(issuedAt < s_now.ToUnixTimeSeconds());
        Assert.True(expiresAt > s_now.ToUnixTimeSeconds());
        Assert.Equal(clientNow.ToUnixTimeSeconds() + 60, expiresAt);
    }

    [Fact]
    public void CreateClientAssertionUsesAUniqueJtiForEveryAssertion()
    {
        using ECDsa ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        OAuthClientSigningKey key = OAuthClientSigningKey.FromPem(ecdsa.ExportPkcs8PrivateKeyPem());
        Uri authority = new("https://auth.example.com");

        string first = GetClaim(key.CreateClientAssertion(ClientId, authority, s_now, OAuthOptions.DefaultClientAssertionClockSkew), "jti");
        string second = GetClaim(key.CreateClientAssertion(ClientId, authority, s_now, OAuthOptions.DefaultClientAssertionClockSkew), "jti");

        Assert.NotEqual(first, second);
    }

    [Theory]
    [InlineData("http://localhost")]
    [InlineData("http://LOCALHOST/?scope=atproto")]
    public void ValidateRejectsASigningKeyForALocalhostClientId(string clientId)
    {
        OAuthOptions options = new(clientId)
        {
            ClientSigningKey = CreateKey()
        };

        Assert.Throws<ArgumentException>(options.Validate);
    }

    [Fact]
    public void ValidateRejectsASigningKeyPathForALocalhostClientId()
    {
        OAuthOptions options = new("http://localhost")
        {
            ClientSigningKeyPath = "client-signing-key.pem"
        };

        Assert.Throws<ArgumentException>(options.Validate);
    }

    [Fact]
    public void ValidateAcceptsASigningKeyForAPublishedClientId()
    {
        OAuthOptions options = new(ClientId)
        {
            ClientSigningKey = CreateKey()
        };

        options.Validate();
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void ValidateRejectsAdditionalSigningKeysForALocalhostClientId(bool withKey, bool withPath)
    {
        OAuthOptions options = new("http://localhost");
        AddAdditionalKeys(options, withKey, withPath);

        Assert.Throws<ArgumentException>(options.Validate);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void ValidateRejectsAdditionalSigningKeysWithoutAnActiveKey(bool withKey, bool withPath)
    {
        OAuthOptions options = new(ClientId);
        AddAdditionalKeys(options, withKey, withPath);

        Assert.Throws<ArgumentException>(options.Validate);
    }

    [Fact]
    public void ValidateRejectsSigningKeysWhichShareAKeyId()
    {
        OAuthOptions options = new(ClientId)
        {
            ClientSigningKey = CreateKey("shared")
        };
        options.AdditionalClientSigningKeys.Add(CreateKey("shared"));

        Assert.Throws<ArgumentException>(options.Validate);
    }

    [Fact]
    public void ValidateAcceptsAdditionalSigningKeysWithAnActiveKey()
    {
        OAuthOptions options = new(ClientId)
        {
            ClientSigningKey = CreateKey()
        };
        options.AdditionalClientSigningKeys.Add(CreateKey());
        options.AdditionalClientSigningKeyPaths.Add("previous-key.pem");

        options.Validate();
    }

    [Theory]
    [InlineData(null, "active", true)]
    [InlineData("active", "active", true)]
    [InlineData("previous", "previous", true)]
    [InlineData("unknown", "active", false)]
    public void GetClientSigningKeySelectsTheSessionKey(string? sessionKeyId, string expectedKeyId, bool expectedFound)
    {
        OAuthOptions options = new(ClientId)
        {
            ClientSigningKey = CreateKey("active")
        };
        options.AdditionalClientSigningKeys.Add(CreateKey("previous"));

        OAuthClientSigningKey? key = options.GetClientSigningKey(sessionKeyId, out bool found);

        Assert.Equal(expectedKeyId, key?.KeyId);
        Assert.Equal(expectedFound, found);
    }

    [Theory]
    [InlineData(null, "active")]
    [InlineData("previous", "previous")]
    [InlineData("unknown", "active")]
    public async Task ConfigureClientAssertionSignsWithTheSessionKey(string? sessionKeyId, string expectedKeyId)
    {
        OAuthOptions options = new(ClientId)
        {
            ClientSigningKey = CreateKey("active")
        };
        options.AdditionalClientSigningKeys.Add(CreateKey("previous"));
        OAuthClient client = CreateOAuthClient(options, new FakeTimeProvider(s_now));
        OidcClientOptions oidcOptions = new();

        string? usedKeyId = client.ConfigureClientAssertion(oidcOptions, ClientId, new Uri("https://auth.example.com/"), sessionKeyId);

        Duende.IdentityModel.Client.ClientAssertion assertion = await oidcOptions.GetClientAssertionAsync();
        Assert.Equal(expectedKeyId, usedKeyId);
        Assert.Equal(expectedKeyId, GetHeader(assertion.Value, "kid"));
    }

    [Fact]
    public async Task ConfigureClientAssertionAddsAFreshAssertionForEachRequest()
    {
        OAuthClientSigningKey key = CreateKey();
        FakeTimeProvider timeProvider = new(s_now);
        OAuthClient client = CreateOAuthClient(new OAuthOptions(ClientId) { ClientSigningKey = key }, timeProvider);
        OidcClientOptions oidcOptions = new();

        client.ConfigureClientAssertion(oidcOptions, ClientId, new Uri("https://auth.example.com/"));

        Duende.IdentityModel.Client.ClientAssertion first = await oidcOptions.GetClientAssertionAsync();
        Duende.IdentityModel.Client.ClientAssertion second = await oidcOptions.GetClientAssertionAsync();

        Assert.Equal("urn:ietf:params:oauth:client-assertion-type:jwt-bearer", first.Type);
        Assert.Equal("https://auth.example.com", GetClaim(first.Value, "aud"));
        Assert.Equal(ClientId, GetClaim(first.Value, "iss"));
        Assert.NotEqual(GetClaim(first.Value, "jti"), GetClaim(second.Value, "jti"));
    }

    [Theory]
    [InlineData(true, "http://localhost?scope=atproto")]
    [InlineData(false, ClientId)]
    public void ConfigureClientAssertionDoesNothingForPublicClients(bool withKey, string clientId)
    {
        OAuthOptions options = new(ClientId)
        {
            ClientSigningKey = withKey ? CreateKey() : null
        };
        OAuthClient client = CreateOAuthClient(options, new FakeTimeProvider(s_now));
        OidcClientOptions oidcOptions = new();
        Func<Task<Duende.IdentityModel.Client.ClientAssertion>> defaultFactory = oidcOptions.GetClientAssertionAsync;

        client.ConfigureClientAssertion(oidcOptions, clientId, new Uri("https://auth.example.com/"));

        Assert.Same(defaultFactory, oidcOptions.GetClientAssertionAsync);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(10)]
    [InlineData(30)]
    [InlineData(60)]
    public void BuildRevocationFormAddsAClientAssertionForConfidentialClients(int clockSkewSeconds)
    {
        using AtProtoAgent agent = CreateAgent(new OAuthOptions(ClientId)
        {
            ClientAssertionClockSkew = TimeSpan.FromSeconds(clockSkewSeconds)
        });
        OAuthClientSigningKey key = CreateKey();

        Dictionary<string, string> form = new(agent.BuildRevocationForm("token", "refresh_token", ClientId, new Uri("https://auth.example.com/"), key));

        Assert.Equal("token", form["token"]);
        Assert.Equal("refresh_token", form["token_type_hint"]);
        Assert.Equal(ClientId, form["client_id"]);
        Assert.Equal("urn:ietf:params:oauth:client-assertion-type:jwt-bearer", form["client_assertion_type"]);
        Assert.Equal("https://auth.example.com", GetClaim(form["client_assertion"], "aud"));
        Assert.Equal(s_now.ToUnixTimeSeconds() - clockSkewSeconds, long.Parse(GetClaim(form["client_assertion"], "iat"), System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal(s_now.ToUnixTimeSeconds() + 60, long.Parse(GetClaim(form["client_assertion"], "exp"), System.Globalization.CultureInfo.InvariantCulture));
    }

    [Theory]
    [InlineData(true, "http://localhost?scope=atproto")]
    [InlineData(false, ClientId)]
    public void BuildRevocationFormOmitsTheClientAssertionForPublicClients(bool withKey, string clientId)
    {
        using AtProtoAgent agent = CreateAgent();

        Dictionary<string, string> form = new(agent.BuildRevocationForm("token", "access_token", clientId, new Uri("https://auth.example.com/"), withKey ? CreateKey() : null));

        Assert.Equal(["token", "token_type_hint", "client_id"], form.Keys);
    }

    private static OAuthClientSigningKey CreateKey(string? keyId = null)
    {
        using ECDsa ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);

        return OAuthClientSigningKey.FromPem(ecdsa.ExportPkcs8PrivateKeyPem(), keyId);
    }

    private static void AddAdditionalKeys(OAuthOptions options, bool withKey, bool withPath)
    {
        if (withKey)
        {
            options.AdditionalClientSigningKeys.Add(CreateKey());
        }

        if (withPath)
        {
            options.AdditionalClientSigningKeyPaths.Add("previous-key.pem");
        }
    }

    private static string GetHeader(string assertion, string name)
    {
        using JsonDocument header = JsonDocument.Parse(Base64UrlDecode(assertion.Split('.')[0]));

        return header.RootElement.GetProperty(name).GetString()!;
    }

    private static OAuthClient CreateOAuthClient(OAuthOptions options, TimeProvider timeProvider) =>
        new(httpClientConfigurator: httpClient => httpClient,
            innerHandlerFactory: () => new HttpClientHandler(),
            loggerFactory: null,
            options: options,
            timeProvider: timeProvider);

    private static AtProtoAgent CreateAgent(OAuthOptions? oAuthOptions = null) =>
        new(new Uri("https://example.com"),
            new AtProtoAgentOptions(NullLoggerFactory.Instance)
            {
                TimeProvider = new FakeTimeProvider(s_now),
                OAuthOptions = oAuthOptions
            });

    private static string GetClaim(string assertion, string claim)
    {
        using JsonDocument payload = JsonDocument.Parse(Base64UrlDecode(assertion.Split('.')[1]));
        JsonElement value = payload.RootElement.GetProperty(claim);

        return value.ValueKind == JsonValueKind.String ? value.GetString()! : value.GetRawText();
    }

    private static string Base64UrlEncode(byte[] value) =>
        Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] Base64UrlDecode(string value)
    {
        string base64 = value.Replace('-', '+').Replace('_', '/');

        return Convert.FromBase64String(base64.PadRight(base64.Length + ((4 - (base64.Length % 4)) % 4), '='));
    }
}
