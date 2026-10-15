// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Collections.ObjectModel;
using System.Net;
using System.Text.Json;

using Duende.IdentityModel.OidcClient.DPoP;

using idunno.AtProto;
using idunno.AtProto.Authentication;
using idunno.Bluesky.Actor;
using idunno.Bluesky.Contact;
using idunno.Bluesky.Contact.Model;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Logging;

namespace idunno.Bluesky.Integration.Test;

[ExcludeFromCodeCoverage]
public class ContactDiscoveryTests
{
    private const string ProfileJson = """
        {"did":"did:plc:test","handle":"test.invalid","associated":{"chat":{"allowIncoming":"all","allowGroupInvites":"future-policy"}}}
        """;

    public static TheoryData<string, bool> Endpoints => new(
        from endpoint in new[] { "startPhoneVerification", "verifyPhone", "importContacts", "dismissMatch", "removeData", "getMatches", "getSyncStatus" }
        from useAgent in new[] { false, true }
        select (endpoint, useAgent));

    public static TheoryData<string, bool, string> Errors
    {
        get
        {
            TheoryData<string, bool, string> data = [];
            foreach (TheoryDataRow<string, bool> row in Endpoints)
            {
                (string endpoint, bool useAgent) = row.Data;
                string[] errors = endpoint switch
                {
                    "startPhoneVerification" => ["RateLimitExceeded", "InvalidDid", "InvalidPhone", "InternalError"],
                    "verifyPhone" => ["RateLimitExceeded", "InvalidDid", "InvalidPhone", "InvalidCode", "InternalError"],
                    "importContacts" => ["InvalidDid", "InvalidContacts", "TooManyContacts", "InvalidToken", "InternalError"],
                    "getMatches" => ["InvalidDid", "InvalidLimit", "InvalidCursor", "InternalError"],
                    _ => ["InvalidDid", "InternalError"]
                };
                foreach (string error in errors)
                {
                    data.Add(endpoint, useAgent, error);
                }
            }

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(Endpoints))]
    public async Task EndpointsUseAuthenticatedPdsRoutingAndSourceGeneratedBodies(string endpoint, bool useAgent)
    {
        string response = endpoint switch
        {
            "verifyPhone" => """{"token":"single-use-test-token"}""",
            "importContacts" => $$"""{"matchesAndContactIndexes":[null,{"match":{{ProfileJson}},"contactIndex":2},null]}""",
            "getMatches" => $$"""{"matches":[null,{{ProfileJson}},null],"cursor":"next&cursor"}""",
            "getSyncStatus" => """{"syncStatus":{"syncedAt":"2026-01-01T00:00:00Z","matchesCount":3}}""",
            _ => "{}"
        };
        using TestServer server = TestServerBuilder.CreateServer(TestServerBuilder.DefaultUri, async context =>
        {
            Assert.Equal("pds.example.com", context.Request.Host.Host);
            Assert.Equal($"/xrpc/app.bsky.contact.{endpoint}", context.Request.Path.ToString());
            Assert.Equal("did:web:api.bsky.app#bsky_appview", context.Request.Headers["atproto-proxy"]);
            Assert.StartsWith("Bearer ", context.Request.Headers.Authorization.ToString(), StringComparison.Ordinal);
            Assert.Equal(endpoint.StartsWith("get", StringComparison.Ordinal) ? "GET" : "POST", context.Request.Method);
            if (endpoint == "getMatches")
            {
                Assert.Equal("2", context.Request.Query["limit"]);
                Assert.Equal("older&cursor=1", context.Request.Query["cursor"]);
            }
            else
            {
                Assert.Empty(context.Request.Query);
            }

            if (context.Request.Method == "POST")
            {
                Assert.StartsWith("application/json", context.Request.ContentType, StringComparison.Ordinal);
                using JsonDocument body = await JsonDocument.ParseAsync(context.Request.Body, cancellationToken: TestContext.Current.CancellationToken);
                JsonElement root = body.RootElement;
                switch (endpoint)
                {
                    case "startPhoneVerification":
                        Assert.Equal("+12125550123", root.GetProperty("phone").GetString());
                        Assert.Single(root.EnumerateObject());
                        break;
                    case "verifyPhone":
                        Assert.Equal("+12125550123", root.GetProperty("phone").GetString());
                        Assert.Equal("012345", root.GetProperty("code").GetString());
                        Assert.Equal(2, root.EnumerateObject().Count());
                        break;
                    case "importContacts":
                        Assert.Equal("single-use-test-token", root.GetProperty("token").GetString());
                        Assert.Equal(new[] { "+12125550124", "", "+12125550124" }, root.GetProperty("contacts").EnumerateArray().Select(item => item.GetString()).ToArray());
                        Assert.Equal(2, root.EnumerateObject().Count());
                        break;
                    case "dismissMatch":
                        Assert.Equal("did:plc:test", root.GetProperty("subject").GetString());
                        Assert.Single(root.EnumerateObject());
                        break;
                    default:
                        Assert.Empty(root.EnumerateObject());
                        break;
                }
            }

            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(response, TestContext.Current.CancellationToken);
        });
        using HttpClient client = server.CreateClient();
        using BlueskyAgent agent = CreateAgent(server);
        Response result = await Invoke(endpoint, useAgent, agent, client);

        Assert.True(result.Succeeded);
        switch (endpoint)
        {
            case "verifyPhone":
                PhoneVerificationResult verification = Assert.IsType<PhoneVerificationResult>(result.Value);
                Assert.Equal("single-use-test-token", verification.Token);
                Assert.DoesNotContain(verification.Token, verification.ToString(), StringComparison.Ordinal);
                break;
            case "importContacts":
                ContactMatch match = Assert.Single(Assert.IsType<ReadOnlyCollection<ContactMatch>>(result.Value));
                Assert.Equal(2, match.ContactIndex);
                Assert.Equal("future-policy", match.Match.Associated?.Chat?.AllowGroupInvites);
                break;
            case "getMatches":
                var matches = Assert.IsType<PagedViewReadOnlyCollection<ProfileView>>(result.Value);
                Assert.Equal("next&cursor", matches.Cursor);
                Assert.Equal("future-policy", Assert.Single(matches).Associated?.Chat?.AllowGroupInvites);
                break;
            case "getSyncStatus":
                ContactSyncStatus? status = Assert.IsType<ContactSyncStatusResult>(result.Value).SyncStatus;
                Assert.NotNull(status);
                Assert.Equal(3, status.MatchesCount);
                Assert.Equal(DateTimeOffset.Parse("2026-01-01T00:00:00Z", System.Globalization.CultureInfo.InvariantCulture), status.SyncedAt);
                break;
            default:
                Assert.IsType<EmptyResponse>(result.Value);
                break;
        }
    }

    [Theory]
    [MemberData(nameof(Errors))]
    public async Task NamedErrorsSurfaceThroughAtErrorDetail(string endpoint, bool useAgent, string error)
    {
        using TestServer server = TestServerBuilder.CreateServer(TestServerBuilder.DefaultUri, async context =>
        {
            context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync($$"""{"error":"{{error}}","message":"Synthetic contact error."}""", TestContext.Current.CancellationToken);
        });
        using HttpClient client = server.CreateClient();
        using BlueskyAgent agent = CreateAgent(server);

        Response result = await Invoke(endpoint, useAgent, agent, client);

        Assert.False(result.Succeeded);
        Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode);
        Assert.Equal(error, result.Error?.Error);
        Assert.Null(result.Value);
    }

    [Theory]
    [MemberData(nameof(Endpoints))]
    public async Task AgentsRequireAuthentication(string endpoint, bool _)
    {
        using BlueskyAgent agent = new();
        using HttpClient client = new();

        await Assert.ThrowsAsync<AuthenticationRequiredException>(() => Invoke(endpoint, true, agent, client));
    }

    [Theory]
    [MemberData(nameof(Endpoints))]
    public async Task EndpointsForwardTheConfiguredLoggerFactory(string endpoint, bool useAgent)
    {
        using ILoggerFactory loggerFactory = LoggerFactory.Create(_ => { });
        using TestServer server = TestServerBuilder.CreateServer(TestServerBuilder.DefaultUri, _ => Task.CompletedTask);
        using HttpClient client = server.CreateClient();
        using BlueskyAgent agent = CreateAgent(server, loggerFactory);
        loggerFactory.Dispose();

        await Assert.ThrowsAsync<ObjectDisposedException>(() => Invoke(endpoint, useAgent, agent, client, loggerFactory));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("a&b=1/? +")]
    public async Task MatchQueriesPreserveOptionalAndEscapedCursors(string? cursor)
    {
        using TestServer server = TestServerBuilder.CreateServer(TestServerBuilder.DefaultUri, async context =>
        {
            Assert.False(context.Request.Query.ContainsKey("limit"));
            Assert.Equal(cursor is not null, context.Request.Query.ContainsKey("cursor"));
            if (cursor is not null)
            {
                Assert.Equal(cursor, context.Request.Query["cursor"]);
            }

            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync("""{"matches":[]}""", TestContext.Current.CancellationToken);
        });
        using BlueskyAgent agent = CreateAgent(server);

        var result = await agent.GetContactMatches(cursor: cursor, cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Empty(result.Result);
        Assert.Null(result.Result.Cursor);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("""{"syncStatus":null}""")]
    public async Task MissingSyncStatusIsNotInvented(string body)
    {
        using TestServer server = Respond(body);
        using BlueskyAgent agent = CreateAgent(server);

        var result = await agent.GetContactSyncStatus(cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Null(result.Result.SyncStatus);
    }

    [Theory]
    [InlineData("verifyPhone", "{}")]
    [InlineData("verifyPhone", """{"token":null}""")]
    [InlineData("getMatches", "{}")]
    [InlineData("getMatches", """{"matches":null}""")]
    [InlineData("importContacts", "{}")]
    [InlineData("importContacts", """{"matchesAndContactIndexes":null}""")]
    [InlineData("importContacts", """{"matchesAndContactIndexes":[{"match":null,"contactIndex":0}]}""")]
    [InlineData("importContacts", """{"matchesAndContactIndexes":[{"match":{"did":"did:plc:test","handle":"test.invalid"},"contactIndex":-1}]}""")]
    [InlineData("importContacts", """{"matchesAndContactIndexes":[{"match":{"did":"did:plc:test","handle":"test.invalid"},"contactIndex":1000}]}""")]
    [InlineData("importContacts", """{"matchesAndContactIndexes":[{"contactIndex":0}]}""")]
    [InlineData("importContacts", """{"matchesAndContactIndexes":[{"match":{"did":"did:plc:test","handle":"test.invalid"}}]}""")]
    [InlineData("getSyncStatus", """{"syncStatus":{"syncedAt":"2026-01-01T00:00:00Z","matchesCount":-1}}""")]
    [InlineData("getSyncStatus", """{"syncStatus":{"matchesCount":0}}""")]
    [InlineData("getSyncStatus", """{"syncStatus":{"syncedAt":"2026-01-01T00:00:00Z"}}""")]
    public async Task MalformedResponsesUseTheStandardInvalidResponseResult(string endpoint, string body)
    {
        using TestServer server = Respond(body);
        using HttpClient client = server.CreateClient();
        using BlueskyAgent agent = CreateAgent(server);

        Response result = await Invoke(endpoint, true, agent, client);

        Assert.False(result.Succeeded);
        Assert.Equal("InvalidResponse", result.Error?.Error);
    }

    [Theory]
    [InlineData(false, 0)]
    [InlineData(false, 101)]
    [InlineData(true, 0)]
    [InlineData(true, 101)]
    public async Task MatchLimitsAreValidatedOnBothSurfaces(bool useAgent, int limit)
    {
        using TestServer server = Respond("""{"matches":[]}""");
        using HttpClient client = server.CreateClient();
        using BlueskyAgent agent = CreateAgent(server);
        CancellationToken token = TestContext.Current.CancellationToken;

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => useAgent
            ? agent.GetContactMatches(limit: limit, cancellationToken: token)
            : BlueskyServer.GetContactMatches(limit, null, agent.Service, agent.Credentials!, client, cancellationToken: token));
    }

    [Theory]
    [InlineData(false, 0)]
    [InlineData(false, 1001)]
    [InlineData(true, 0)]
    [InlineData(true, 1001)]
    public async Task ImportBoundsAreValidatedOnBothSurfaces(bool useAgent, int count)
    {
        using TestServer server = Respond("{}");
        using HttpClient client = server.CreateClient();
        using BlueskyAgent agent = CreateAgent(server);
        string[] contacts = Enumerable.Repeat("+12125550124", count).ToArray();
        CancellationToken token = TestContext.Current.CancellationToken;

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => useAgent
            ? agent.ImportContacts("token", contacts, token)
            : BlueskyServer.ImportContacts("token", contacts, agent.Service, agent.Credentials!, client, cancellationToken: token));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NullContactEntriesAreRejectedButEmptyStringsRemainWireValues(bool useAgent)
    {
        using TestServer server = Respond("""{"matchesAndContactIndexes":[]}""");
        using HttpClient client = server.CreateClient();
        using BlueskyAgent agent = CreateAgent(server);
        CancellationToken token = TestContext.Current.CancellationToken;

        await Assert.ThrowsAsync<ArgumentException>(() => useAgent
            ? agent.ImportContacts("token", [null!], token)
            : BlueskyServer.ImportContacts("token", [null!], agent.Service, agent.Credentials!, client, cancellationToken: token));

        var result = useAgent
            ? await agent.ImportContacts("", [""], token)
            : await BlueskyServer.ImportContacts("", [""], agent.Service, agent.Credentials!, client, cancellationToken: token);
        Assert.True(result.Succeeded);
        Assert.Empty(result.Result);
    }

    [Theory]
    [InlineData(false, 1, 0)]
    [InlineData(true, 1, 0)]
    [InlineData(false, 1000, 999)]
    [InlineData(true, 1000, 999)]
    public async Task ImportAcceptsInclusiveCountAndIndexBounds(bool useAgent, int count, int index)
    {
        using TestServer server = TestServerBuilder.CreateServer(TestServerBuilder.DefaultUri, async context =>
        {
            using JsonDocument request = await JsonDocument.ParseAsync(context.Request.Body, cancellationToken: TestContext.Current.CancellationToken);
            Assert.Equal(count, request.RootElement.GetProperty("contacts").GetArrayLength());
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(
                $$"""{"matchesAndContactIndexes":[{"match":{{ProfileJson}},"contactIndex":{{index}}}]}""",
                TestContext.Current.CancellationToken);
        });
        using HttpClient client = server.CreateClient();
        using BlueskyAgent agent = CreateAgent(server);
        string[] contacts = Enumerable.Repeat("+12125550124", count).ToArray();
        CancellationToken token = TestContext.Current.CancellationToken;

        var result = useAgent
            ? await agent.ImportContacts("token", contacts, token)
            : await BlueskyServer.ImportContacts("token", contacts, agent.Service, agent.Credentials!, client, cancellationToken: token);

        Assert.True(result.Succeeded);
        Assert.Equal(index, Assert.Single(result.Result).ContactIndex);
    }

    [Theory]
    [InlineData(false, 1)]
    [InlineData(true, 1)]
    [InlineData(false, 100)]
    [InlineData(true, 100)]
    public async Task MatchQueriesAcceptInclusivePageBounds(bool useAgent, int limit)
    {
        using TestServer server = TestServerBuilder.CreateServer(TestServerBuilder.DefaultUri, async context =>
        {
            Assert.Equal(limit.ToString(System.Globalization.CultureInfo.InvariantCulture), context.Request.Query["limit"]);
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync("""{"matches":[]}""", TestContext.Current.CancellationToken);
        });
        using HttpClient client = server.CreateClient();
        using BlueskyAgent agent = CreateAgent(server);
        CancellationToken token = TestContext.Current.CancellationToken;

        var result = useAgent
            ? await agent.GetContactMatches(limit, cancellationToken: token)
            : await BlueskyServer.GetContactMatches(limit, null, agent.Service, agent.Credentials!, client, cancellationToken: token);

        Assert.True(result.Succeeded);
        Assert.Empty(result.Result);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RequiredEndpointArgumentsRejectNullOnBothSurfaces(bool useAgent)
    {
        using TestServer server = Respond("{}");
        using HttpClient client = server.CreateClient();
        using BlueskyAgent agent = CreateAgent(server);
        CancellationToken token = TestContext.Current.CancellationToken;

        await Assert.ThrowsAsync<ArgumentNullException>(() => useAgent
            ? agent.StartPhoneVerification(null!, token)
            : BlueskyServer.StartPhoneVerification(null!, agent.Service, agent.Credentials!, client, cancellationToken: token));
        await Assert.ThrowsAsync<ArgumentNullException>(() => useAgent
            ? agent.VerifyPhone(null!, "", token)
            : BlueskyServer.VerifyPhone(null!, "", agent.Service, agent.Credentials!, client, cancellationToken: token));
        await Assert.ThrowsAsync<ArgumentNullException>(() => useAgent
            ? agent.VerifyPhone("", null!, token)
            : BlueskyServer.VerifyPhone("", null!, agent.Service, agent.Credentials!, client, cancellationToken: token));
        await Assert.ThrowsAsync<ArgumentNullException>(() => useAgent
            ? agent.ImportContacts(null!, [""], token)
            : BlueskyServer.ImportContacts(null!, [""], agent.Service, agent.Credentials!, client, cancellationToken: token));
        await Assert.ThrowsAsync<ArgumentNullException>(() => useAgent
            ? agent.ImportContacts("", null!, token)
            : BlueskyServer.ImportContacts("", null!, agent.Service, agent.Credentials!, client, cancellationToken: token));
        await Assert.ThrowsAsync<ArgumentNullException>(() => useAgent
            ? agent.DismissContactMatch(null!, token)
            : BlueskyServer.DismissContactMatch(null!, agent.Service, agent.Credentials!, client, cancellationToken: token));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PhoneVerificationPreservesEmptyWireStrings(bool useAgent)
    {
        using TestServer server = TestServerBuilder.CreateServer(TestServerBuilder.DefaultUri, async context =>
        {
            using JsonDocument request = await JsonDocument.ParseAsync(context.Request.Body, cancellationToken: TestContext.Current.CancellationToken);
            Assert.Empty(request.RootElement.GetProperty("phone").GetString()!);
            if (context.Request.Path.ToString().EndsWith(".verifyPhone", StringComparison.Ordinal))
            {
                Assert.Empty(request.RootElement.GetProperty("code").GetString()!);
            }

            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync("""{"token":""}""", TestContext.Current.CancellationToken);
        });
        using HttpClient client = server.CreateClient();
        using BlueskyAgent agent = CreateAgent(server);
        CancellationToken token = TestContext.Current.CancellationToken;

        var start = useAgent
            ? await agent.StartPhoneVerification("", token)
            : await BlueskyServer.StartPhoneVerification("", agent.Service, agent.Credentials!, client, cancellationToken: token);
        var verify = useAgent
            ? await agent.VerifyPhone("", "", token)
            : await BlueskyServer.VerifyPhone("", "", agent.Service, agent.Credentials!, client, cancellationToken: token);

        Assert.True(start.Succeeded);
        Assert.True(verify.Succeeded);
        Assert.Empty(verify.Result.Token);
    }

    [Theory]
    [MemberData(nameof(Endpoints))]
    public async Task EndpointsForwardCancellation(string endpoint, bool useAgent)
    {
        using CancellationHandler handler = new();
        using HttpClient client = handler.CreateClient();
        using BlueskyAgent agent = CreateAgent(handler);
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Invoke(
            endpoint, useAgent, agent, client, cancellationOverride: cancellation.Token));
    }

    [Theory]
    [MemberData(nameof(Endpoints))]
    public async Task OversizedErrorResponsesRespectTheConfiguredSizeLimit(string endpoint, bool useAgent)
    {
        using TestServer server = TestServerBuilder.CreateServer(TestServerBuilder.DefaultUri, async context =>
        {
            context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync("""{"error":"InvalidDid","message":"Synthetic oversized response."}""", TestContext.Current.CancellationToken);
        });
        using HttpClient client = server.CreateClient();
        using BlueskyAgent agent = CreateAgent(server, maximumResponseSize: 1);
        Response result = await Invoke(endpoint, useAgent, agent, client, maximumResponseSize: 1);

        Assert.False(result.Succeeded);
        Assert.Null(result.Value);
        Assert.NotNull(result.Error);
        Assert.Equal("{", result.Error.RawContent);
    }

    [Theory]
    [InlineData("verifyPhone", false)]
    [InlineData("verifyPhone", true)]
    [InlineData("importContacts", false)]
    [InlineData("importContacts", true)]
    [InlineData("getMatches", false)]
    [InlineData("getMatches", true)]
    [InlineData("getSyncStatus", false)]
    [InlineData("getSyncStatus", true)]
    public async Task OversizedSuccessResponsesAreRejectedBeforeDeserialization(string endpoint, bool useAgent)
    {
        using TestServer server = Respond("""{"padding":"Synthetic oversized response."}""");
        using HttpClient client = server.CreateClient();
        using BlueskyAgent agent = CreateAgent(server, maximumResponseSize: 1);

        Response result = await Invoke(endpoint, useAgent, agent, client, maximumResponseSize: 1);

        Assert.False(result.Succeeded);
        Assert.Null(result.Value);
        Assert.Equal("ResponseTooLarge", result.Error?.Error);
    }

    [Theory]
    [MemberData(nameof(Endpoints))]
    public async Task EndpointsRejectNonpositiveServerResponseSize(string endpoint, bool _)
    {
        using TestServer server = Respond("{}");
        using HttpClient client = server.CreateClient();
        using BlueskyAgent agent = CreateAgent(server);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => Invoke(endpoint, false, agent, client, maximumResponseSize: 0));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => Invoke(endpoint, false, agent, client, maximumResponseSize: -1));
    }

    [Theory]
    [MemberData(nameof(Endpoints))]
    public async Task EndpointsForwardCredentialUpdates(string endpoint, bool useAgent)
    {
        string body = endpoint switch
        {
            "verifyPhone" => """{"token":"test-token"}""",
            "importContacts" => """{"matchesAndContactIndexes":[]}""",
            "getMatches" => """{"matches":[]}""",
            _ => "{}"
        };
        using TestServer server = TestServerBuilder.CreateServer(TestServerBuilder.DefaultUri, async context =>
        {
            context.Response.Headers.Append("DPoP-Nonce", "updatedNonce");
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(body, TestContext.Current.CancellationToken);
        });
        using HttpClient client = server.CreateClient();
        using BlueskyAgent agent = CreateAgent(server);
        agent.Credentials = (DPoPAccessCredentials)AtProtoCredential.Create(
            agent.Service,
            authenticationType: AuthenticationType.OAuth,
            accessJwt: JwtBuilder.CreateJwt(new Did("did:plc:test")),
            refreshToken: "refreshToken",
            dPoPProofKey: JsonWebKeys.CreateRsaJson(),
            dPoPNonce: "originalNonce");
        string? persistedNonce = null;
        Task Persist(AtProtoCredential credentials, CancellationToken _)
        {
            persistedNonce = Assert.IsType<DPoPAccessCredentials>(credentials).DPoPNonce;
            return Task.CompletedTask;
        }

        agent.CredentialsUpdatedAsync = (args, token) => Persist(args.AccessCredentials, token);

        Response result = await Invoke(endpoint, useAgent, agent, client, onCredentialsUpdated: Persist);

        Assert.True(result.Succeeded);
        Assert.Equal("updatedNonce", persistedNonce);
    }

    [Fact]
    public void EndpointTypesResolveThroughConfiguredSourceGeneratedMetadata()
    {
        Type[] types =
        [
            typeof(StartPhoneVerificationRequest), typeof(VerifyPhoneRequest), typeof(ImportContactsRequest),
            typeof(DismissContactMatchRequest), typeof(GetContactMatchesResponse), typeof(ImportContactsResponse),
            typeof(ContactMatch), typeof(ContactSyncStatus), typeof(ContactSyncStatusResult),
            typeof(PhoneVerificationResult), typeof(ProfileView), typeof(EmptyResponse)
        ];
        foreach (Type type in types)
        {
            Assert.NotNull(BlueskyServer.BlueskyJsonSerializerOptions.GetTypeInfo(type));
        }
    }

    [Fact(Explicit = true)]
    public void ReflectionIsDisabledForMetadataValidation()
    {
        Assert.False(JsonSerializer.IsReflectionEnabledByDefault);
    }

    private static TestServer Respond(string body) => TestServerBuilder.CreateServer(TestServerBuilder.DefaultUri, async context =>
    {
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync(body, TestContext.Current.CancellationToken);
    });

    private static BlueskyAgent CreateAgent(
        TestServer server,
        ILoggerFactory? loggerFactory = null,
        int maximumResponseSize = AtProtoHttpClient.DefaultMaximumResponseSize)
        => CreateAgent(new TestHttpClientFactory(server), loggerFactory, maximumResponseSize);

    private static BlueskyAgent CreateAgent(
        IHttpClientFactory httpClientFactory,
        ILoggerFactory? loggerFactory = null,
        int maximumResponseSize = AtProtoHttpClient.DefaultMaximumResponseSize)
    {
        Uri pds = new("https://pds.example.com");

        return new BlueskyAgent(httpClientFactory, new BlueskyAgentOptions
        {
            PublicAppViewUri = new Uri("https://public.example.com"),
            LoggerFactory = loggerFactory,
            MaximumResponseSize = maximumResponseSize
        })
        {
            Service = pds,
            Credentials = new AccessCredentials(pds, AuthenticationType.UsernamePassword, JwtBuilder.CreateJwt(new Did("did:plc:test"), pds.ToString()), "refreshToken")
        };
    }

    private static async Task<Response> Invoke(
        string endpoint,
        bool useAgent,
        BlueskyAgent agent,
        HttpClient client,
        ILoggerFactory? loggerFactory = null,
        Func<AtProtoCredential, CancellationToken, Task>? onCredentialsUpdated = null,
        CancellationToken? cancellationOverride = null,
        int maximumResponseSize = AtProtoHttpClient.DefaultMaximumResponseSize)
    {
        CancellationToken token = cancellationOverride ?? TestContext.Current.CancellationToken;
        if (useAgent)
        {
            return endpoint switch
            {
                "startPhoneVerification" => Response.From(await agent.StartPhoneVerification("+12125550123", token)),
                "verifyPhone" => Response.From(await agent.VerifyPhone("+12125550123", "012345", token)),
                "importContacts" => Response.From(await agent.ImportContacts("single-use-test-token", ["+12125550124", "", "+12125550124"], token)),
                "dismissMatch" => Response.From(await agent.DismissContactMatch(new Did("did:plc:test"), token)),
                "removeData" => Response.From(await agent.RemoveContactData(token)),
                "getMatches" => Response.From(await agent.GetContactMatches(2, "older&cursor=1", token)),
                "getSyncStatus" => Response.From(await agent.GetContactSyncStatus(token)),
                _ => throw new ArgumentOutOfRangeException(nameof(endpoint))
            };
        }

        return endpoint switch
        {
            "startPhoneVerification" => Response.From(await BlueskyServer.StartPhoneVerification("+12125550123", agent.Service, agent.Credentials!, client, onCredentialsUpdated, loggerFactory, maximumResponseSize, token)),
            "verifyPhone" => Response.From(await BlueskyServer.VerifyPhone("+12125550123", "012345", agent.Service, agent.Credentials!, client, onCredentialsUpdated, loggerFactory, maximumResponseSize, token)),
            "importContacts" => Response.From(await BlueskyServer.ImportContacts("single-use-test-token", ["+12125550124", "", "+12125550124"], agent.Service, agent.Credentials!, client, onCredentialsUpdated, loggerFactory, maximumResponseSize, token)),
            "dismissMatch" => Response.From(await BlueskyServer.DismissContactMatch(new Did("did:plc:test"), agent.Service, agent.Credentials!, client, onCredentialsUpdated, loggerFactory, maximumResponseSize, token)),
            "removeData" => Response.From(await BlueskyServer.RemoveContactData(agent.Service, agent.Credentials!, client, onCredentialsUpdated, loggerFactory, maximumResponseSize, token)),
            "getMatches" => Response.From(await BlueskyServer.GetContactMatches(2, "older&cursor=1", agent.Service, agent.Credentials!, client, onCredentialsUpdated, loggerFactory, maximumResponseSize, token)),
            "getSyncStatus" => Response.From(await BlueskyServer.GetContactSyncStatus(agent.Service, agent.Credentials!, client, onCredentialsUpdated, loggerFactory, maximumResponseSize, token)),
            _ => throw new ArgumentOutOfRangeException(nameof(endpoint))
        };
    }

    private sealed record Response(bool Succeeded, HttpStatusCode StatusCode, AtErrorDetail? Error, object? Value)
    {
        internal static Response From<T>(AtProtoHttpResult<T> result) where T : class =>
            new(result.Succeeded, result.StatusCode, result.AtErrorDetail, result.Result);
    }

    private sealed class CancellationHandler : HttpMessageHandler, IHttpClientFactory
    {
        public HttpClient CreateClient(string name = "") => new(this, disposeHandler: false);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}") });
        }
    }
}
