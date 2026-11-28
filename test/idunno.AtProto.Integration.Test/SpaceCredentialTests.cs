// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

#pragma warning disable BSKYSpaces

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

using idunno.AtProto.Authentication;
using idunno.AtProto.Repo;
using idunno.AtProto.Space;
using idunno.AtProto.Space.Models;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Time.Testing;

namespace idunno.AtProto.Integration.Test;

public class SpaceCredentialTests
{
    private static readonly Did s_user = new("did:plc:user");
    private static readonly Did s_author = new("did:plc:writer");
    private static readonly SpaceAtUri s_space = new("at:space/did:plc:owner/app.bsky.bookmark/test");
    private static readonly SpaceAtUri s_record = new(
        spaceDid: s_space.SpaceDid,
        spaceType: s_space.SpaceType,
        spaceKey: s_space.SpaceKey,
        authorDid: s_author,
        collection: new Nsid("app.bsky.bookmark"),
        recordKey: new RecordKey("record"));
    private static readonly Cid s_cid = Cid.FromDagCbor(Convert.FromHexString("A1647465787464706C616E"));

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task DelegationAuthorizationAndAuthenticatedReadsDoNotForwardAccountCredentials(
        bool useDefaultDirectory,
        bool authenticatedReader)
    {
        using SpaceTestServer fixture = new() { UseDefaultDirectory = useDefaultDirectory };
        using AtProtoAgent account = fixture.CreateAgent(true);
        using AtProtoAgent reader = fixture.CreateAgent(authenticatedReader);
        AtProtoHttpResult<SpaceDelegationToken> delegation = await account.GetSpaceDelegationToken(s_space, cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(delegation.Succeeded);
        Assert.Equal(s_user, delegation.Result.Issuer);
        AtProtoHttpResult<SpaceCredential> authorized = await reader.AuthorizeSpaceSync(
            space: s_space,
            delegationToken: delegation.Result,
            clientAttestation: "client-assertion",
            notify: new SpaceNotificationRegistration(new Uri("https://app.example/notify"), "notification-secret"),
            cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(authorized.Succeeded);
        using SpaceCredential credential = authorized.Result;
        Assert.True(delegation.Result.IsUsed);
        Assert.Equal(authenticatedReader, reader.IsAuthenticated);
        Assert.Equal(fixture.Clock.GetUtcNow().AddMinutes(10), credential.ExpiresAt);

        for (int read = 0; read < 2; read++)
        {
            AtProtoHttpResult<AtProtoRepositoryRecord> record = await reader.GetSpaceRecord(s_record, credential, TestContext.Current.CancellationToken);
            Assert.True(record.Succeeded);
            Assert.Equal(s_record, record.Result.Uri);
            Assert.Equal(s_cid, record.Result.Cid);
            Assert.Equal("plan", record.Result.Value!["text"]!.GetValue<string>());
        }

        Assert.Equal(2, fixture.KeyShares.Distinct().Count());
        AtProtoHttpResult<PagedReadOnlyCollection<AtProtoRepositoryRecord>> list = await reader.ListSpaceRecords(
            space: s_space,
            repo: s_author,
            credential: credential,
            collection: new Nsid("app.bsky.bookmark"),
            limit: 1,
            cursor: "before &/record",
            cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(list.Succeeded);
        Assert.Equal(s_record, Assert.Single(list.Result).Uri);
        Assert.Equal("next &/record", list.Result.Cursor);
        Assert.Equal("before &/record", fixture.LastCursor);
    }

    [Theory]
    [InlineData("SpaceNotFound")]
    [InlineData("SpaceDeleted")]
    [InlineData("AccountNotAuthorized")]
    [InlineData("AppNotAuthorized")]
    [InlineData("NotAuthorized")]
    [InlineData("InvalidDelegationToken")]
    [InlineData("InvalidClientAttestation")]
    [InlineData("RegistrationNotConfirmed")]
    [InlineData("RepoTakendown")]
    [InlineData("RepoDeactivated")]
    public async Task AuthorizationErrorsPreserveAtErrorAndConsumeDelegation(string error)
    {
        using SpaceTestServer fixture = new() { ErrorEndpoint = "authorizeSpaceSync", Error = error };
        using AtProtoAgent account = fixture.CreateAgent(true);
        using AtProtoAgent reader = fixture.CreateAgent(false);
        SpaceDelegationToken delegation = Assert.IsType<SpaceDelegationToken>((await account.GetSpaceDelegationToken(s_space, cancellationToken: TestContext.Current.CancellationToken)).Result);
        AtProtoHttpResult<SpaceCredential> response = await reader.AuthorizeSpaceSync(s_space, delegation, cancellationToken: TestContext.Current.CancellationToken);
        Assert.False(response.Succeeded);
        Assert.Equal(error, response.AtErrorDetail!.Error);
        Assert.IsType<AtProtoError>(response.AtErrorDetail, exactMatch: false);
        Assert.True(delegation.IsUsed);
        await Assert.ThrowsAsync<InvalidOperationException>(() => reader.AuthorizeSpaceSync(s_space, delegation, cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal(1, fixture.Authorizations);
    }

    [Fact]
    public async Task CancellationBeforeAuthorizationDoesNotConsumeDelegation()
    {
        using SpaceTestServer fixture = new();
        using AtProtoAgent account = fixture.CreateAgent(true);
        SpaceDelegationToken delegation = Assert.IsType<SpaceDelegationToken>((await account.GetSpaceDelegationToken(s_space, cancellationToken: TestContext.Current.CancellationToken)).Result);
        using CancellationTokenSource canceled = new();
        canceled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => AtProtoServer.AuthorizeSpaceSync(
            space: s_space,
            service: TestServerBuilder.DefaultUri,
            httpClient: fixture.HttpClient,
            delegationToken: delegation,
            cancellationToken: canceled.Token));
        Assert.False(delegation.IsUsed);
        Assert.Equal(0, fixture.Authorizations);
    }

    [Fact]
    public async Task TransportFailureConsumesDelegationBecauseTheAuthorityMightHaveObservedIt()
    {
        using SpaceTestServer fixture = new() { FailAuthorizationTransport = true };
        using AtProtoAgent account = fixture.CreateAgent(true);
        using AtProtoAgent reader = fixture.CreateAgent(false);
        SpaceDelegationToken delegation = Assert.IsType<SpaceDelegationToken>((await account.GetSpaceDelegationToken(s_space, cancellationToken: TestContext.Current.CancellationToken)).Result);
        await Assert.ThrowsAsync<HttpRequestException>(() => reader.AuthorizeSpaceSync(s_space, delegation, cancellationToken: TestContext.Current.CancellationToken));
        Assert.True(delegation.IsUsed);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task UnusableCredentialsFailBeforeRecordHostResolution(bool expired)
    {
        using SpaceTestServer fixture = new();
        using AtProtoAgent reader = fixture.CreateAgent(false);
        using SpaceCredential credential = Assert.IsType<SpaceCredential>((await reader.AuthorizeSpaceSync(s_space, cancellationToken: TestContext.Current.CancellationToken)).Result);
        int requests = fixture.Requests;
        if (expired)
        {
            fixture.Clock.Advance(TimeSpan.FromMinutes(10));
            await Assert.ThrowsAsync<InvalidOperationException>(() => reader.GetSpaceRecord(s_record, credential, TestContext.Current.CancellationToken));
        }
        else
        {
            credential.Dispose();
            await Assert.ThrowsAsync<ObjectDisposedException>(() => reader.GetSpaceRecord(s_record, credential, TestContext.Current.CancellationToken));
        }

        Assert.Equal(requests, fixture.Requests);
    }

    [Theory]
    [InlineData("issuer")]
    [InlineData("subject")]
    [InlineData("audience")]
    [InlineData("binding")]
    [InlineData("type")]
    [InlineData("algorithm")]
    [InlineData("kid")]
    [InlineData("expired")]
    [InlineData("long-lived")]
    [InlineData("future")]
    [InlineData("not-before")]
    [InlineData("missing-id")]
    [InlineData("duplicate")]
    [InlineData("signature")]
    public async Task AuthorizationRejectsUntrustedCredentialClaimsAndSignatures(string mutation)
    {
        using SpaceTestServer fixture = new() { TokenMutation = mutation };
        using AtProtoAgent reader = fixture.CreateAgent(false);
        await Assert.ThrowsAsync<InvalidDataException>(() => reader.AuthorizeSpaceSync(s_space, cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task TokenLifetimeIsRecheckedAfterIssuerResolution()
    {
        using SpaceTestServer fixture = new() { ExpireDuringResolution = true };
        using AtProtoAgent reader = fixture.CreateAgent(false);
        await Assert.ThrowsAsync<InvalidDataException>(() => reader.AuthorizeSpaceSync(s_space, cancellationToken: TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("getRecord", "InvalidSpaceMac")]
    [InlineData("getRecord", "RecordNotFound")]
    [InlineData("getRecord", "SpaceNotFound")]
    [InlineData("getRecord", "RepoNotFound")]
    [InlineData("getRecord", "RepoTakendown")]
    [InlineData("getRecord", "RepoSuspended")]
    [InlineData("getRecord", "RepoDeactivated")]
    [InlineData("listRecords", "SpaceNotFound")]
    [InlineData("listRecords", "RepoNotFound")]
    [InlineData("listRecords", "RepoTakendown")]
    [InlineData("listRecords", "RepoSuspended")]
    [InlineData("listRecords", "RepoDeactivated")]
    [InlineData("listRecords", "InvalidCursor")]
    public async Task ReadErrorsDoNotRequireMacAndPreserveAtError(string endpoint, string error)
    {
        using SpaceTestServer fixture = new() { ErrorEndpoint = endpoint, Error = error };
        using AtProtoAgent reader = fixture.CreateAgent(false);
        using SpaceCredential credential = Assert.IsType<SpaceCredential>((await reader.AuthorizeSpaceSync(s_space, cancellationToken: TestContext.Current.CancellationToken)).Result);
        AtErrorDetail? detail;
        if (endpoint == "getRecord")
        {
            AtProtoHttpResult<AtProtoRepositoryRecord> response = await reader.GetSpaceRecord(s_record, credential, TestContext.Current.CancellationToken);
            Assert.False(response.Succeeded);
            detail = response.AtErrorDetail;
        }
        else
        {
            AtProtoHttpResult<PagedReadOnlyCollection<AtProtoRepositoryRecord>> response = await reader.ListSpaceRecords(
                space: s_space,
                repo: s_author,
                credential: credential,
                cancellationToken: TestContext.Current.CancellationToken);
            Assert.False(response.Succeeded);
            detail = response.AtErrorDetail;
        }

        Assert.Equal(error, detail!.Error);
        Assert.NotEqual(typeof(AtErrorDetail), detail.GetType());
    }

    [Theory]
    [InlineData("body")]
    [InlineData("uri")]
    [InlineData("cid")]
    [InlineData("null-cid")]
    [InlineData("invalid-json")]
    [InlineData("mac")]
    [InlineData("missing-mac")]
    public async Task RecordReadRejectsTamperingWithoutReturningUnverifiedData(string mutation)
    {
        using SpaceTestServer fixture = new() { RecordMutation = mutation };
        using AtProtoAgent reader = fixture.CreateAgent(false);
        using SpaceCredential credential = Assert.IsType<SpaceCredential>((await reader.AuthorizeSpaceSync(s_space, cancellationToken: TestContext.Current.CancellationToken)).Result);
        await Assert.ThrowsAsync<InvalidDataException>(() => reader.GetSpaceRecord(s_record, credential, TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("duplicate")]
    [InlineData("wrong-cid")]
    [InlineData("missing-value")]
    [InlineData("wrong-collection")]
    [InlineData("too-many")]
    public async Task ListingsRejectInconsistentRecordData(string mutation)
    {
        using SpaceTestServer fixture = new() { ListingMutation = mutation };
        using AtProtoAgent reader = fixture.CreateAgent(false);
        using SpaceCredential credential = Assert.IsType<SpaceCredential>((await reader.AuthorizeSpaceSync(s_space, cancellationToken: TestContext.Current.CancellationToken)).Result);
        await Assert.ThrowsAsync<InvalidDataException>(() => reader.ListSpaceRecords(
            space: s_space,
            repo: s_author,
            credential: credential,
            collection: new Nsid("app.bsky.bookmark"),
            limit: 1,
            cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ListingCanExcludeValuesWithoutPretendingToVerifyAResponseMac()
    {
        using SpaceTestServer fixture = new();
        using AtProtoAgent reader = fixture.CreateAgent(false);
        using SpaceCredential credential = Assert.IsType<SpaceCredential>((await reader.AuthorizeSpaceSync(s_space, cancellationToken: TestContext.Current.CancellationToken)).Result);
        AtProtoHttpResult<PagedReadOnlyCollection<AtProtoRepositoryRecord>> response = await reader.ListSpaceRecords(
            space: s_space,
            repo: s_author,
            credential: credential,
            excludeValues: true,
            cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(response.Succeeded);
        Assert.Null(Assert.Single(response.Result).Value);
        Assert.False(response.HttpResponseHeaders!.Contains("Signature"));
    }

    [Theory]
    [InlineData("listRecords", false, 1, "fixture-cursor")]
    [InlineData("listRecordsExcluded", true, 1, null)]
    [InlineData("listRecordsNext", false, 0, null)]
    public async Task ListingsReplaySanitizedAlphaResponses(string capture, bool excludeValues, int count, string? cursor)
    {
        using SpaceTestServer fixture = new() { ListingCapture = await LoadAlphaCapture(capture) };
        using AtProtoAgent reader = fixture.CreateAgent(false);
        using SpaceCredential credential = Assert.IsType<SpaceCredential>((await reader.AuthorizeSpaceSync(s_space, cancellationToken: TestContext.Current.CancellationToken)).Result);
        AtProtoHttpResult<PagedReadOnlyCollection<AtProtoRepositoryRecord>> result = await reader.ListSpaceRecords(
            space: s_space,
            repo: s_author,
            credential: credential,
            collection: new Nsid("org.example.space.plan"),
            limit: 1,
            excludeValues: excludeValues,
            cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(result.Succeeded);
        Assert.Equal(count, result.Result.Count);
        Assert.Equal(cursor, result.Result.Cursor);
        if (count == 1)
        {
            Assert.Equal(new Cid("bafyreicyj7vwd46tjxojgmo3x6hy6witjvohdthvddogfnwgttawpnxk6a"), result.Result[0].Cid);
            Assert.Equal(excludeValues, result.Result[0].Value is null);
        }
    }

    [Fact]
    public async Task OriginalAlphaRecordValueMatchesThePdsCanonicalCid()
    {
        JsonElement capture = await LoadAlphaCapture("getRecord");
        SpaceRecordResponse? record = capture.Deserialize(SpaceSourceGenerationContext.Default.SpaceRecordResponse);
        Assert.NotNull(record);
        Assert.True(record.Uri.IsRecord);
        Assert.Equal(new Nsid("org.example.space.plan"), record.Uri.Collection);
        Assert.Equal("space credential live check", record.Value.GetProperty("text").GetString());
        Assert.Equal(record.Cid, Cid.FromDagCbor(DagCbor.FromJsonElement(record.Value)));
    }

    [Fact]
    public async Task RedactedAlphaCredentialResponsesUseRegisteredSourceGeneratedMetadata()
    {
        JsonElement delegation = await LoadAlphaCapture("getDelegationToken");
        JsonElement authorization = await LoadAlphaCapture("authorizeSpaceSync");
        SpaceDelegationResponse? token = delegation.Deserialize(SpaceSourceGenerationContext.Default.SpaceDelegationResponse);
        AuthorizeSpaceSyncResponse? credential = authorization.Deserialize(SpaceSourceGenerationContext.Default.AuthorizeSpaceSyncResponse);
        Assert.NotNull(token);
        Assert.NotNull(credential);
        Assert.Equal("<redacted>", token.Token);
        Assert.Equal("<redacted>", credential.Credential);
        Assert.Equal(new TimestampIdentifier("3n3cwms7dcm2j"), credential.SpaceRev);
    }

    /// <summary>Loads only sanitized response bodies; captured JWTs and MAC secrets are deliberately not retained.</summary>
    private static async Task<JsonElement> LoadAlphaCapture(string name)
    {
        await using Stream stream = typeof(SpaceCredentialTests).Assembly.GetManifestResourceStream(
            "idunno.AtProto.Integration.Test.TestData.SpaceAlphaResponses.json") ??
            throw new InvalidOperationException("The Spaces alpha response fixture is missing.");
        using JsonDocument document = await JsonDocument.ParseAsync(stream, cancellationToken: TestContext.Current.CancellationToken);
        return document.RootElement.GetProperty(name).GetProperty("body").Clone();
    }

    /// <summary>Provides independently signed JWTs, proof verification and recipient-side HPKE for the test transport.</summary>
    private sealed class SpaceTestServer : IDisposable
    {
        private readonly ECDsa _issuer = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        private readonly ECDiffieHellman _recipient = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
        private readonly Dictionary<string, Did> _credentials = new(StringComparer.Ordinal);
        private readonly TestServer _server;

        /// <summary>Creates a fixed-clock authority, user PDS and record-author PDS on one test transport.</summary>
        internal SpaceTestServer()
        {
            _server = TestServerBuilder.CreateServer(TestServerBuilder.DefaultUri, Handle);
            HttpClient = _server.CreateClient();
        }

        internal FakeTimeProvider Clock { get; } = new(new DateTimeOffset(
            year: 2026,
            month: 11,
            day: 27,
            hour: 12,
            minute: 0,
            second: 0,
            offset: TimeSpan.Zero));
        internal HttpClient HttpClient { get; }
        internal string? ErrorEndpoint { get; init; }
        internal string? Error { get; init; }
        internal string? TokenMutation { get; init; }
        internal string? RecordMutation { get; init; }
        internal string? ListingMutation { get; init; }
        internal JsonElement? ListingCapture { get; init; }
        internal bool ExpireDuringResolution { get; init; }
        internal bool FailAuthorizationTransport { get; init; }
        internal bool UseDefaultDirectory { get; init; }
        internal int Authorizations { get; private set; }
        internal int Requests { get; private set; }
        internal List<Did> KeyShares { get; } = [];
        internal string? LastCursor { get; private set; }

        /// <summary>Creates an agent with optional account credentials but one fixed clock for the complete flow.</summary>
        internal AtProtoAgent CreateAgent(bool authenticated)
        {
            AtProtoAgent agent = new(
                service: TestServerBuilder.DefaultUri,
                httpClientFactory: new TestHttpClientFactory(_server),
                options: new AtProtoAgentOptions
                {
                    TimeProvider = Clock,
                    EnableBackgroundTokenRefresh = false,
                    PlcDirectoryServer = UseDefaultDirectory ? new Uri("https://plc.directory") : TestServerBuilder.DefaultUri
                });
            if (authenticated)
            {
                agent.Credentials = new AccessCredentials(
                    service: TestServerBuilder.DefaultUri,
                    authenticationType: AuthenticationType.UsernamePassword,
                    accessJwt: JwtBuilder.CreateJwt(s_user, timeProvider: Clock),
                    refreshToken: "refresh");
            }

            return agent;
        }

        /// <summary>Releases the HTTP transport and both owned private test keys.</summary>
        public void Dispose()
        {
            HttpClient.Dispose();
            _server.Dispose();
            _issuer.Dispose();
            _recipient.Dispose();
        }

        /// <summary>Dispatches protocol requests and validates authentication at each endpoint.</summary>
        private async Task Handle(HttpContext context)
        {
            Requests++;
            context.Response.ContentType = "application/json";
            string path = context.Request.Path.Value!;
            if (path.StartsWith("/did:plc:", StringComparison.Ordinal))
            {
                Assert.Equal(UseDefaultDirectory ? "plc.directory" : TestServerBuilder.DefaultUri.Host, context.Request.Host.Host);
                Assert.False(context.Request.Headers.ContainsKey("Authorization"));
                Did did = new(path[1..]);
                ECParameters parameters = did == s_author ? _recipient.ExportParameters(false) : _issuer.ExportParameters(false);
                string fragment = did == s_author ? "atproto_kem" : "atproto";
                string multikey = SpaceCrypto.ToDidKey(parameters).Value["did:key:".Length..];
                await context.Response.WriteAsync($$"""
                    {"id":"{{did}}","verificationMethod":[{"id":"{{did}}#{{fragment}}","type":"Multikey","controller":"{{did}}","publicKeyMultibase":"{{multikey}}"}],"service":[{"id":"#atproto_pds","type":"AtprotoPersonalDataServer","serviceEndpoint":"{{TestServerBuilder.DefaultUri}}"}]}
                    """);
                if (ExpireDuringResolution && Authorizations > 0)
                {
                    Clock.Advance(TimeSpan.FromMinutes(10));
                }

                return;
            }

            string endpoint = path["/xrpc/com.atproto.space.".Length..];
            if (endpoint == "authorizeSpaceSync")
            {
                Authorizations++;
                if (FailAuthorizationTransport)
                {
                    throw new HttpRequestException("The authority observed the request but the transport failed.");
                }

                Assert.Equal("POST", context.Request.Method);
                Assert.False(context.Request.Headers.ContainsKey("Authorization"));
                using JsonDocument request = await JsonDocument.ParseAsync(context.Request.Body, cancellationToken: context.RequestAborted);
                Assert.Equal(s_space.ToString(), request.RootElement.GetProperty("space").GetString());
                Did key = new(request.RootElement.GetProperty("credentialKey").GetString()!);
                using ECDiffieHellman parsed = SpaceCrypto.ParseDidKey(key);
                if (request.RootElement.TryGetProperty("notify", out JsonElement notify))
                {
                    Assert.Equal("https://app.example/notify", notify.GetProperty("endpoint").GetString());
                    Assert.Equal("notification-secret", notify.GetProperty("token").GetString());
                    Assert.Equal("client-assertion", request.RootElement.GetProperty("clientAttestation").GetString());
                }

                if (ErrorEndpoint == endpoint)
                {
                    await WriteError(context);
                    return;
                }

                string credential = SignToken(key);
                _credentials.Add(credential, key);
                await context.Response.WriteAsync(JsonSerializer.Serialize(new { credential }));
                return;
            }

            Assert.Equal("GET", context.Request.Method);
            Assert.Equal(s_space.ToString(), context.Request.Query["space"].ToString());
            if (endpoint == "getDelegationToken")
            {
                Assert.StartsWith("Bearer ", context.Request.Headers.Authorization.ToString());
                await context.Response.WriteAsync(JsonSerializer.Serialize(new { token = SignToken(null) }));
                return;
            }

            Assert.Equal(s_author.Value, context.Request.Query["repo"].ToString());
            VerifyProof(context);
            if (ErrorEndpoint == endpoint)
            {
                await WriteError(context);
                return;
            }

            if (endpoint == "getRecord")
            {
                Assert.Equal(s_record.Collection!.ToString(), context.Request.Query["collection"].ToString());
                Assert.Equal(s_record.RecordKey!.Value, context.Request.Query["rkey"].ToString());
                const string prefix = "mac=(\"atproto-space-record\";sf);keyid=\"";
                string accept = context.Request.Headers["Accept-Signature"].ToString();
                Assert.StartsWith(prefix, accept);
                Did share = new(accept[prefix.Length..accept.IndexOf("\";tag=", StringComparison.Ordinal)]);
                Assert.EndsWith("\";tag=\"atproto-space-record\"", accept);
                KeyShares.Add(share);
                using ECDiffieHellman sender = SpaceCrypto.ParseDidKey(share);
                byte[] secret = RecipientExport(sender);
                try
                {
                    string metadata = $"uri=\"{s_record}\", cid=\"{s_cid}\"";
                    string parameters = $"(\"atproto-space-record\";sf);keyid=\"{share}\";tag=\"atproto-space-record\";extra=42";
                    byte[] mac = HMACSHA256.HashData(secret, Encoding.ASCII.GetBytes($"\"atproto-space-record\";sf: {metadata}\n\"@signature-params\": {parameters}"));
                    if (RecordMutation == "mac")
                    {
                        mac[0] ^= 1;
                    }

                    if (RecordMutation != "missing-mac")
                    {
                        context.Response.Headers["Atproto-Space-Record"] = $"uri=\"{s_record}\",  cid=\"{s_cid}\"";
                        context.Response.Headers["Signature-Input"] = "relabel=" + parameters.Replace(";extra=42", ";extra=00042", StringComparison.Ordinal);
                        context.Response.Headers["Signature"] = "relabel=:" + Convert.ToBase64String(mac) + ":";
                    }
                }
                finally
                {
                    CryptographicOperations.ZeroMemory(secret);
                }

                if (RecordMutation == "invalid-json")
                {
                    await context.Response.WriteAsync("{");
                    return;
                }

                await context.Response.WriteAsync(JsonSerializer.Serialize(new
                {
                    uri = RecordMutation == "uri" ? s_space.ToString() : s_record.ToString(),
                    cid = RecordMutation switch
                    {
                        "null-cid" => null,
                        "cid" => Cid.FromDagCbor([0xa0]).Value,
                        _ => s_cid.Value
                    },
                    value = new { text = RecordMutation == "body" ? "altered" : "plan" }
                }));
                return;
            }

            Assert.Equal("listRecords", endpoint);
            Assert.False(context.Request.Headers.ContainsKey("Accept-Signature"));
            if (ListingCapture is JsonElement capture)
            {
                await context.Response.WriteAsync(capture.GetRawText());
                return;
            }

            LastCursor = context.Request.Query["cursor"].ToString();
            bool excludeValues = context.Request.Query["excludeValues"] == "true";
            JsonObject entry = new()
            {
                ["collection"] = ListingMutation == "wrong-collection" ? "app.bsky.other" : s_record.Collection!.ToString(),
                ["rkey"] = s_record.RecordKey!.Value,
                ["cid"] = ListingMutation == "wrong-cid" ? Cid.FromDagCbor([0xa0]).Value : s_cid.Value
            };
            if (!excludeValues && ListingMutation != "missing-value")
            {
                entry["value"] = new JsonObject { ["text"] = "plan" };
            }

            JsonArray records = new(entry);
            if (ListingMutation is "duplicate" or "too-many")
            {
                JsonNode another = entry.DeepClone();
                if (ListingMutation == "too-many")
                {
                    another["rkey"] = "another";
                }

                records.Add(another);
            }

            await context.Response.WriteAsync(new JsonObject { ["records"] = records, ["cursor"] = "next &/record" }.ToJsonString());
        }

        /// <summary>Verifies the request using an independently constructed RFC 9421 signature base.</summary>
        private void VerifyProof(HttpContext context)
        {
            string authorization = context.Request.Headers.Authorization.ToString();
            const string scheme = "Atproto-Space-Credential ";
            Assert.StartsWith(scheme, authorization);
            Did key = _credentials[authorization[scheme.Length..]];
            Assert.Equal(s_author.Value, context.Request.Headers["Atproto-Space-Audience"].ToString());
            const string parameters = "(\"authorization\" \"atproto-space-audience\");tag=\"atproto-space-credential\"";
            Assert.Equal("sig=" + parameters, context.Request.Headers["Signature-Input"].ToString());
            string signature = context.Request.Headers["Signature"].ToString();
            using ECDiffieHellman publicKey = SpaceCrypto.ParseDidKey(key);
            using ECDsa verifier = ECDsa.Create(publicKey.ExportParameters(false));
            Assert.True(verifier.VerifyData(
                data: Encoding.ASCII.GetBytes($"\"authorization\": {authorization}\n\"atproto-space-audience\": {s_author}\n\"@signature-params\": {parameters}"),
                signature: Convert.FromBase64String(signature[5..^1]),
                hashAlgorithm: HashAlgorithmName.SHA256,
                signatureFormat: DSASignatureFormat.IeeeP1363FixedFieldConcatenation));
        }

        /// <summary>Signs the test token, optionally mutating one untrusted claim or header.</summary>
        private string SignToken(Did? key)
        {
            bool credential = key is not null;
            JsonObject header = new()
            {
                ["alg"] = "ES256",
                ["kid"] = "#atproto",
                ["typ"] = credential ? "atproto-space-credential+jwt" : "atproto-space-delegation+jwt"
            };
            JsonObject payload = new()
            {
                ["iss"] = credential ? s_space.SpaceDid.Value : s_user.Value,
                ["sub"] = s_space.ToString(),
                ["iat"] = Clock.GetUtcNow().ToUnixTimeSeconds(),
                ["exp"] = Clock.GetUtcNow().AddMinutes(credential ? 10 : 1).ToUnixTimeSeconds(),
                ["jti"] = Guid.NewGuid().ToString()
            };
            if (credential)
            {
                payload["cnf"] = new JsonObject { ["kid"] = key!.Value };
            }
            else
            {
                payload["aud"] = $"{s_space.SpaceDid}#atproto_pds";
            }

            if (credential)
            {
                switch (TokenMutation)
                {
                    case "issuer": payload["iss"] = s_user.Value; break;
                    case "subject": payload["sub"] = s_record.ToString(); break;
                    case "audience": payload["aud"] = "unexpected"; break;
                    case "binding": payload["cnf"]!["kid"] = "did:key:wrong"; break;
                    case "type": header["typ"] = "atproto-space-delegation+jwt"; break;
                    case "algorithm": header["alg"] = "ES256K"; break;
                    case "kid": header["kid"] = "#wrong"; break;
                    case "expired": payload["exp"] = Clock.GetUtcNow().ToUnixTimeSeconds(); break;
                    case "long-lived": payload["exp"] = Clock.GetUtcNow().AddMinutes(61).ToUnixTimeSeconds(); break;
                    case "future": payload["iat"] = Clock.GetUtcNow().AddSeconds(6).ToUnixTimeSeconds(); break;
                    case "not-before": payload["nbf"] = Clock.GetUtcNow().AddSeconds(6).ToUnixTimeSeconds(); break;
                    case "missing-id": payload.Remove("jti"); break;
                }
            }

            string claims = payload.ToJsonString();
            if (credential && TokenMutation == "duplicate")
            {
                claims = claims[..^1] + $",\"iss\":\"{s_space.SpaceDid}\"}}";
            }

            string unsigned = Encode(Encoding.UTF8.GetBytes(header.ToJsonString())) + "." + Encode(Encoding.UTF8.GetBytes(claims));
            byte[] signature = _issuer.SignData(Encoding.ASCII.GetBytes(unsigned), HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
            if (credential && TokenMutation == "signature")
            {
                signature[0] ^= 1;
            }

            return unsigned + "." + Encode(signature);
        }

        /// <summary>Derives the recipient's export-only HPKE secret independently from the sender implementation.</summary>
        private byte[] RecipientExport(ECDiffieHellman sender)
        {
            byte[] kemId = [.. Encoding.ASCII.GetBytes("KEM"), .. new byte[] { 0, 16 }];
            byte[] suiteId = [.. Encoding.ASCII.GetBytes("HPKE"), .. new byte[] { 0, 16, 0, 1, 255, 255 }];
            byte[] Extract(byte[] id, byte[] salt, string label, byte[] input) =>
                HKDF.Extract(HashAlgorithmName.SHA256, [.. Encoding.ASCII.GetBytes("HPKE-v1"), .. id, .. Encoding.ASCII.GetBytes(label), .. input], salt);
            byte[] Expand(byte[] id, byte[] prk, string label, byte[] info) =>
                HKDF.Expand(
                    hashAlgorithmName: HashAlgorithmName.SHA256,
                    prk: prk,
                    outputLength: 32,
                    info: [0, 32, .. Encoding.ASCII.GetBytes("HPKE-v1"), .. id, .. Encoding.ASCII.GetBytes(label), .. info]);
            byte[] Point(ECParameters point) => [4, .. point.Q.X!, .. point.Q.Y!];
            using ECDiffieHellmanPublicKey publicKey = sender.PublicKey;
            byte[] dh = _recipient.DeriveRawSecretAgreement(publicKey);
            byte[] eae = Extract(id: kemId, salt: [], label: "eae_prk", input: dh);
            byte[] shared = Expand(
                id: kemId,
                prk: eae,
                label: "shared_secret",
                info: [.. Point(sender.ExportParameters(false)), .. Point(_recipient.ExportParameters(false))]);
            byte[] secret = Extract(id: suiteId, salt: shared, label: "secret", input: []);
            byte[] context = [
                0,
                .. Extract(id: suiteId, salt: [], label: "psk_id_hash", input: []),
                .. Extract(id: suiteId, salt: [], label: "info_hash", input: "atproto-mac"u8.ToArray())];
            byte[] exporter = Expand(id: suiteId, prk: secret, label: "exp", info: context);
            try
            {
                return Expand(id: suiteId, prk: exporter, label: "sec", info: []);
            }
            finally
            {
                foreach (byte[] material in new[] { dh, eae, shared, secret, exporter })
                {
                    CryptographicOperations.ZeroMemory(material);
                }
            }
        }

        /// <summary>Produces canonical JWT base64url segments without padding.</summary>
        private static string Encode(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

        /// <summary>Returns a lexicon error without any success-response authentication fields.</summary>
        private async Task WriteError(HttpContext context)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsync(JsonSerializer.Serialize(new { error = Error, message = "test endpoint error" }));
        }
    }
}
