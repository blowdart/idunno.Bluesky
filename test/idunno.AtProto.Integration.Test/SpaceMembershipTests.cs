// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

#pragma warning disable BSKYSpaces

using System.Text.Json;

using idunno.AtProto.Authentication;
using idunno.AtProto.Repo;
using idunno.AtProto.Space;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;

namespace idunno.AtProto.Integration.Test;

public class SpaceMembershipTests
{
    private const string SpaceUri = "at:space/did:plc:owner/app.bsky.bookmark/space-key";
    private const string MemberDid = "did:plc:member";
    private const string AlphaSpaceUri = "at:space/did:plc:fixture/app.bsky.bookmark/fixture";

    [Theory]
    [InlineData(StatusCodes.Status200OK)]
    [InlineData(StatusCodes.Status204NoContent)]
    public async Task MemberEndpointsSendRequestsAndReturnPaginatedMembers(int statusCode)
    {
        Uri service = TestServerBuilder.DefaultUri;
        Dictionary<string, JsonElement> bodies = [];
        TestServer server = TestServerBuilder.CreateServer(service, async context =>
        {
            Assert.StartsWith("Bearer ", context.Request.Headers.Authorization.ToString());
            string path = context.Request.Path;
            if (path == "/xrpc/com.atproto.memberspace.listMembers")
            {
                Assert.Equal(HttpMethods.Get, context.Request.Method);
                Assert.Equal(SpaceUri, context.Request.Query["space"].ToString());
                Assert.Equal("1000", context.Request.Query["limit"].ToString());
                Assert.Equal("next/+", context.Request.Query["cursor"].ToString());
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync("""{"members":[{"did":"did:plc:member","read":true,"write":false}],"cursor":"second"}""");
            }
            else
            {
                Assert.Equal(HttpMethods.Post, context.Request.Method);
                Assert.True(path is "/xrpc/com.atproto.memberspace.putMembers" or "/xrpc/com.atproto.memberspace.removeMembers");
                using JsonDocument body = await JsonDocument.ParseAsync(context.Request.Body);
                bodies[path] = body.RootElement.Clone();
                context.Response.StatusCode = statusCode;
            }
        });

        using AtProtoAgent agent = CreateAgent(service, server);
        SpaceAtUri space = new(SpaceUri);
        SpaceMember member = new() { Did = MemberDid, Read = true, Write = false };
        AtProtoHttpResult<EmptyResponse> put = await agent.PutSpaceMembers(space, [member], cancellationToken: TestContext.Current.CancellationToken);
        AtProtoHttpResult<EmptyResponse> remove = await agent.RemoveSpaceMembers(space, [member.Did], cancellationToken: TestContext.Current.CancellationToken);
        AtProtoHttpResult<PagedReadOnlyCollection<SpaceMember>> list = await agent.ListSpaceMembers(
            space: space,
            limit: 1000,
            cursor: "next/+",
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(put.Succeeded);
        Assert.True(remove.Succeeded);
        Assert.True(list.Succeeded);
        Assert.Equal("second", list.Result.Cursor);
        SpaceMember listed = Assert.Single(list.Result);
        Assert.Equal(member.Did, listed.Did);
        Assert.True(listed.Read);
        Assert.False(listed.Write);
        JsonElement putBody = bodies["/xrpc/com.atproto.memberspace.putMembers"];
        Assert.Equal(SpaceUri, putBody.GetProperty("space").GetString());
        JsonElement written = Assert.Single(putBody.GetProperty("members").EnumerateArray());
        Assert.Equal(MemberDid, written.GetProperty("did").GetString());
        Assert.True(written.GetProperty("read").GetBoolean());
        Assert.False(written.GetProperty("write").GetBoolean());
        Assert.Equal(MemberDid, Assert.Single(bodies["/xrpc/com.atproto.memberspace.removeMembers"].GetProperty("dids").EnumerateArray()).GetString());
    }

    [Theory]
    [InlineData("put", "SpaceNotFound")]
    [InlineData("put", "NotAuthorized")]
    [InlineData("put", "PolicyMismatch")]
    [InlineData("remove", "SpaceNotFound")]
    [InlineData("remove", "NotAuthorized")]
    [InlineData("remove", "PolicyMismatch")]
    [InlineData("list", "SpaceNotFound")]
    [InlineData("list", "NotAuthorized")]
    [InlineData("list", "PolicyMismatch")]
    [InlineData("list", "InvalidCursor")]
    public async Task MemberEndpointErrorsAreReturned(string operation, string error)
    {
        Uri service = TestServerBuilder.DefaultUri;
        TestServer server = TestServerBuilder.CreateServer(service, async context =>
        {
            string expected = operation switch
            {
                "put" => "/xrpc/com.atproto.memberspace.putMembers",
                "remove" => "/xrpc/com.atproto.memberspace.removeMembers",
                _ => "/xrpc/com.atproto.memberspace.listMembers"
            };
            Assert.Equal(expected, context.Request.Path.Value);
            Assert.Equal(operation == "list" ? HttpMethods.Get : HttpMethods.Post, context.Request.Method);
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync($$"""{"error":"{{error}}","message":"test"}""");
        });
        using AtProtoAgent agent = CreateAgent(service, server);
        SpaceAtUri space = new(SpaceUri);
        AtErrorDetail? detail = operation switch
        {
            "put" => (await agent.PutSpaceMembers(space, [], cancellationToken: TestContext.Current.CancellationToken)).AtErrorDetail,
            "remove" => (await agent.RemoveSpaceMembers(space, [], cancellationToken: TestContext.Current.CancellationToken)).AtErrorDetail,
            _ => (await agent.ListSpaceMembers(space, cancellationToken: TestContext.Current.CancellationToken)).AtErrorDetail
        };
        Assert.Equal(error, detail?.Error);
        Assert.Equal(error, Assert.IsType<AtProtoError>(detail, exactMatch: false).GetType().Name);
    }

    [Theory]
    [InlineData("""{"members":null}""", false)]
    [InlineData("""{"members":[null]}""", true)]
    [InlineData("""{"members":[{"did":null,"read":true,"write":false}]}""", false)]
    [InlineData("""{"members":[{"did":"did:plc:member","read":true}]}""", false)]
    public async Task MemberListRejectsInvalidResponses(string body, bool throws)
    {
        Uri service = TestServerBuilder.DefaultUri;
        TestServer server = TestServerBuilder.CreateServer(service, async context =>
        {
            Assert.Equal("?space=at%3Aspace%2Fdid%3Aplc%3Aowner%2Fapp.bsky.bookmark%2Fspace-key&limit=1", context.Request.QueryString.Value);
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(body);
        });
        using AtProtoAgent agent = CreateAgent(service, server);
        if (throws)
        {
            await Assert.ThrowsAsync<JsonException>(() => agent.ListSpaceMembers(
                space: new SpaceAtUri(SpaceUri),
                limit: 1,
                cancellationToken: TestContext.Current.CancellationToken));
        }
        else
        {
            AtProtoHttpResult<PagedReadOnlyCollection<SpaceMember>> result = await agent.ListSpaceMembers(
                space: new SpaceAtUri(SpaceUri),
                limit: 1,
                cancellationToken: TestContext.Current.CancellationToken);
            Assert.False(result.Succeeded);
            Assert.Equal("InvalidResponse", result.AtErrorDetail?.Error);
        }
    }

    [Fact]
    public async Task MemberBatchAndListLimitsAreEnforced()
    {
        Uri service = TestServerBuilder.DefaultUri;
        TestServer server = TestServerBuilder.CreateServer(service, context =>
        {
            context.Response.StatusCode = StatusCodes.Status204NoContent;
            return Task.CompletedTask;
        });
        using AtProtoAgent agent = CreateAgent(service, server);
        SpaceAtUri space = new(SpaceUri);
        SpaceMember member = new() { Did = MemberDid, Read = true, Write = true };
        SpaceMember[] members = [.. Enumerable.Repeat(member, 100)];
        Did[] dids = [.. Enumerable.Repeat(member.Did, 100)];

        Assert.True((await agent.PutSpaceMembers(space, members, cancellationToken: TestContext.Current.CancellationToken)).Succeeded);
        Assert.True((await agent.RemoveSpaceMembers(space, dids, cancellationToken: TestContext.Current.CancellationToken)).Succeeded);
        await Assert.ThrowsAsync<ArgumentException>(() => agent.PutSpaceMembers(space, [.. members, member], cancellationToken: TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ArgumentException>(() => agent.RemoveSpaceMembers(space, [.. dids, member.Did], cancellationToken: TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ArgumentException>(() => agent.PutSpaceMembers(space, [null!], cancellationToken: TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ArgumentException>(() => agent.RemoveSpaceMembers(space, [null!], cancellationToken: TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => agent.ListSpaceMembers(space, limit: 0, cancellationToken: TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => agent.ListSpaceMembers(space, limit: 1001, cancellationToken: TestContext.Current.CancellationToken));
        SpaceAtUri record = new(SpaceUri + "/did:plc:member/app.bsky.feed.post/record-key");
        await Assert.ThrowsAsync<ArgumentException>(() => agent.PutSpaceMembers(record, [], cancellationToken: TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ArgumentException>(() => agent.RemoveSpaceMembers(record, [], cancellationToken: TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ArgumentException>(() => agent.ListSpaceMembers(record, cancellationToken: TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ManagedAccessUsesServiceCredentialAndEncodesOptionalParameters(bool includeOptional)
    {
        Uri service = TestServerBuilder.DefaultUri;
        string token = JwtBuilder.CreateJwt(
            did: null,
            issuer: "did:plc:host",
            audience: "did:plc:app#app",
            lxm: "com.atproto.managedspace.checkAccess");
        ServiceCredential credential = new(service, token);
        TestServer server = TestServerBuilder.CreateServer(service, async context =>
        {
            Assert.Equal(HttpMethods.Get, context.Request.Method);
            Assert.Equal("/xrpc/com.atproto.managedspace.checkAccess", context.Request.Path.Value);
            Assert.Equal($"Bearer {token}", context.Request.Headers.Authorization.ToString());
            Assert.Equal(SpaceUri, context.Request.Query["space"].ToString());
            Assert.Equal(includeOptional, context.Request.Query.ContainsKey("repo"));
            Assert.Equal(includeOptional, context.Request.Query.ContainsKey("clientId"));
            Assert.Equal(includeOptional ? MemberDid : "", context.Request.Query["repo"].ToString());
            Assert.Equal(includeOptional ? "https://example.com/client?value=a%2Bb" : "", context.Request.Query["clientId"].ToString());
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync("""{"read":true,"write":false}""");
        });
        using HttpClient client = server.CreateClient();
        AtProtoHttpResult<ManagedSpaceAccess> result = await AtProtoServer.CheckManagedSpaceAccess(
            space: new SpaceAtUri(SpaceUri),
            repo: includeOptional ? new Did(MemberDid) : null,
            clientId: includeOptional ? new Uri("https://example.com/client?value=a%2Bb") : null,
            service: service,
            serviceCredential: credential,
            httpClient: client,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.True(result.Result.Read);
        Assert.False(result.Result.Write);
        await Assert.ThrowsAsync<ArgumentException>(() => AtProtoServer.CheckManagedSpaceAccess(
            space: new SpaceAtUri(SpaceUri + "/did:plc:member/app.bsky.feed.post/record-key"),
            repo: null,
            clientId: null,
            service: service,
            serviceCredential: credential,
            httpClient: client,
            cancellationToken: TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("""{"read":true}""")]
    [InlineData("""{"write":false}""")]
    public async Task ManagedAccessRejectsIncompleteDecisions(string body)
    {
        Uri service = TestServerBuilder.DefaultUri;
        ServiceCredential credential = new(service, JwtBuilder.CreateJwt(
            did: null,
            issuer: "did:plc:host",
            audience: "did:plc:app#app"));
        TestServer server = TestServerBuilder.CreateServer(service, async context =>
        {
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(body);
        });
        using HttpClient client = server.CreateClient();
        AtProtoHttpResult<ManagedSpaceAccess> result = await AtProtoServer.CheckManagedSpaceAccess(
            space: new SpaceAtUri(SpaceUri),
            repo: null,
            clientId: null,
            service: service,
            serviceCredential: credential,
            httpClient: client,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Equal("InvalidResponse", result.AtErrorDetail?.Error);
    }

    [Fact]
    public async Task ManagedAccessReturnsServiceErrors()
    {
        Uri service = TestServerBuilder.DefaultUri;
        ServiceCredential credential = new(service, JwtBuilder.CreateJwt(
            did: null,
            issuer: "did:plc:host",
            audience: "did:plc:app#app"));
        TestServer server = TestServerBuilder.CreateServer(service, async context =>
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync("""{"error":"NotAuthorized","message":"denied"}""");
        });
        using HttpClient client = server.CreateClient();
        AtProtoHttpResult<ManagedSpaceAccess> result = await AtProtoServer.CheckManagedSpaceAccess(
            space: new SpaceAtUri(SpaceUri),
            repo: null,
            clientId: null,
            service: service,
            serviceCredential: credential,
            httpClient: client,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.IsType<NotAuthorized>(result.AtErrorDetail);
    }

    [Fact]
    public void PublishedRecordsUseTheirDiscriminatorsAndTypedEnvelopes()
    {
        const string policyJson = """{"uri":"at://did:plc:owner/com.atproto.space.policy/self","value":{"$type":"com.atproto.space.policy","policy":{"$type":"org.example.policy","enabled":true}}}""";
        const string memberJson = """{"uri":"at://did:plc:owner/com.atproto.memberspace.member/did:plc:member","value":{"$type":"com.atproto.memberspace.member","read":true,"write":false}}""";
        AtProtoRepositoryRecord<SpacePolicyRecord>? policy = JsonSerializer.Deserialize<AtProtoRepositoryRecord<SpacePolicyRecord>>(policyJson, AtProtoJsonSerializerOptions.Default);
        AtProtoRepositoryRecord<SpaceMemberRecord>? member = JsonSerializer.Deserialize<AtProtoRepositoryRecord<SpaceMemberRecord>>(memberJson, AtProtoJsonSerializerOptions.Default);
        Assert.NotNull(policy);
        Assert.NotNull(member);
        UnknownSpacePolicy unknown = Assert.IsType<UnknownSpacePolicy>(policy.Value.Policy);
        Assert.True(unknown.Value.GetProperty("enabled").GetBoolean());
        Assert.True(member.Value.Read);
        Assert.False(member.Value.Write);

        using JsonDocument policyDocument = JsonDocument.Parse(JsonSerializer.Serialize(policy.Value, AtProtoJsonSerializerOptions.Default));
        using JsonDocument memberDocument = JsonDocument.Parse(JsonSerializer.Serialize(member.Value, AtProtoJsonSerializerOptions.Default));
        Assert.Equal("com.atproto.space.policy", policyDocument.RootElement.GetProperty("$type").GetString());
        Assert.Equal("org.example.policy", policyDocument.RootElement.GetProperty("policy").GetProperty("$type").GetString());
        Assert.True(policyDocument.RootElement.GetProperty("policy").GetProperty("enabled").GetBoolean());
        Assert.Equal("com.atproto.memberspace.member", memberDocument.RootElement.GetProperty("$type").GetString());
    }

    [Theory]
    [InlineData("membersAdded", 1, false, "did:plc:fixture")]
    [InlineData("membersUpdated", 1, true, "did:plc:fixture")]
    [InlineData("membersRemoved", 0, false, null)]
    public async Task ListMembersReadsCapturedAlphaResponses(string capture, int count, bool write, string? cursor)
    {
        JsonElement body = await LoadAlphaCapture(capture);
        Uri service = TestServerBuilder.DefaultUri;
        TestServer server = TestServerBuilder.CreateServer(service, async context =>
        {
            Assert.Equal("/xrpc/com.atproto.memberspace.listMembers", context.Request.Path);
            Assert.Equal(AlphaSpaceUri, context.Request.Query["space"].ToString());
            Assert.Equal("10", context.Request.Query["limit"].ToString());
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(body.GetRawText());
        });

        using AtProtoAgent agent = CreateAgent(service, server);
        AtProtoHttpResult<PagedReadOnlyCollection<SpaceMember>> list = await agent.ListSpaceMembers(
            space: new SpaceAtUri(AlphaSpaceUri),
            limit: 10,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(list.Succeeded);
        Assert.Equal(count, list.Result.Count);
        Assert.Equal(cursor, list.Result.Cursor);
        if (count == 1)
        {
            SpaceMember member = Assert.Single(list.Result);
            Assert.Equal(new Did("did:plc:fixture"), member.Did);
            Assert.True(member.Read);
            Assert.Equal(write, member.Write);
        }
    }

    [Fact]
    public async Task PublishedRecordsReadCapturedAlphaResponses()
    {
        JsonElement policyCapture = await LoadAlphaCapture("policyRecord");
        JsonElement memberCapture = await LoadAlphaCapture("memberRecord");

        AtProtoRepositoryRecord<SpacePolicyRecord>? policy =
            JsonSerializer.Deserialize<AtProtoRepositoryRecord<SpacePolicyRecord>>(policyCapture.GetRawText(), AtProtoJsonSerializerOptions.Default);
        AtProtoRepositoryRecord<SpaceMemberRecord>? member =
            JsonSerializer.Deserialize<AtProtoRepositoryRecord<SpaceMemberRecord>>(memberCapture.GetRawText(), AtProtoJsonSerializerOptions.Default);

        Assert.NotNull(policy);
        SpaceAtUri policyUri = Assert.IsType<SpaceAtUri>(policy.Uri);
        Assert.True(policyUri.IsRecord);
        MemberSpacePolicy memberPolicy = Assert.IsType<MemberSpacePolicy>(policy.Value.Policy);
        Assert.True(memberPolicy.PublishPolicy);
        Assert.True(memberPolicy.PublishMembers);

        Assert.NotNull(member);
        SpaceAtUri memberUri = Assert.IsType<SpaceAtUri>(member.Uri);
        Assert.True(memberUri.IsRecord);
        Assert.True(member.Value.Read);
        Assert.False(member.Value.Write);
    }

    /// <summary>
    /// Loads one sanitized response captured from the Spaces alpha PDS.
    /// </summary>
    private static async Task<JsonElement> LoadAlphaCapture(string name)
    {
        await using Stream stream = typeof(SpaceMembershipTests).Assembly.GetManifestResourceStream(
            "idunno.AtProto.Integration.Test.TestData.SpaceMembershipAlphaResponses.json") ??
            throw new InvalidOperationException("The Spaces membership alpha response fixture is missing.");
        using JsonDocument document = await JsonDocument.ParseAsync(stream, cancellationToken: TestContext.Current.CancellationToken);
        return document.RootElement.GetProperty(name).Clone();
    }

    /// <summary>
    /// Creates an authenticated agent bound to the test server.
    /// </summary>
    private static AtProtoAgent CreateAgent(Uri service, TestServer server)
    {
        AtProtoAgent agent = new(service, new TestHttpClientFactory(server))
        {
            Credentials = new AccessCredentials(
                service: service,
                authenticationType: AuthenticationType.UsernamePassword,
                accessJwt: JwtBuilder.CreateJwt(new Did("did:plc:owner"), service.ToString()),
                refreshToken: "refreshToken")
        };
        return agent;
    }
}
