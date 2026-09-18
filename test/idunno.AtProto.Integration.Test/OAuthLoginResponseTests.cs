// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;

using idunno.AtProto.Authentication;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace idunno.AtProto.Integration.Test;

[ExcludeFromCodeCoverage]
public class OAuthLoginResponseTests
{
    private const string DomainName = "login.test.internal";
    private const string ServerDid = $"did:web:{DomainName}";
    private const string Authority = $"https://{DomainName}/";
    private const string AccountDid = "did:plc:loginaccount";
    private const string ClientId = "https://client.test/clientMetadata.json";

    private static readonly Uri s_authority = new(Authority);
    private static readonly Uri s_service = new($"https://{DomainName}");
    private static readonly Uri s_returnUri = new("https://client.test/callback");

    private static readonly string s_signingKeyJson = CreatePublicSigningKeyJson();

    [Fact]
    public async Task ASuccessfullyProcessedLoginResponseDiscardsTheLoginState()
    {
        LoginTestServer server = new();
        OAuthClient client = CreateClient(server);

        await client.BuildOAuth2LoginUri(
            service: s_service,
            authority: s_authority,
            returnUri: s_returnUri,
            clientId: ClientId,
            cancellationToken: TestContext.Current.CancellationToken);

        DPoPAccessCredentials? credentials = await client.ProcessOAuth2LoginResponse(
            CallbackData(client.State!.State),
            clientId: ClientId,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.NotNull(credentials);

        // The authorize state carries a single use code verifier which the server has now spent, and the proof key is a
        // private key. Neither has any further use, and both are readable through State until they are discarded.
        Assert.Null(client.State);
    }

    [Fact]
    public async Task ALoginResponseWhichFailsValidationDiscardsTheLoginState()
    {
        LoginTestServer server = new() { IssueBearerToken = true };
        OAuthClient client = CreateClient(server);

        await client.BuildOAuth2LoginUri(
            service: s_service,
            authority: s_authority,
            returnUri: s_returnUri,
            clientId: ClientId,
            cancellationToken: TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<OAuthException>(
            () => client.ProcessOAuth2LoginResponse(
                CallbackData(client.State!.State),
                clientId: ClientId,
                cancellationToken: TestContext.Current.CancellationToken));

        Assert.Null(client.State);
    }

    [Fact]
    public async Task ProcessingARestoredLoginSignsTheTokenRequestWithThatLoginsProofKey()
    {
        LoginTestServer server = new();

        // A client which has already prepared a login of its own, so it is holding an OidcClient configured with that
        // login's proof key.
        OAuthClient client = CreateClient(server);
        await client.BuildOAuth2LoginUri(
            service: s_service,
            authority: s_authority,
            returnUri: s_returnUri,
            clientId: ClientId,
            cancellationToken: TestContext.Current.CancellationToken);

        string abandonedProofKey = client.State!.ProofKey;

        // A second, genuine login prepared elsewhere, as an application which restores saved state would supply.
        OAuthClient other = CreateClient(server);
        await other.BuildOAuth2LoginUri(
            service: s_service,
            authority: s_authority,
            returnUri: s_returnUri,
            clientId: ClientId,
            cancellationToken: TestContext.Current.CancellationToken);

        OAuthLoginState restoredState = other.State!;
        Assert.NotEqual(abandonedProofKey, restoredState.ProofKey);

        server.ClearRecordedProofKeyThumbprints();

        DPoPAccessCredentials? credentials = await client.ProcessOAuth2Response(
            restoredState,
            CallbackData(restoredState.State),
            TestContext.Current.CancellationToken);

        Assert.NotNull(credentials);

        // The token the authorization server issues is bound to the key the token request was signed with, so the
        // credentials would carry a proof key the token was never bound to had the abandoned login's client been reused.
        Assert.Equal(restoredState.ProofKey, credentials.DPoPProofKey);
        Assert.Equal(Thumbprint(restoredState.ProofKey), server.TokenRequestProofKeyThumbprint);
        Assert.NotEqual(Thumbprint(abandonedProofKey), server.TokenRequestProofKeyThumbprint);
    }

    [Fact]
    public async Task BuildingALogoutUriDoesNotCarryTheAccessTokenAndLeavesTheLoginStateIntact()
    {
        LoginTestServer server = new();
        OAuthClient client = CreateClient(server);

        await client.BuildOAuth2LoginUri(
            service: s_service,
            authority: s_authority,
            returnUri: s_returnUri,
            clientId: ClientId,
            cancellationToken: TestContext.Current.CancellationToken);

        OAuthLoginState stateBeforeLogout = client.State!;

        string accessJwt = CreateAccessJwt(new Did(AccountDid));

        DPoPAccessCredentials credentials = new(
            service: s_service,
            accessJwt: accessJwt,
            refreshToken: "refresh",
            dPoPProofKey: JwtBuilder.CreateProofKey(),
            dPoPNonce: "nonce");

        Uri logoutUri = await client.BuildOAuth2LogoutUri(
            credentials,
            authority: s_authority,
            clientId: ClientId,
            returnUri: s_returnUri,
            cancellationToken: TestContext.Current.CancellationToken);

        // The logout address is handed to a browser, so a token in it reaches the address bar, the history and any
        // Referer header the logout page goes on to send.
        Assert.DoesNotContain(accessJwt, logoutUri.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("id_token_hint", logoutUri.ToString(), StringComparison.OrdinalIgnoreCase);

        // Building a logout address is not a login, so it must not have replaced the proof key of the login in progress.
        Assert.Equal(stateBeforeLogout, client.State);
    }

    private static string CallbackData(string state) => $"?code=authorizationCode&state={state}";

    private static OAuthClient CreateClient(LoginTestServer server) =>
        new(httpClientConfigurator: httpClient => httpClient,
            innerHandlerFactory: server.TestServer.CreateHandler,
            loggerFactory: null,
            options: new OAuthOptions(ClientId, s_returnUri));

    private static string CreateAccessJwt(Did did) =>
        JwtBuilder.CreateJwt(did, issuer: Authority, audience: ServerDid, scope: "atproto");

    /// <summary>
    /// Gets the thumbprint of the public part of the specified <paramref name="proofKey"/>.
    /// </summary>
    private static string Thumbprint(string proofKey)
    {
        JsonWebKey key = new(proofKey);

        return Base64UrlEncoder.Encode(key.ComputeJwkThumbprint());
    }

    private static string CreatePublicSigningKeyJson()
    {
        using RSA rsa = RSA.Create(2048);

        RSAParameters parameters = rsa.ExportParameters(false);

        string modulus = Base64UrlEncoder.Encode(parameters.Modulus!);
        string exponent = Base64UrlEncoder.Encode(parameters.Exponent!);

        return $$"""{"kty":"RSA","use":"sig","alg":"RS256","kid":"test","n":"{{modulus}}","e":"{{exponent}}"}""";
    }

    /// <summary>
    /// A test server which serves enough of an authorization server and a personal data server for an OAuth login to
    /// be prepared and its response processed, recording the proof key each token request was signed with.
    /// </summary>
    private sealed class LoginTestServer
    {
        private readonly List<string> _tokenRequestProofKeyThumbprints = [];
        private readonly object _lock = new();

        internal LoginTestServer()
        {
            TestServer = TestServerBuilder.CreateServer(DomainName, Handle);
        }

        internal TestServer TestServer { get; }

        /// <summary>
        /// Gets or sets a value indicating whether the token endpoint issues a bearer token rather than a DPoP bound one.
        /// </summary>
        internal bool IssueBearerToken { get; set; }

        internal string? TokenRequestProofKeyThumbprint
        {
            get
            {
                lock (_lock)
                {
                    return _tokenRequestProofKeyThumbprints.Count == 0 ? null : _tokenRequestProofKeyThumbprints[^1];
                }
            }
        }

        internal void ClearRecordedProofKeyThumbprints()
        {
            lock (_lock)
            {
                _tokenRequestProofKeyThumbprints.Clear();
            }
        }

        private async Task Handle(HttpContext context)
        {
            HttpRequest request = context.Request;
            HttpResponse response = context.Response;

            switch (request.Path)
            {
                case "/.well-known/oauth-authorization-server":
                    response.ContentType = "application/json";
                    await response.WriteAsync($$"""
                        {
                          "issuer": "https://{{DomainName}}",
                          "authorization_endpoint": "https://{{DomainName}}/authorize",
                          "token_endpoint": "https://{{DomainName}}/token",
                          "pushed_authorization_request_endpoint": "https://{{DomainName}}/par",
                          "end_session_endpoint": "https://{{DomainName}}/logout",
                          "jwks_uri": "https://{{DomainName}}/jwks",
                          "response_types_supported": [ "code" ],
                          "grant_types_supported": [ "authorization_code", "refresh_token" ],
                          "code_challenge_methods_supported": [ "S256" ]
                        }
                        """);
                    return;

                case "/jwks":
                    response.ContentType = "application/json";
                    await response.WriteAsync($$"""{"keys":[{{s_signingKeyJson}}]}""");
                    return;

                case "/par" when request.Method == HttpMethod.Post.Method:
                    response.StatusCode = StatusCodes.Status201Created;
                    response.ContentType = "application/json";
                    await response.WriteAsync("""{"request_uri":"urn:ietf:params:oauth:request_uri:test","expires_in":90}""");
                    return;

                case "/token" when request.Method == HttpMethod.Post.Method:
                    RecordProofKey(request);

                    response.Headers["DPoP-Nonce"] = "serverIssuedNonce";
                    response.ContentType = "application/json";
                    await response.WriteAsync($$"""
                        {
                          "access_token": "{{CreateAccessJwt(new Did(AccountDid))}}",
                          "token_type": "{{(IssueBearerToken ? "Bearer" : "DPoP")}}",
                          "expires_in": 900,
                          "refresh_token": "refreshToken",
                          "scope": "atproto"
                        }
                        """);
                    return;

                case "/xrpc/com.atproto.server.describeServer":
                    response.ContentType = "application/json";
                    await response.WriteAsync($$"""{"did":"{{ServerDid}}","availableUserDomains":[".{{DomainName}}"]}""");
                    return;

                default:
                    response.StatusCode = StatusCodes.Status404NotFound;
                    return;
            }
        }

        private void RecordProofKey(HttpRequest request)
        {
            if (!request.Headers.TryGetValue("DPoP", out Microsoft.Extensions.Primitives.StringValues proofValues))
            {
                return;
            }

            JsonWebToken proof = new(proofValues.ToString());

            if (!proof.TryGetHeaderValue("jwk", out System.Text.Json.JsonElement jwk))
            {
                return;
            }

            JsonWebKey key = new(jwk.GetRawText());

            lock (_lock)
            {
                _tokenRequestProofKeyThumbprints.Add(Base64UrlEncoder.Encode(key.ComputeJwkThumbprint()));
            }
        }
    }
}
