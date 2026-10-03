// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Net;
using System.Text.Json;

using idunno.AtProto;
using idunno.AtProto.Authentication;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace idunno.Bluesky.AspNet.Authentication.Test;

public class OAuthClientMetadataTests
{
    private const string ClientId = "https://app.example.com/oauth-client-metadata.json";
    private const string Callback = "https://app.example.com/Bluesky/Callback";

    [Fact]
    public void GeneratesRequiredPublicWebClientFieldsWithoutOptionalFields()
    {
        using JsonDocument document = JsonDocument.Parse(new BlueskyOAuthClientMetadataOptions().GenerateJson(
            new OAuthOptions(ClientId, new Uri(Callback))));
        JsonElement root = document.RootElement;

        Assert.Equal(ClientId, root.GetProperty("client_id").GetString());
        Assert.Equal(Callback, root.GetProperty("redirect_uris")[0].GetString());
        Assert.Equal("atproto", root.GetProperty("scope").GetString());
        Assert.Equal("web", root.GetProperty("application_type").GetString());
        Assert.Equal("none", root.GetProperty("token_endpoint_auth_method").GetString());
        Assert.True(root.GetProperty("dpop_bound_access_tokens").GetBoolean());
        Assert.Equal(["authorization_code", "refresh_token"], root.GetProperty("grant_types").EnumerateArray().Select(value => value.GetString()));
        Assert.Equal("code", Assert.Single(root.GetProperty("response_types").EnumerateArray()).GetString());
        Assert.Equal(8, root.EnumerateObject().Count());
        Assert.False(root.TryGetProperty("jwks", out _));
        Assert.False(root.TryGetProperty("token_endpoint_auth_signing_alg", out _));
    }

    [Fact]
    public void PublishesThePublicKeyForAConfidentialClient()
    {
        using System.Security.Cryptography.ECDsa ecdsa = System.Security.Cryptography.ECDsa.Create(
            System.Security.Cryptography.ECCurve.NamedCurves.nistP256);
        OAuthClientSigningKey signingKey = OAuthClientSigningKey.FromPem(ecdsa.ExportPkcs8PrivateKeyPem());
        OAuthOptions oAuthOptions = new(ClientId, new Uri(Callback))
        {
            ClientSigningKey = signingKey
        };

        string json = new BlueskyOAuthClientMetadataOptions().GenerateJson(oAuthOptions);

        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;
        Assert.Equal("private_key_jwt", root.GetProperty("token_endpoint_auth_method").GetString());
        Assert.Equal("ES256", root.GetProperty("token_endpoint_auth_signing_alg").GetString());
        Assert.False(root.TryGetProperty("jwks_uri", out _));
        Assert.Equal(10, root.EnumerateObject().Count());

        JsonElement key = Assert.Single(root.GetProperty("jwks").GetProperty("keys").EnumerateArray());
        Assert.Equal(7, key.EnumerateObject().Count());
        Assert.Equal("EC", key.GetProperty("kty").GetString());
        Assert.Equal("P-256", key.GetProperty("crv").GetString());
        Assert.Equal(signingKey.X, key.GetProperty("x").GetString());
        Assert.Equal(signingKey.Y, key.GetProperty("y").GetString());
        Assert.Equal(signingKey.KeyId, key.GetProperty("kid").GetString());
        Assert.Equal("ES256", key.GetProperty("alg").GetString());
        Assert.Equal("sig", key.GetProperty("use").GetString());
        Assert.False(key.TryGetProperty("d", out _));
    }

    [Fact]
    public void PublishesTheActiveAndAdditionalPublicKeys()
    {
        OAuthClientSigningKey activeKey = CreateSigningKey("active");
        OAuthClientSigningKey previousKey = CreateSigningKey("previous");
        OAuthOptions oAuthOptions = new(ClientId, new Uri(Callback))
        {
            ClientSigningKey = activeKey
        };
        oAuthOptions.AdditionalClientSigningKeys.Add(previousKey);

        string json = new BlueskyOAuthClientMetadataOptions().GenerateJson(oAuthOptions);

        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement[] keys = [.. document.RootElement.GetProperty("jwks").GetProperty("keys").EnumerateArray()];
        Assert.Equal(["active", "previous"], keys.Select(key => key.GetProperty("kid").GetString()));
        Assert.Equal([activeKey.X, previousKey.X], keys.Select(key => key.GetProperty("x").GetString()));
    }

    [Fact]
    public void AdditionalKeysWithoutAnActiveKeyAreNotPublished()
    {
        OAuthOptions oAuthOptions = new(ClientId, new Uri(Callback));
        oAuthOptions.AdditionalClientSigningKeys.Add(CreateSigningKey("previous"));

        string json = new BlueskyOAuthClientMetadataOptions().GenerateJson(oAuthOptions);

        using JsonDocument document = JsonDocument.Parse(json);
        Assert.Equal("none", document.RootElement.GetProperty("token_endpoint_auth_method").GetString());
        Assert.False(document.RootElement.TryGetProperty("jwks", out _));
    }

    private static OAuthClientSigningKey CreateSigningKey(string keyId)
    {
        using System.Security.Cryptography.ECDsa ecdsa = System.Security.Cryptography.ECDsa.Create(
            System.Security.Cryptography.ECCurve.NamedCurves.nistP256);

        return OAuthClientSigningKey.FromPem(ecdsa.ExportPkcs8PrivateKeyPem(), keyId);
    }

    [Fact]
    public void IncludesBrandingPermissionSetsAndAdditionalScopesWithoutChangingLoginScopes()
    {
        OAuthOptions oAuthOptions = new(ClientId, new Uri(Callback), ["atproto", "transition:generic"])
        {
            ClientName = "Example",
            ClientUri = new Uri("https://app.example.com/"),
            TosUri = new Uri("https://app.example.com/terms"),
            PolicyUri = new Uri("https://app.example.com/privacy"),
            PermissionSets = [new OAuthPermissionSet(new Nsid("app.example.permissions"))]
        };
        BlueskyOAuthClientMetadataOptions options = new()
        {
            LogoUri = new Uri("https://cdn.example.com/logo.png")
        };
        options.AdditionalScopes.Add("transition:generic");
        options.AdditionalScopes.Add("transition:email");

        using JsonDocument document = JsonDocument.Parse(options.GenerateJson(oAuthOptions));
        JsonElement root = document.RootElement;
        Assert.Equal("atproto transition:generic include:app.example.permissions transition:email", root.GetProperty("scope").GetString());
        Assert.Equal("Example", root.GetProperty("client_name").GetString());
        Assert.Equal(oAuthOptions.ClientUri.OriginalString, root.GetProperty("client_uri").GetString());
        Assert.Equal(options.LogoUri.OriginalString, root.GetProperty("logo_uri").GetString());
        Assert.Equal(oAuthOptions.TosUri.OriginalString, root.GetProperty("tos_uri").GetString());
        Assert.Equal(oAuthOptions.PolicyUri.OriginalString, root.GetProperty("policy_uri").GetString());
        Assert.Equal(["atproto", "transition:generic", "include:app.example.permissions"], oAuthOptions.GetRequestedScopes());
    }

    [Theory]
    [InlineData("http://localhost")]
    [InlineData("http://app.example.com/metadata")]
    [InlineData("https://app.example.com:443/metadata")]
    [InlineData("https://app.example.com:8443/metadata")]
    [InlineData("https://app.example.com/metadata#fragment")]
    [InlineData("https://user:password@app.example.com/metadata")]
    [InlineData("/metadata")]
    [InlineData("https://app.example.com/meta data")]
    [InlineData(" https://app.example.com/metadata")]
    [InlineData("https://app.example.com/metadata%ZZ")]
    [InlineData("")]
    public void RejectsInvalidClientIds(string clientId)
    {
        Assert.Throws<ArgumentException>(() => new BlueskyOAuthClientMetadataOptions().GenerateJson(
            new OAuthOptions(clientId, new Uri(Callback))));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("/callback")]
    [InlineData("http://app.example.com/callback")]
    [InlineData("https://app.example.com/callback#fragment")]
    [InlineData("https://user:password@app.example.com/callback")]
    [InlineData("https://app.example.com:443/callback")]
    public void RejectsInvalidCallbacks(string? callback)
    {
        Assert.Throws<ArgumentException>(() => new BlueskyOAuthClientMetadataOptions().GenerateJson(
            new OAuthOptions(ClientId, callback is null ? null : new Uri(callback, UriKind.RelativeOrAbsolute))));
    }

    [Theory]
    [InlineData("")]
    [InlineData("two scopes")]
    [InlineData("scope\n")]
    [InlineData("scope\"")]
    [InlineData("scope\\")]
    [InlineData(null)]
    public void RejectsInvalidScopeTokens(string? scope)
    {
        BlueskyOAuthClientMetadataOptions options = new();
        options.AdditionalScopes.Add(scope!);
        Assert.Throws<ArgumentException>(() => options.GenerateJson(new OAuthOptions(ClientId, new Uri(Callback))));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ReadsOptionalBrandingFromAgentOAuthConfiguration(bool includeBranding)
    {
        Dictionary<string, string?> settings = new()
        {
            ["BlueskyAgent:OAuthOptions:ClientId"] = ClientId,
            ["BlueskyAgent:OAuthOptions:ReturnUri"] = Callback,
            ["BlueskyAgent:OAuthOptions:Scopes:0"] = "atproto"
        };
        if (includeBranding)
        {
            settings["BlueskyAgent:OAuthOptions:ClientName"] = "Configured app";
            settings["BlueskyAgent:OAuthOptions:ClientUri"] = "https://app.example.com/";
            settings["BlueskyAgent:OAuthOptions:TosUri"] = "https://app.example.com/terms";
            settings["BlueskyAgent:OAuthOptions:PolicyUri"] = "https://app.example.com/privacy";
        }

        IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        ServiceCollection services = new();
        services.Configure<BlueskyAgentOptions>(configuration.GetSection("BlueskyAgent"));
        services.AddBlueskyOAuthClientMetadata();
        using ServiceProvider provider = services.BuildServiceProvider();
        OAuthOptions oAuthOptions = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<BlueskyAgentOptions>>().Value.OAuthOptions!;
        using JsonDocument document = JsonDocument.Parse(new BlueskyOAuthClientMetadataOptions().GenerateJson(oAuthOptions));
        JsonElement root = document.RootElement;
        if (includeBranding)
        {
            Assert.Equal("Configured app", root.GetProperty("client_name").GetString());
            Assert.Equal("https://app.example.com/", root.GetProperty("client_uri").GetString());
            Assert.Equal("https://app.example.com/terms", root.GetProperty("tos_uri").GetString());
            Assert.Equal("https://app.example.com/privacy", root.GetProperty("policy_uri").GetString());
        }
        else
        {
            Assert.Null(oAuthOptions.ClientName);
            Assert.Null(oAuthOptions.ClientUri);
            Assert.False(root.TryGetProperty("client_name", out _));
            Assert.False(root.TryGetProperty("client_uri", out _));
            Assert.Null(oAuthOptions.TosUri);
            Assert.Null(oAuthOptions.PolicyUri);
            Assert.False(root.TryGetProperty("tos_uri", out _));
            Assert.False(root.TryGetProperty("policy_uri", out _));
        }
    }

    [Fact]
    public void RequiresAtProtoScope()
    {
        Assert.Throws<ArgumentException>(() => new BlueskyOAuthClientMetadataOptions().GenerateJson(
            new OAuthOptions(ClientId, new Uri(Callback), ["transition:generic"])));
    }

    [Theory]
    [InlineData(null, "include:app.bsky.authViewAll")]
    [InlineData("*", "include:app.bsky.authViewAll?aud=%2A")]
    [InlineData("did:web:api.bsky.app#bsky_appview", "include:app.bsky.authViewAll?aud=did%3Aweb%3Aapi.bsky.app%23bsky_appview")]
    public void BindsStructuredPermissionSetsFromAgentConfiguration(string? audience, string expectedScope)
    {
        Dictionary<string, string?> settings = new()
        {
            ["BlueskyAgent:OAuthOptions:ClientId"] = ClientId,
            ["BlueskyAgent:OAuthOptions:ReturnUri"] = Callback,
            ["BlueskyAgent:OAuthOptions:Scopes:0"] = "atproto",
            ["BlueskyAgent:OAuthOptions:PermissionSets:0:Nsid"] = "app.bsky.authViewAll",
            ["BlueskyAgent:OAuthOptions:PermissionSets:1:Nsid"] = "com.example.authBasic"
        };
        if (audience is not null)
        {
            settings["BlueskyAgent:OAuthOptions:PermissionSets:0:Audience"] = audience;
        }

        IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        ServiceCollection services = new();
        services.Configure<BlueskyAgentOptions>(configuration.GetSection("BlueskyAgent"), options => options.ErrorOnUnknownConfiguration = true);
        using ServiceProvider provider = services.BuildServiceProvider();
        OAuthOptions oAuthOptions = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<BlueskyAgentOptions>>().Value.OAuthOptions!;
        Assert.Equal(2, oAuthOptions.PermissionSets.Count());
        Assert.Equal(audience, oAuthOptions.PermissionSets.First().Audience);
        Assert.Equal(["atproto", expectedScope, "include:com.example.authBasic"], oAuthOptions.GetRequestedScopes());
        using JsonDocument document = JsonDocument.Parse(new BlueskyOAuthClientMetadataOptions().GenerateJson(oAuthOptions));
        Assert.Equal($"atproto {expectedScope} include:com.example.authBasic", document.RootElement.GetProperty("scope").GetString());
    }

    [Theory]
    [InlineData("not-an-nsid", null)]
    [InlineData("", null)]
    [InlineData(null, "did:web:api.bsky.app#bsky_appview")]
    [InlineData("app.bsky.authViewAll", "not-a-did")]
    [InlineData("app.bsky.authViewAll", "did:web:api.bsky.app")]
    public void RejectsInvalidStructuredPermissionSetConfiguration(string? nsid, string? audience)
    {
        Dictionary<string, string?> settings = new()
        {
            ["OAuthOptions:PermissionSets:0:Nsid"] = nsid,
            ["OAuthOptions:PermissionSets:0:Audience"] = audience
        };
        IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        Assert.Throws<InvalidOperationException>(() => configuration.Get<BlueskyAgentOptions>(
            options => options.ErrorOnUnknownConfiguration = true));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RawAndStructuredPermissionSetConfigurationProduceEquivalentScopes(bool structured)
    {
        const string viewAllScope = "include:app.bsky.authViewAll?aud=did%3Aweb%3Aapi.bsky.app%23bsky_appview";
        Dictionary<string, string?> settings = new()
        {
            ["ClientId"] = ClientId,
            ["ReturnUri"] = Callback,
            ["Scopes:0"] = "atproto",
            ["Scopes:1"] = viewAllScope
        };
        if (structured)
        {
            settings["PermissionSets:0:Nsid"] = "app.bsky.authViewAll";
            settings["PermissionSets:0:Audience"] = "did:web:api.bsky.app#bsky_appview";
        }

        IConfiguration configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        OAuthOptions oAuthOptions = configuration.Get<OAuthOptions>(options => options.ErrorOnUnknownConfiguration = true)!;
        Assert.Equal(["atproto", viewAllScope], oAuthOptions.GetRequestedScopes());
        Assert.Equal(structured ? 1 : 0, oAuthOptions.PermissionSets.Count());
        using JsonDocument document = JsonDocument.Parse(new BlueskyOAuthClientMetadataOptions().GenerateJson(oAuthOptions));
        Assert.Equal($"atproto {viewAllScope}", document.RootElement.GetProperty("scope").GetString());
    }

    [Theory]
    [InlineData(nameof(OAuthOptions.ClientUri), "https://other.example.com/")]
    [InlineData(nameof(OAuthOptions.ClientUri), "/homepage")]
    [InlineData(nameof(BlueskyOAuthClientMetadataOptions.LogoUri), "http://app.example.com/logo")]
    [InlineData(nameof(OAuthOptions.TosUri), "/terms")]
    [InlineData(nameof(OAuthOptions.PolicyUri), "http://app.example.com/privacy")]
    public void RejectsInvalidOptionalUrls(string property, string value)
    {
        BlueskyOAuthClientMetadataOptions options = new();
        OAuthOptions oAuthOptions = new(ClientId, new Uri(Callback));
        Uri uri = new(value, UriKind.RelativeOrAbsolute);
        switch (property)
        {
            case nameof(oAuthOptions.ClientUri):
                oAuthOptions.ClientUri = uri;
                break;
            case nameof(options.LogoUri):
                options.LogoUri = uri;
                break;
            case nameof(oAuthOptions.TosUri):
                oAuthOptions.TosUri = uri;
                break;
            case nameof(oAuthOptions.PolicyUri):
                oAuthOptions.PolicyUri = uri;
                break;
        }

        Assert.Throws<ArgumentException>(() => options.GenerateJson(oAuthOptions));
    }

    [Theory]
    [InlineData("")]
    [InlineData("/site")]
    public async Task PublishesAtConfiguredPathIncludingPathBaseBeforeDownstreamMiddleware(string pathBase)
    {
        string clientId = $"https://app.example.com{pathBase}/metadata.json";
        using IHost host = await CreateHost(clientId, pathBase);
        using HttpClient client = host.GetTestClient();
        using HttpResponseMessage response = await client.GetAsync($"{pathBase}/metadata.json", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal(clientId, document.RootElement.GetProperty("client_id").GetString());
        Assert.Null(response.Headers.Location);
        Assert.False(response.Headers.Contains("Set-Cookie"));

        using HttpResponseMessage other = await client.GetAsync($"{pathBase}/other", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, other.StatusCode);
    }

    [Theory]
    [InlineData("GET", "/metadata.json?version=1", 200)]
    [InlineData("HEAD", "/metadata.json?version=1", 200)]
    [InlineData("POST", "/metadata.json?version=1", 405)]
    [InlineData("GET", "/metadata.json", 401)]
    [InlineData("GET", "/metadata.json?version=2", 401)]
    [InlineData("GET", "/Metadata.json?version=1", 401)]
    public async Task MatchesQueryAndHandlesMethods(string method, string path, int statusCode)
    {
        using IHost host = await CreateHost("https://app.example.com/metadata.json?version=1");
        using HttpClient client = host.GetTestClient();
        using HttpRequestMessage request = new(new HttpMethod(method), path);
        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        Assert.Equal(statusCode, (int)response.StatusCode);
        if (method == "HEAD" || method == "POST")
        {
            Assert.Empty(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        }

        if (method == "HEAD")
        {
            Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
            Assert.True(response.Content.Headers.ContentLength > 0);
        }

        if (method == "POST")
        {
            Assert.Equal(["GET", "HEAD"], response.Content.Headers.Allow);
        }
    }

    [Fact]
    public async Task InvalidConfigurationFailsWhenBuildingThePipeline()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => CreateHost("http://localhost"));
    }

    [Fact]
    public void RequiresOAuthConfiguration()
    {
        ServiceCollection services = new();
        services.AddOptions<BlueskyAgentOptions>();
        services.AddBlueskyOAuthClientMetadata();
        using ServiceProvider provider = services.BuildServiceProvider();
        ApplicationBuilder app = new(provider);
        Assert.Throws<InvalidOperationException>(() => app.UseBlueskyOAuthClientMetadata());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DuplicateSigningKeyIdsFailGenerationAndPipelineStartup(bool duplicateActiveKey)
    {
        OAuthOptions options = new(ClientId, new Uri(Callback))
        {
            ClientSigningKey = CreateSigningKey("active")
        };
        options.AdditionalClientSigningKeys.Add(CreateSigningKey("previous"));
        options.AdditionalClientSigningKeys.Add(CreateSigningKey(duplicateActiveKey ? "active" : "previous"));

        Assert.Throws<ArgumentException>(() => new BlueskyOAuthClientMetadataOptions().GenerateJson(options));

        ServiceCollection services = new();
        services.Configure<BlueskyAgentOptions>(agentOptions => agentOptions.OAuthOptions = options);
        services.AddBlueskyOAuthClientMetadata();
        using ServiceProvider provider = services.BuildServiceProvider();
        ApplicationBuilder app = new(provider);

        Assert.Throws<ArgumentException>(() => app.UseBlueskyOAuthClientMetadata());
    }

    [Theory]
    [InlineData(true, 200)]
    [InlineData(false, 302)]
    public async Task PublicationIsOptInAndBypassesAnAuthenticatedFallbackPolicy(bool publishMetadata, int statusCode)
    {
        using IHost host = await new HostBuilder().ConfigureWebHost(webHost => webHost
            .UseTestServer()
            .ConfigureServices(services =>
            {
                services.AddRouting();
                services.AddAuthentication(BlueskyAuthenticationDefaults.AuthenticationScheme).AddBluesky();
                services.AddAuthorization(options =>
                    options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());
                services.Configure<BlueskyAgentOptions>(options =>
                    options.OAuthOptions = new OAuthOptions(ClientId, new Uri(Callback)));
                services.AddBlueskyOAuthClientMetadata();
            })
            .Configure(app =>
            {
                if (publishMetadata)
                {
                    app.UseBlueskyOAuthClientMetadata();
                }

                app.UseRouting();
                app.UseAuthentication();
                app.UseAuthorization();
                app.UseEndpoints(endpoints => endpoints.MapGet("/oauth-client-metadata.json", () => "Not metadata"));
            })).StartAsync(TestContext.Current.CancellationToken);

        using HttpClient client = host.GetTestClient();
        using HttpResponseMessage response = await client.GetAsync("/oauth-client-metadata.json", TestContext.Current.CancellationToken);
        Assert.Equal(statusCode, (int)response.StatusCode);
        if (publishMetadata)
        {
            using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
            Assert.Equal(ClientId, document.RootElement.GetProperty("client_id").GetString());
        }
    }

    private static Task<IHost> CreateHost(string clientId, string pathBase = "")
    {
        return new HostBuilder().ConfigureWebHost(webHost => webHost
            .UseTestServer()
            .ConfigureServices(services =>
            {
                services.Configure<BlueskyAgentOptions>(options =>
                    options.OAuthOptions = new OAuthOptions(clientId, new Uri(Callback)));
                services.AddBlueskyOAuthClientMetadata();
            })
            .Configure(app =>
            {
                if (!string.IsNullOrEmpty(pathBase))
                {
                    app.UsePathBase(pathBase);
                }

                app.UseBlueskyOAuthClientMetadata();
                app.Run(context =>
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return Task.CompletedTask;
                });
            })).StartAsync();
    }
}
