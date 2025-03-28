// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;
using System.Text.Json.Nodes;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;

using Duende.IdentityModel.OidcClient.DPoP;

using idunno.AtProto.Authentication;
using idunno.AtProto.Authentication.Models;
using idunno.AtProto.Repo;
using idunno.AtProto.Repo.Models;

namespace idunno.AtProto.Integration.Test
{
    public class AuthenticationTests
    {
        private readonly JsonSerializerOptions _jsonSerializerOptions;

        public AuthenticationTests()
        {
            _jsonSerializerOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
            _jsonSerializerOptions.TypeInfoResolverChain.Insert(0, SourceGenerationContext.Default);
            _jsonSerializerOptions.TypeInfoResolverChain.Insert(0, AtProto.SourceGenerationContext.Default);
        }

        [Fact]
        public async Task CreateSessionSendsCorrectRequestParsesResponseAndSetsAgentCredentialsAndDid()
        {
            const string domainName = "test.invalid";
            const string userIdentifier = domainName;
            const string expectedDid = "did:plc:ec72yg6n2sydzjvtovvdlxrk";
            const string expectedRefreshToken = "refreshToken";

            string expectedJwt = JwtBuilder.CreateJwt(expectedDid, $"did:web:{domainName}");

            string? sentIdentifer;
            string? sentPassword;
            string? authFactorToken;

            TestServer testServer = TestServerBuilder.CreateServer(domainName, async context =>
            {
                HttpRequest request = context.Request;
                HttpResponse response = context.Response;

                if (request.Path == "/.well-known/atproto-did")
                {
                    response.StatusCode = 200;
                    response.ContentType = "text/plain";
                    await response.WriteAsync(expectedDid);
                }
                else if (request.Path == $"/{expectedDid}")
                {
                    response.StatusCode = 200;
                    DidDocument didDocument = new(
                        id: $"did:web:{domainName}",
                        context: ["https://www.w3.org/ns/did/v1"],
                        alsoKnownAs: null,
                        verificationMethods: null,
                        services:
                        [
                            new(
                                id : "#atproto_pds",
                                type : "atprotopds",
                                serviceEndpoint : new Uri($"https://{domainName}")
                                )
                        ]);
                    await response.WriteAsJsonAsync(didDocument, _jsonSerializerOptions);
                }
                else if (request.Path == "/xrpc/com.atproto.server.createSession" && request.Method == HttpMethod.Post.Method)
                {
                    string requestBody = await new StreamReader(request.Body).ReadToEndAsync();

                    CreateSessionRequest? createSessionRequest = JsonSerializer.Deserialize<CreateSessionRequest>(requestBody, _jsonSerializerOptions);

                    if (createSessionRequest is null)
                    {
                        response.StatusCode = 400;
                        return;
                    }

                    if (createSessionRequest.Identifier != userIdentifier)
                    {
                        response.StatusCode = 400;
                        var errorResponse = new AtErrorDetail()
                        {
                            Error = "InvalidIdentifier",
                            Message = $"Invalid identifier {createSessionRequest.Identifier}."
                        };
                        await response.WriteAsJsonAsync(errorResponse);
                    }

                    response.StatusCode = 200;

                    sentIdentifer = createSessionRequest.Identifier;
                    sentPassword = createSessionRequest.Password;
                    authFactorToken = createSessionRequest.AuthFactorToken;

                    var createSessionResponse = new CreateSessionResponse(
                        accessJwt: expectedJwt,
                        refreshJwt: expectedRefreshToken,
                        handle: userIdentifier,
                        did: expectedDid);

                    await response.WriteAsJsonAsync(createSessionResponse, _jsonSerializerOptions);
                }
            });

            using (var agent = new AtProtoAgent(
                new Uri($"https://{domainName}"),
                new TestHttpClientFactory(testServer),
                new AtProtoAgentOptions()
                {
                    PlcDirectoryServer = new Uri($"https://{domainName}")
                }))
            {
                AtProtoHttpResult<bool> loginResult = await agent.Login(
                    identifier: userIdentifier,
                    password: "password",
                    authFactorToken: null,
                    cancellationToken: TestContext.Current.CancellationToken);

                Assert.True(loginResult.Succeeded);
                Assert.True(loginResult.Result);
                Assert.True(agent.IsAuthenticated);
                Assert.Equal(expectedDid, agent.Did);
            }
        }

        [Fact]
        public async Task ApiEndpointRotatingDPoPNonceInA200ResponseUpdatesCredentialsAndRaisesEvent()
        {
            int callCount = 0;
            bool cycleDPopNonce = true;

            Uri server = new("https://test.invalid");
            const string endpoint = "/xrpc/com.atproto.repo.createRecord";

            const string repo = "did:plc:identifier";
            const string collection = "test.idunno.lexiconType";

            TestServer testServer = TestServerBuilder.CreateServer(server, async context =>
            {
                HttpRequest request = context.Request;
                HttpResponse response = context.Response;

                if (request.Headers.Authorization.Count == 0)
                {
                    response.StatusCode = 401;
                    return;
                }
                else if (request.Path == endpoint && request.Body is not null)
                {
                    JsonNode? requestJson = await JsonSerializer.DeserializeAsync<JsonNode>(request.Body);

                    if (requestJson is null)
                    {
                        response.StatusCode = 400;
                        return;
                    }

                    if ((string?)requestJson["repo"] != repo)
                    {
                        response.StatusCode = 401;
                        var errorResponse = new AtErrorDetail()
                        {
                            Error = "NotAuthorized"
                        };
                        await response.WriteAsJsonAsync(errorResponse);
                        return;
                    }

                    if ((string?)requestJson["collection"] != collection)
                    {
                        response.StatusCode = 400;
                        var errorResponse = new AtErrorDetail()
                        {
                            Error = "InvalidCollection",
                            Message = $"Invalid collection {(string?)requestJson["repo"]}."
                        };
                        await response.WriteAsJsonAsync(errorResponse);
                        return;
                    }

                    callCount++;

                    if (cycleDPopNonce)
                    {
                        // Simulate a DPoP nonce rotation
                        response.Headers.Append("DPoP-Nonce", "newNonce");
                        cycleDPopNonce = false;
                    }

                    response.StatusCode = 200;
                    response.ContentType = "application/json";

                    var createRecordResponse = new CreateRecordResponse(
                        uri: new($"at://{(string?)requestJson["repo"]}/{(string ?)requestJson["collection"]}/rkey"),
                        cid: new("bafyreihd3v4j"),
                        commit: null,
                        validationStatus: ValidationStatus.Valid);

                    await response.WriteAsJsonAsync(createRecordResponse, _jsonSerializerOptions);
                    return;
                }
            });

            using (AtProtoAgent agent = new(server, new TestHttpClientFactory(testServer)))
            {
                bool credentialsUpdatedEventRaised = false;
                string? credentialEventNonce = null;

                agent.CredentialsUpdated += (sender, args) =>
                {
                    credentialsUpdatedEventRaised = true;

                    if (args.AccessCredentials is DPoPAccessCredentials dPopAccessCredentials)
                    {
                        credentialEventNonce = dPopAccessCredentials.DPoPNonce;
                    }
                };

                var credential = AtProtoCredential.Create(
                    server,
                    authenticationType: AuthenticationType.OAuth,
                    accessJwt: JwtBuilder.CreateJwt(new Did("did:plc:identifier")),
                    refreshToken: "refreshToken",
                    dPoPProofKey: JsonWebKeys.CreateRsaJson(),
                    dPoPNonce: "nonce");

                agent.Credentials = (DPoPAccessCredentials)credential;

                TestRecordValue recordValue = new() { TestValue = "test" };

                _ = await agent.CreateRecord(
                    recordValue,
                    collection,
                    cancellationToken: TestContext.Current.CancellationToken);

                DPoPAccessCredentials credentials = (DPoPAccessCredentials)agent.Credentials;

                Assert.Equal("newNonce", credentials.DPoPNonce);
                Assert.Equal(1, callCount);

                Assert.True(credentialsUpdatedEventRaised);

                Assert.NotNull(credentialEventNonce);
                Assert.Equal("newNonce", credentialEventNonce);
            }
        }

        [Fact]
        public async Task ApiEndpointRotatingDPoPNonceInA400ResponseUpdatesCredentialsAndRaisesEventAndRetries()
        {
            int callCount = 0;
            bool returnBadRequest = true;

            Uri server = new("https://test.invalid");
            const string endpoint = "/xrpc/com.atproto.repo.createRecord";

            const string repo = "did:plc:identifier";
            const string collection = "test.idunno.lexiconType";

            TestServer testServer = TestServerBuilder.CreateServer(server, async context =>
            {
                HttpRequest request = context.Request;
                HttpResponse response = context.Response;

                if (request.Headers.Authorization.Count == 0)
                {
                    response.StatusCode = 401;
                    return;
                }
                else if (request.Path == endpoint && request.Body is not null)
                {
                    JsonNode? requestJson = await JsonSerializer.DeserializeAsync<JsonNode>(request.Body);

                    if (requestJson is null)
                    {
                        response.StatusCode = 400;
                        return;
                    }

                    if ((string?)requestJson["repo"] != repo)
                    {
                        response.StatusCode = 401;
                        var errorResponse = new AtErrorDetail()
                        {
                            Error = "NotAuthorized"
                        };
                        await response.WriteAsJsonAsync(errorResponse);
                        return;
                    }

                    if ((string?)requestJson["collection"] != collection)
                    {
                        response.StatusCode = 400;
                        var errorResponse = new AtErrorDetail()
                        {
                            Error = "InvalidCollection",
                            Message = $"Invalid collection {(string?)requestJson["repo"]}."
                        };
                        await response.WriteAsJsonAsync(errorResponse);
                        return;
                    }

                    callCount++;

                    if (returnBadRequest)
                    {
                        // Simulate a DPoP nonce rotation in a bad request
                        response.StatusCode = 400;
                        response.ContentType = "application/json";
                        string responseBody = " {\r\n  \"error\": \"use_dpop_nonce\",\r\n  \"error_description\":\r\n    \"Authorization server requires nonce in DPoP proof\"\r\n }";
                        response.Headers.Append("DPoP-Nonce", "newNonce");
                        await response.WriteAsync(responseBody);
                        return;
                    }

                    response.StatusCode = 200;
                    response.ContentType = "application/json";

                    var createRecordResponse = new CreateRecordResponse(
                        uri: new($"at://{(string?)requestJson["repo"]}/{(string?)requestJson["collection"]}/rkey"),
                        cid: new("bafyreihd3v4j"),
                        commit: null,
                        validationStatus: ValidationStatus.Valid);

                    await response.WriteAsJsonAsync(createRecordResponse, _jsonSerializerOptions);
                    return;
                }
            });

            using (AtProtoAgent agent = new(server, new TestHttpClientFactory(testServer)))
            {
                bool credentialsUpdatedEventRaised = false;
                string? credentialEventNonce = null;

                agent.CredentialsUpdated += (sender, args) =>
                {
                    credentialsUpdatedEventRaised = true;

                    if (args.AccessCredentials is DPoPAccessCredentials dPopAccessCredentials)
                    {
                        credentialEventNonce = dPopAccessCredentials.DPoPNonce;
                    }
                };

                var credential = AtProtoCredential.Create(
                    server,
                    authenticationType: AuthenticationType.OAuth,
                    accessJwt: JwtBuilder.CreateJwt(new Did("did:plc:identifier")),
                    refreshToken: "refreshToken",
                    dPoPProofKey: JsonWebKeys.CreateRsaJson(),
                    dPoPNonce: "nonce");

                agent.Credentials = (DPoPAccessCredentials)credential;

                TestRecordValue recordValue = new() { TestValue = "test" };

                _ = await agent.CreateRecord(
                    recordValue,
                    collection,
                    cancellationToken: TestContext.Current.CancellationToken);

                DPoPAccessCredentials credentials = (DPoPAccessCredentials)agent.Credentials;

                Assert.Equal("newNonce", credentials.DPoPNonce);
                Assert.Equal(2, callCount);

                Assert.True(credentialsUpdatedEventRaised);

                Assert.NotNull(credentialEventNonce);
                Assert.Equal("newNonce", credentialEventNonce);
            }
        }
    }
}
