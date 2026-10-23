// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json;

using idunno.AtProto;
using idunno.AtProto.Authentication;
using idunno.AtProto.Labels;
using idunno.AtProto.Repo;
using idunno.Bluesky.Labeler;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;

namespace idunno.Bluesky.Integration.Test;

[ExcludeFromCodeCoverage]
public class LabelerDeclarationRequestTests
{
    private static readonly Did s_did = new("did:plc:hfgp6pj3akhqxntgqwramlbg");
    private static readonly Cid s_cid = new("bafyreigu3v6uxtmx3rv7emh4yvuvz2tom7ib7a4yl3x2eimard2brqeaei");
    private const string RecordResponse = """
        {"uri":"at://did:plc:hfgp6pj3akhqxntgqwramlbg/app.bsky.labeler.service/self",
         "cid":"bafyreigu3v6uxtmx3rv7emh4yvuvz2tom7ib7a4yl3x2eimard2brqeaei"}
        """;

    private static Service CreateDeclaration() => new()
    {
        CreatedAt = DateTimeOffset.Parse("2026-01-01T00:00:00Z", CultureInfo.InvariantCulture),
        Policies = new LabelerPolicies { LabelValues = ["spam"] },
        ReasonTypes = []
    };

    private static BlueskyAgent CreateAgent(TestServer server) => new(new TestHttpClientFactory(server))
    {
        Service = TestServerBuilder.DefaultUri,
        Credentials = new AccessCredentials(
            TestServerBuilder.DefaultUri, AuthenticationType.UsernamePassword,
            JwtBuilder.CreateJwt(s_did, TestServerBuilder.DefaultUri.ToString()), "refreshToken")
    };

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DeclarationWritesUseTheFixedKeyAndUpdatesRequireTheExpectedCid(bool update)
    {
        using TestServer server = TestServerBuilder.CreateServer(TestServerBuilder.DefaultUri, async context =>
        {
            Assert.Equal("POST", context.Request.Method);
            Assert.Equal(update ? "/xrpc/com.atproto.repo.putRecord" : "/xrpc/com.atproto.repo.createRecord", context.Request.Path);
            using JsonDocument body = await JsonDocument.ParseAsync(context.Request.Body, cancellationToken: TestContext.Current.CancellationToken);
            JsonElement root = body.RootElement;
            Assert.Equal(s_did.Value, root.GetProperty("repo").GetString());
            Assert.Equal(RecordType.LabelerDeclaration, root.GetProperty("collection").GetString());
            Assert.Equal("self", root.GetProperty("rkey").GetString());
            Assert.True(root.GetProperty("validate").GetBoolean());
            Assert.Equal(update, root.TryGetProperty("swapRecord", out JsonElement swap));
            if (update)
            {
                Assert.Equal(s_cid.Value, swap.GetString());
            }

            JsonElement record = root.GetProperty("record");
            Assert.Equal(RecordType.LabelerDeclaration, record.GetProperty("$type").GetString());
            Assert.Equal(0, record.GetProperty("reasonTypes").GetArrayLength());
            Assert.False(record.TryGetProperty("subjectCollections", out _));
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(RecordResponse, TestContext.Current.CancellationToken);
        });
        using BlueskyAgent agent = CreateAgent(server);

        if (update)
        {
            AtProtoHttpResult<PutRecordResult> result = await agent.UpdateLabelerDeclaration(
                CreateDeclaration(), s_cid, TestContext.Current.CancellationToken);
            Assert.True(result.Succeeded);
            Assert.Equal(s_cid, result.Result.Cid);
        }
        else
        {
            AtProtoHttpResult<CreateRecordResult> result = await agent.CreateLabelerDeclaration(
                CreateDeclaration(), TestContext.Current.CancellationToken);
            Assert.True(result.Succeeded);
            Assert.Equal(s_cid, result.Result.Cid);
        }
    }

    [Fact]
    public async Task DeclarationReadThenConditionalUpdatePreservesUnchangedFields()
    {
        int writes = 0;
        using TestServer server = TestServerBuilder.CreateServer(TestServerBuilder.DefaultUri, async context =>
        {
            context.Response.ContentType = "application/json";
            if (context.Request.Path == "/xrpc/com.atproto.repo.getRecord")
            {
                Assert.Equal("GET", context.Request.Method);
                Assert.Equal(s_did.Value, context.Request.Query["repo"]);
                Assert.Equal(RecordType.LabelerDeclaration, context.Request.Query["collection"]);
                Assert.Equal("self", context.Request.Query["rkey"]);
                await context.Response.WriteAsync(
                    $$$"""
                    {"uri":"at://{{{s_did}}}/app.bsky.labeler.service/self","cid":"{{{s_cid}}}",
                    "value":{"$type":"app.bsky.labeler.service","createdAt":"2026-01-01T00:00:00Z",
                    "policies":{"labelValues":["spam"]},"reasonTypes":["com.atproto.moderation.defs#reasonSpam"],
                    "subjectTypes":["record"],"subjectCollections":["app.bsky.feed.post"],
                    "labels":{"$type":"com.atproto.label.defs#selfLabels","values":[{"val":"!no-unauthenticated"}]},
                    "futureField":{"enabled":true}
                    }
                    }
                    """, TestContext.Current.CancellationToken);
            }
            else
            {
                Assert.Equal("/xrpc/com.atproto.repo.putRecord", context.Request.Path);
                writes++;
                using JsonDocument body = await JsonDocument.ParseAsync(context.Request.Body, cancellationToken: TestContext.Current.CancellationToken);
                Assert.Equal(s_cid.Value, body.RootElement.GetProperty("swapRecord").GetString());
                JsonElement record = body.RootElement.GetProperty("record");
                Assert.Equal("tools.ozone.report.defs#reasonOther", record.GetProperty("reasonTypes")[0].GetString());
                Assert.Equal("record", record.GetProperty("subjectTypes")[0].GetString());
                Assert.Equal("app.bsky.feed.post", record.GetProperty("subjectCollections")[0].GetString());
                Assert.Equal("spam", record.GetProperty("policies").GetProperty("labelValues")[0].GetString());
                Assert.Equal(CreateDeclaration().CreatedAt, record.GetProperty("createdAt").GetDateTimeOffset());
                Assert.True(record.GetProperty("futureField").GetProperty("enabled").GetBoolean());
                Assert.Equal("!no-unauthenticated", record.GetProperty("labels").GetProperty("values")[0].GetProperty("val").GetString());
                await context.Response.WriteAsync(RecordResponse, TestContext.Current.CancellationToken);
            }
        });
        using BlueskyAgent agent = CreateAgent(server);
        AtProtoHttpResult<AtProtoRepositoryRecord<Service>> read = await agent.GetLabelerDeclarationRecord(TestContext.Current.CancellationToken);
        Assert.True(read.Succeeded);
        Service edited = read.Result.Value with { ReasonTypes = ["tools.ozone.report.defs#reasonOther"] };

        AtProtoHttpResult<PutRecordResult> updated = await agent.UpdateLabelerDeclaration(
            edited, read.Result.Cid, TestContext.Current.CancellationToken);

        Assert.True(updated.Succeeded);
        Assert.Equal(1, writes);
    }

    [Fact]
    public async Task EnsureDeclarationIsIdempotentAndDoesNotWriteWhenMatchingRecordExists()
    {
        int writes = 0;
        using TestServer server = TestServerBuilder.CreateServer(TestServerBuilder.DefaultUri, async context =>
        {
            Assert.Equal("/xrpc/com.atproto.repo.getRecord", context.Request.Path);
            Assert.Equal("GET", context.Request.Method);
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(
                $$$"""
                {"uri":"at://{{{s_did}}}/app.bsky.labeler.service/self","cid":"{{{s_cid}}}",
                "value":{"$type":"app.bsky.labeler.service","createdAt":"2026-01-01T00:00:00Z",
                "policies":{"labelValues":["spam"]},"reasonTypes":[]}}
                """, TestContext.Current.CancellationToken);
        });
        using BlueskyAgent agent = CreateAgent(server);

        AtProtoHttpResult<AtProtoRepositoryRecord<Service>> result = await agent.EnsureLabelerDeclaration(
            CreateDeclaration(),
            TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Equal(s_cid, result.Result.Cid);
        Assert.Equal(0, writes);
    }

    [Fact]
    public async Task EnsureDeclarationDoesNotOverwriteDifferentExistingRecord()
    {
        using TestServer server = TestServerBuilder.CreateServer(TestServerBuilder.DefaultUri, async context =>
        {
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(
                $$$"""
                {"uri":"at://{{{s_did}}}/app.bsky.labeler.service/self","cid":"{{{s_cid}}}",
                "value":{"$type":"app.bsky.labeler.service","createdAt":"2026-01-01T00:00:00Z",
                "policies":{"labelValues":["different"]},"reasonTypes":[]}}
                """, TestContext.Current.CancellationToken);
        });
        using BlueskyAgent agent = CreateAgent(server);

        AtProtoHttpResult<AtProtoRepositoryRecord<Service>> result = await agent.EnsureLabelerDeclaration(
            CreateDeclaration(),
            TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Equal(System.Net.HttpStatusCode.Conflict, result.StatusCode);
        Assert.Equal("DeclarationConflict", result.AtErrorDetail!.Error);
    }

    [Theory]
    [InlineData(404, 200)]
    [InlineData(400, 200)]
    [InlineData(400, 400)]
    [InlineData(404, 409)]
    public async Task EnsureDeclarationCreatesMissingRecordAndVerifiesItAfterward(int missingStatus, int createStatus)
    {
        int writes = 0;
        int reads = 0;
        using TestServer server = TestServerBuilder.CreateServer(TestServerBuilder.DefaultUri, async context =>
        {
            context.Response.ContentType = "application/json";
            if (context.Request.Method == "GET")
            {
                reads++;
                if (reads == 1)
                {
                    context.Response.StatusCode = missingStatus;
                    await context.Response.WriteAsync(
                        """{"error":"RecordNotFound","message":"Missing"}""",
                        TestContext.Current.CancellationToken);
                    return;
                }

                await context.Response.WriteAsync(
                    $$$"""
                    {"uri":"at://{{{s_did}}}/app.bsky.labeler.service/self","cid":"{{{s_cid}}}",
                    "value":{"$type":"app.bsky.labeler.service","createdAt":"2026-01-01T00:00:00Z",
                    "policies":{"labelValues":["spam"]},"reasonTypes":[]}}
                    """, TestContext.Current.CancellationToken);
                return;
            }

            writes++;
            Assert.Equal("/xrpc/com.atproto.repo.createRecord", context.Request.Path);
            context.Response.StatusCode = createStatus;
            await context.Response.WriteAsync(
                createStatus == 200 ? RecordResponse : """{"error":"RecordAlreadyExists","message":"Concurrent setup"}""",
                TestContext.Current.CancellationToken);
        });
        using BlueskyAgent agent = CreateAgent(server);

        AtProtoHttpResult<AtProtoRepositoryRecord<Service>> result = await agent.EnsureLabelerDeclaration(
            CreateDeclaration(),
            TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Equal(s_cid, result.Result.Cid);
        Assert.Equal(1, writes);
        Assert.Equal(2, reads);
    }

    [Theory]
    [InlineData(400, false)]
    [InlineData(409, false)]
    [InlineData(400, true)]
    [InlineData(409, true)]
    public async Task EnsureDeclarationPreservesConcurrentConflictsAndOriginalCreateErrors(int createStatus, bool readFails)
    {
        int reads = 0;
        int writes = 0;
        using TestServer server = TestServerBuilder.CreateServer(TestServerBuilder.DefaultUri, async context =>
        {
            context.Response.ContentType = "application/json";
            if (context.Request.Method == "POST")
            {
                Assert.Equal("/xrpc/com.atproto.repo.createRecord", context.Request.Path);
                writes++;
                context.Response.StatusCode = createStatus;
                await context.Response.WriteAsync(
                    """{"error":"RecordAlreadyExists","message":"Concurrent setup"}""",
                    TestContext.Current.CancellationToken);
                return;
            }

            reads++;
            if (reads == 1 || readFails)
            {
                context.Response.StatusCode = 400;
                await context.Response.WriteAsync(
                    """{"error":"RecordNotFound","message":"Missing"}""",
                    TestContext.Current.CancellationToken);
                return;
            }

            await context.Response.WriteAsync(
                $$$"""
                {"uri":"at://{{{s_did}}}/app.bsky.labeler.service/self","cid":"{{{s_cid}}}",
                "value":{"$type":"app.bsky.labeler.service","createdAt":"2026-01-01T00:00:00Z",
                "policies":{"labelValues":["different"]},"reasonTypes":[]}}
                """,
                TestContext.Current.CancellationToken);
        });
        using BlueskyAgent agent = CreateAgent(server);

        AtProtoHttpResult<AtProtoRepositoryRecord<Service>> result = await agent.EnsureLabelerDeclaration(
            CreateDeclaration(),
            TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Equal(readFails ? createStatus : 409, (int)result.StatusCode);
        Assert.Equal(readFails ? "RecordAlreadyExists" : "DeclarationConflict", result.AtErrorDetail!.Error);
        if (readFails)
        {
            Assert.Equal("Concurrent setup", result.AtErrorDetail.Message);
        }

        Assert.Equal(2, reads);
        Assert.Equal(1, writes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EnsureDeclarationDoesNotRetryUnrelatedReadOrCreateErrors(bool createFails)
    {
        int requests = 0;
        using TestServer server = TestServerBuilder.CreateServer(TestServerBuilder.DefaultUri, async context =>
        {
            requests++;
            context.Response.ContentType = "application/json";
            context.Response.StatusCode = 400;
            string error = createFails && requests == 1 ? "RecordNotFound" : "InvalidRequest";
            await context.Response.WriteAsync(
                $$"""{"error":"{{error}}","message":"Rejected"}""",
                TestContext.Current.CancellationToken);
        });
        using BlueskyAgent agent = CreateAgent(server);

        AtProtoHttpResult<AtProtoRepositoryRecord<Service>> result = await agent.EnsureLabelerDeclaration(
            CreateDeclaration(),
            TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Equal("InvalidRequest", result.AtErrorDetail!.Error);
        Assert.Equal(createFails ? 2 : 1, requests);
    }

    [Theory]
    [InlineData(false, "RecordAlreadyExists")]
    [InlineData(true, "InvalidSwap")]
    public async Task DeclarationWriteErrorsAreReturnedWithoutRetry(bool update, string error)
    {
        int requests = 0;
        using TestServer server = TestServerBuilder.CreateServer(TestServerBuilder.DefaultUri, async context =>
        {
            requests++;
            context.Response.StatusCode = 400;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync($$"""{"error":"{{error}}","message":"Rejected"}""", TestContext.Current.CancellationToken);
        });
        using BlueskyAgent agent = CreateAgent(server);

        if (update)
        {
            AtProtoHttpResult<PutRecordResult> result = await agent.UpdateLabelerDeclaration(CreateDeclaration(), s_cid, TestContext.Current.CancellationToken);
            Assert.False(result.Succeeded);
            Assert.Equal(error, result.AtErrorDetail!.Error);
        }
        else
        {
            AtProtoHttpResult<CreateRecordResult> result = await agent.CreateLabelerDeclaration(CreateDeclaration(), TestContext.Current.CancellationToken);
            Assert.False(result.Succeeded);
            Assert.Equal(error, result.AtErrorDetail!.Error);
        }

        Assert.Equal(1, requests);
    }

    [Fact]
    public async Task DeclarationHelpersRequireAuthenticationAndNonNullInputs()
    {
        using BlueskyAgent agent = new();
        await Assert.ThrowsAsync<AuthenticationRequiredException>(() => agent.GetLabelerDeclarationRecord(TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<AuthenticationRequiredException>(() => agent.CreateLabelerDeclaration(CreateDeclaration(), TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<AuthenticationRequiredException>(() => agent.UpdateLabelerDeclaration(CreateDeclaration(), s_cid, TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ArgumentNullException>(() => agent.CreateLabelerDeclaration(null!, TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ArgumentNullException>(() => agent.UpdateLabelerDeclaration(null!, s_cid, TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ArgumentNullException>(() => agent.UpdateLabelerDeclaration(CreateDeclaration(), null!, TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("!warn")]
    [InlineData("Uppercase")]
    [InlineData("has space")]
    [InlineData("has_underscore")]
    [InlineData("")]
    public async Task PublishingRejectsInvalidCustomIdentifiersBeforeMakingARequest(string identifier)
    {
        Service declaration = CreateDeclaration() with
        {
            Policies = new LabelerPolicies
            {
                LabelValues = [identifier],
                LabelValueDefinitions = [new LabelValueDefinition
                {
                    Identifier = identifier, Severity = "inform", Blurs = "none",
                    Locales = [new LabelValueDefinitionStrings { Lang = "en", Name = "Name", Description = "Description" }]
                }]
            }
        };
        using BlueskyAgent agent = new();

        await Assert.ThrowsAnyAsync<ArgumentException>(() => agent.CreateLabelerDeclaration(declaration, TestContext.Current.CancellationToken));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => agent.UpdateLabelerDeclaration(declaration, s_cid, TestContext.Current.CancellationToken));
    }
}
