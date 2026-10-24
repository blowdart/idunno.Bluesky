// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Nodes;

using idunno.AtProto;
using idunno.AtProto.Authentication;
using idunno.AtProto.Labels;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;

namespace idunno.Bluesky.Integration.Test;

[ExcludeFromCodeCoverage]
public class PlcLabelerIdentityTests
{
    private static readonly Did s_did = new("did:plc:hfgp6pj3akhqxntgqwramlbg");
    private static readonly Uri s_endpoint = new("https://labels.example");

    private static BlueskyAgent CreateAgent(TestServer server) => new(new TestHttpClientFactory(server))
    {
        Service = TestServerBuilder.DefaultUri,
        Credentials = new AccessCredentials(
            TestServerBuilder.DefaultUri,
            AuthenticationType.UsernamePassword,
            JwtBuilder.CreateJwt(s_did, TestServerBuilder.DefaultUri.ToString()),
            "refreshToken")
    };

    [Theory]
    [InlineData(200, null)]
    [InlineData(400, "InvalidToken")]
    [InlineData(401, "AuthenticationRequired")]
    public async Task PlcAuthorizationRequestUsesAuthenticatedPostAndReturnsPdsErrors(int status, string? error)
    {
        int requests = 0;
        string token = JwtBuilder.CreateJwt(s_did, TestServerBuilder.DefaultUri.ToString());
        using TestServer server = TestServerBuilder.CreateServer(TestServerBuilder.DefaultUri, async context =>
        {
            requests++;
            Assert.Equal("POST", context.Request.Method);
            Assert.Equal("/xrpc/com.atproto.identity.requestPlcOperationSignature", context.Request.Path);
            Assert.Equal($"Bearer {token}", context.Request.Headers.Authorization.ToString());
            context.Response.StatusCode = status;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(
                error is null ? "{}" : $$"""{"error":"{{error}}","message":"Authorization denied"}""",
                TestContext.Current.CancellationToken);
        });
        using BlueskyAgent agent = CreateAgent(server);
        agent.Credentials = new AccessCredentials(
            TestServerBuilder.DefaultUri,
            AuthenticationType.UsernamePassword,
            token,
            "refreshToken");

        AtProtoHttpResult<EmptyResponse> result = await agent.RequestPlcOperationSignature(TestContext.Current.CancellationToken);

        Assert.Equal(status, (int)result.StatusCode);
        Assert.Equal(status == 200, result.Succeeded);
        Assert.Equal(error, result.AtErrorDetail?.Error);
        Assert.Equal(1, requests);
    }

    [Fact]
    public async Task PlcAuthorizationRequestRequiresAuthentication()
    {
        using BlueskyAgent agent = new();

        await Assert.ThrowsAsync<AuthenticationRequiredException>(() =>
            agent.RequestPlcOperationSignature(TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PlcIdentityUpdatePreservesExistingEntriesAndVerifiesPublishedDocument(bool legacy)
    {
        using LabelSigningKey signingKey = LabelSigningKey.Generate(LabelSigningCurve.P256);
        JsonObject? signedRequest = null;
        int documentReads = 0;
        int submissions = 0;
        using TestServer server = TestServerBuilder.CreateServer(TestServerBuilder.DefaultUri, async context =>
        {
            context.Response.ContentType = "application/json";
            if (context.Request.Path == $"/{s_did}/log/audit")
            {
                await WriteAuditLog(context, legacy);
                return;
            }

            if (context.Request.Path == $"/{s_did}")
            {
                documentReads++;
                if (documentReads == 1)
                {
                    await context.Response.WriteAsync(
                        $$"""
                        {"id":"{{s_did}}",
                        "verificationMethod":[{"id":"{{s_did}}#atproto","type":"Multikey","controller":"{{s_did}}","publicKeyMultibase":"zStale"}],
                        "service":[
                          {"id":"#atproto_pds","type":"AtprotoPersonalDataServer","serviceEndpoint":"https://pds.example"},
                          {"id":"#other_service","type":"OtherService","serviceEndpoint":"https://other.example"}]}
                        """,
                        TestContext.Current.CancellationToken);
                    return;
                }

                await WritePublishedDocument(context, signedRequest!);
                return;
            }

            Assert.Equal("POST", context.Request.Method);
            if (context.Request.Path == "/xrpc/com.atproto.identity.signPlcOperation")
            {
                using JsonDocument body = await JsonDocument.ParseAsync(
                    context.Request.Body,
                    cancellationToken: TestContext.Current.CancellationToken);
                signedRequest = JsonNode.Parse(body.RootElement.GetRawText())!.AsObject();

                JsonElement methods = body.RootElement.GetProperty("verificationMethods");
                Assert.Equal("did:key:zExisting", methods.GetProperty("atproto").GetString());
                Assert.Equal(
                    $"did:key:{signingKey.ExportPublicKeyMultibase()}",
                    methods.GetProperty("atproto_label").GetString());

                JsonElement services = body.RootElement.GetProperty("services");
                Assert.Equal("AtprotoPersonalDataServer", services.GetProperty("atproto_pds").GetProperty("type").GetString());
                Assert.Equal(!legacy, services.TryGetProperty("other_service", out JsonElement otherService));
                if (!legacy)
                {
                    Assert.Equal("OtherService", otherService.GetProperty("type").GetString());
                    Assert.True(otherService.GetProperty("futureField").GetBoolean());
                }
                Assert.Equal(
                    s_endpoint.GetLeftPart(UriPartial.Authority),
                    services.GetProperty("atproto_labeler").GetProperty("endpoint").GetString());

                await context.Response.WriteAsync(
                    new JsonObject { ["operation"] = CreateSignedOperation(signingKey, signedRequest) }.ToJsonString(),
                    TestContext.Current.CancellationToken);
                return;
            }

            Assert.Equal("/xrpc/com.atproto.identity.submitPlcOperation", context.Request.Path);
            submissions++;
            using JsonDocument submit = await JsonDocument.ParseAsync(
                context.Request.Body,
                cancellationToken: TestContext.Current.CancellationToken);
            Assert.Equal("signed-operation", submit.RootElement.GetProperty("operation").GetProperty("sig").GetString());
            Assert.Equal(
                "did:key:zExisting",
                submit.RootElement.GetProperty("operation").GetProperty("verificationMethods").GetProperty("atproto").GetString());
            await context.Response.WriteAsync("{}", TestContext.Current.CancellationToken);
        });
        using BlueskyAgent agent = CreateAgent(server);

        PlcLabelerIdentityUpdateResult result = await agent.UpdatePlcLabelerIdentity(
            signingKey,
            s_endpoint,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(PlcLabelerIdentityUpdateStage.Verified, result.Stage);
        Assert.Equal(2, documentReads);
        Assert.Equal(1, submissions);
        Assert.Null(result.RecoveryAction);
    }

    [Fact]
    public async Task PlcIdentityUpdateReturnsSignedOperationWhenSubmissionFails()
    {
        using LabelSigningKey signingKey = LabelSigningKey.Generate(LabelSigningCurve.P256);
        int submissions = 0;
        using TestServer server = TestServerBuilder.CreateServer(TestServerBuilder.DefaultUri, async context =>
        {
            context.Response.ContentType = "application/json";
            if (context.Request.Path == $"/{s_did}/log/audit")
            {
                await WriteAuditLog(context);
                return;
            }

            if (context.Request.Path == $"/{s_did}")
            {
                await context.Response.WriteAsync(
                    $$"""{"id":"{{s_did}}","verificationMethod":[],"service":[]}""",
                    TestContext.Current.CancellationToken);
                return;
            }

            if (context.Request.Path == "/xrpc/com.atproto.identity.signPlcOperation")
            {
                await context.Response.WriteAsync(
                    new JsonObject { ["operation"] = CreateSignedOperation(signingKey) }.ToJsonString(),
                    TestContext.Current.CancellationToken);
                return;
            }

            Assert.Equal("/xrpc/com.atproto.identity.submitPlcOperation", context.Request.Path);
            submissions++;
            if (submissions == 1)
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                await context.Response.WriteAsync(
                    """{"error":"InvalidOperation","message":"Rejected"}""",
                    TestContext.Current.CancellationToken);
            }
            else
            {
                await context.Response.WriteAsync("{}", TestContext.Current.CancellationToken);
            }
        });
        using BlueskyAgent agent = CreateAgent(server);

        PlcLabelerIdentityUpdateResult result = await agent.UpdatePlcLabelerIdentity(
            signingKey,
            s_endpoint,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(PlcLabelerIdentityUpdateStage.SubmissionFailed, result.Stage);
        Assert.Equal("signed-operation", result.SignedOperation!.Value.GetProperty("sig").GetString());
        Assert.NotNull(result.RecoveryAction);
        Assert.Equal("InvalidOperation", result.Error!.Error);

        AtProtoHttpResult<EmptyResponse> recovered = await agent.SubmitPlcLabelerIdentityOperation(
            result.SignedOperation.Value,
            TestContext.Current.CancellationToken);
        Assert.True(recovered.Succeeded);
        Assert.Equal(2, submissions);
    }

    [Fact]
    public async Task PlcIdentityUpdateSkipsSigningWhenAlreadyPublished()
    {
        using LabelSigningKey signingKey = LabelSigningKey.Generate(LabelSigningCurve.P256);
        using TestServer server = TestServerBuilder.CreateServer(TestServerBuilder.DefaultUri, async context =>
        {
            Assert.Equal($"/{s_did}", context.Request.Path);
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(
                $$"""
                {"id":"{{s_did}}",
                "verificationMethod":[{"id":"{{s_did}}#atproto_label","type":"Multikey","controller":"{{s_did}}","publicKeyMultibase":"{{signingKey.ExportPublicKeyMultibase()}}"}],
                "service":[{"id":"#atproto_labeler","type":"AtprotoLabeler","serviceEndpoint":"{{s_endpoint}}"}]}
                """,
                TestContext.Current.CancellationToken);
        });
        using BlueskyAgent agent = CreateAgent(server);

        PlcLabelerIdentityUpdateResult result = await agent.UpdatePlcLabelerIdentity(
            signingKey,
            s_endpoint,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(PlcLabelerIdentityUpdateStage.Verified, result.Stage);
        Assert.Null(result.SignedOperation);
    }

    [Fact]
    public async Task PlcIdentitySubmissionRejectsAnEndpointOutsideTheRequiredHttpsOrigin()
    {
        using LabelSigningKey signingKey = LabelSigningKey.Generate(LabelSigningCurve.P256);
        JsonObject signedOperation = CreateSignedOperation(signingKey);
        signedOperation["services"]!["atproto_labeler"]!["endpoint"] = "https://labels.example/path";
        using JsonDocument operation = JsonDocument.Parse(signedOperation.ToJsonString());
        using TestServer server = TestServerBuilder.CreateServer(TestServerBuilder.DefaultUri, _ =>
            throw new InvalidOperationException("The invalid operation must not be submitted."));
        using BlueskyAgent agent = CreateAgent(server);

        await Assert.ThrowsAsync<ArgumentException>(() => agent.SubmitPlcLabelerIdentityOperation(
            operation.RootElement,
            TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("https://operator@labels.example/", false)]
    [InlineData("https://operator:password@labels.example/", false)]
    [InlineData("https://labels.example/#unexpected", false)]
    [InlineData("https://operator@labels.example/", true)]
    [InlineData("https://operator:password@labels.example/", true)]
    [InlineData("https://labels.example/#unexpected", true)]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData(null, true)]
    [InlineData("", true)]
    public async Task PlcIdentityUpdateRejectsForbiddenPublishedEndpointComponents(string? invalidEndpoint, bool invalidAfterSubmission)
    {
        using LabelSigningKey signingKey = LabelSigningKey.Generate(LabelSigningCurve.P256);
        int reads = 0;
        int submissions = 0;
        using TestServer server = TestServerBuilder.CreateServer(TestServerBuilder.DefaultUri, async context =>
        {
            context.Response.ContentType = "application/json";
            if (context.Request.Path == $"/{s_did}/log/audit")
            {
                await WriteAuditLog(context);
            }
            else if (context.Request.Path == $"/{s_did}")
            {
                reads++;
                string? publishedEndpoint = (reads == 1 || invalidAfterSubmission) ? invalidEndpoint : s_endpoint.AbsoluteUri;
                JsonObject document = JsonNode.Parse(
                    $$"""
                    {"id":"{{s_did}}",
                    "verificationMethod":[{"id":"#atproto_label","type":"Multikey","controller":"{{s_did}}","publicKeyMultibase":"{{signingKey.ExportPublicKeyMultibase()}}"}],
                    "service":[{"id":"#atproto_labeler","type":"AtprotoLabeler","serviceEndpoint":"{{publishedEndpoint}}"}]}
                    """)!.AsObject();
                if (publishedEndpoint is null)
                {
                    document["service"]![0]!["serviceEndpoint"] = null;
                }
                else if (publishedEndpoint.Length == 0)
                {
                    document["service"]![0]!.AsObject().Remove("serviceEndpoint");
                }

                await context.Response.WriteAsync(document.ToJsonString(), TestContext.Current.CancellationToken);
            }
            else if (context.Request.Path == "/xrpc/com.atproto.identity.signPlcOperation")
            {
                await context.Response.WriteAsync(
                    new JsonObject { ["operation"] = CreateSignedOperation(signingKey) }.ToJsonString(),
                    TestContext.Current.CancellationToken);
            }
            else
            {
                Assert.Equal("/xrpc/com.atproto.identity.submitPlcOperation", context.Request.Path);
                submissions++;
                await context.Response.WriteAsync("{}", TestContext.Current.CancellationToken);
            }
        });
        using BlueskyAgent agent = CreateAgent(server);

        PlcLabelerIdentityUpdateResult result = await agent.UpdatePlcLabelerIdentity(
            signingKey,
            s_endpoint,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(
            invalidAfterSubmission ? PlcLabelerIdentityUpdateStage.VerificationFailed : PlcLabelerIdentityUpdateStage.Verified,
            result.Stage);
        Assert.Equal("signed-operation", result.SignedOperation!.Value.GetProperty("sig").GetString());
        Assert.Equal(1, submissions);
        Assert.Equal(2, reads);
        if (invalidAfterSubmission)
        {
            Assert.Equal("PublicationUnverified", result.Error!.Error);
            Assert.NotNull(result.RecoveryAction);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PlcIdentityUpdateNeverSubmitsMapsSignedAgainstANewerVersion(bool missingPrevious)
    {
        using LabelSigningKey signingKey = LabelSigningKey.Generate(LabelSigningCurve.P256);
        using TestServer server = TestServerBuilder.CreateServer(TestServerBuilder.DefaultUri, async context =>
        {
            context.Response.ContentType = "application/json";
            if (context.Request.Path == $"/{s_did}/log/audit")
            {
                await WriteAuditLog(context);
            }
            else if (context.Request.Path == $"/{s_did}")
            {
                await context.Response.WriteAsync(
                    $$"""{"id":"{{s_did}}","verificationMethod":[],"service":[]}""",
                    TestContext.Current.CancellationToken);
            }
            else
            {
                Assert.Equal("/xrpc/com.atproto.identity.signPlcOperation", context.Request.Path);
                JsonObject operation = CreateSignedOperation(signingKey);
                if (missingPrevious)
                {
                    operation.Remove("prev");
                }
                else
                {
                    operation["prev"] = "newer-version";
                }

                await context.Response.WriteAsync(
                    new JsonObject { ["operation"] = operation }.ToJsonString(),
                    TestContext.Current.CancellationToken);
            }
        });
        using BlueskyAgent agent = CreateAgent(server);

        PlcLabelerIdentityUpdateResult result = await agent.UpdatePlcLabelerIdentity(
            signingKey,
            s_endpoint,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(PlcLabelerIdentityUpdateStage.SigningFailed, result.Stage);
        Assert.Equal("PlcStateChanged", result.Error!.Error);
        Assert.Null(result.SignedOperation);
        Assert.NotNull(result.RecoveryAction);
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, false, false)]
    [InlineData(true, true, false)]
    [InlineData(false, false, true)]
    [InlineData(true, false, true)]
    public async Task PlcIdentityUpdateRetainsRecoveryDataOnTransportFailure(bool verification, bool timeout, bool callerCancellation)
    {
        using LabelSigningKey signingKey = LabelSigningKey.Generate(LabelSigningCurve.P256);
        using CancellationTokenSource cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        int reads = 0;
        using TestServer server = TestServerBuilder.CreateServer(TestServerBuilder.DefaultUri, async context =>
        {
            context.Response.ContentType = "application/json";
            if (context.Request.Path == $"/{s_did}/log/audit")
            {
                await WriteAuditLog(context);
                return;
            }

            if (context.Request.Path == $"/{s_did}" && ++reads == 1)
            {
                await context.Response.WriteAsync(
                    $$"""{"id":"{{s_did}}","verificationMethod":[],"service":[]}""",
                    TestContext.Current.CancellationToken);
                return;
            }

            if (context.Request.Path == "/xrpc/com.atproto.identity.signPlcOperation")
            {
                await context.Response.WriteAsync(
                    new JsonObject { ["operation"] = CreateSignedOperation(signingKey) }.ToJsonString(),
                    TestContext.Current.CancellationToken);
                return;
            }

            if (verification && context.Request.Path == "/xrpc/com.atproto.identity.submitPlcOperation")
            {
                await context.Response.WriteAsync("{}", TestContext.Current.CancellationToken);
                return;
            }

            if (callerCancellation)
            {
                cancellation.Cancel();
                throw new OperationCanceledException(cancellation.Token);
            }

            if (timeout)
            {
                throw new TaskCanceledException("Injected transport timeout.");
            }

            throw new HttpRequestException("Injected connection loss.");
        });
        using BlueskyAgent agent = CreateAgent(server);

        if (callerCancellation)
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => agent.UpdatePlcLabelerIdentity(
                signingKey,
                s_endpoint,
                cancellationToken: cancellation.Token));
            return;
        }

        PlcLabelerIdentityUpdateResult result = await agent.UpdatePlcLabelerIdentity(
            signingKey,
            s_endpoint,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(
            verification ? PlcLabelerIdentityUpdateStage.VerificationFailed : PlcLabelerIdentityUpdateStage.SubmissionFailed,
            result.Stage);
        Assert.Equal("signed-operation", result.SignedOperation!.Value.GetProperty("sig").GetString());
        Assert.NotNull(result.Error);
        Assert.NotNull(result.RecoveryAction);
    }

    [Theory]
    [InlineData("verificationMethod", "null")]
    [InlineData("service", "null")]
    [InlineData("verificationMethod", """{"type":"Multikey","controller":"did:plc:hfgp6pj3akhqxntgqwramlbg"}""")]
    [InlineData("service", """{"type":"AtprotoLabeler","serviceEndpoint":"https://labels.example"}""")]
    [InlineData("verificationMethod", """{"id":null,"type":"Multikey","controller":"did:plc:hfgp6pj3akhqxntgqwramlbg"}""")]
    [InlineData("service", """{"id":null,"type":"AtprotoLabeler","serviceEndpoint":"https://labels.example"}""")]
    [InlineData("verificationMethod", """{"id":"","type":"Multikey","controller":"did:plc:hfgp6pj3akhqxntgqwramlbg"}""")]
    [InlineData("service", """{"id":"","type":"AtprotoLabeler","serviceEndpoint":"https://labels.example"}""")]
    [InlineData("id", "null")]
    public async Task PlcIdentityUpdateRetainsSignedOperationWhenPublishedDocumentContainsMalformedEntries(string collection, string malformedEntry)
    {
        using LabelSigningKey signingKey = LabelSigningKey.Generate(LabelSigningCurve.P256);
        int reads = 0;
        using TestServer server = TestServerBuilder.CreateServer(TestServerBuilder.DefaultUri, async context =>
        {
            context.Response.ContentType = "application/json";
            if (context.Request.Path == $"/{s_did}/log/audit")
            {
                await WriteAuditLog(context);
            }
            else if (context.Request.Path == $"/{s_did}")
            {
                reads++;
                JsonObject document = new()
                {
                    ["id"] = s_did.Value,
                    ["verificationMethod"] = new JsonArray(),
                    ["service"] = new JsonArray()
                };
                if (reads > 1)
                {
                    document["verificationMethod"]!.AsArray().Add(new JsonObject
                    {
                        ["id"] = $"{s_did}#atproto_label",
                        ["type"] = "Multikey",
                        ["controller"] = s_did.Value,
                        ["publicKeyMultibase"] = signingKey.ExportPublicKeyMultibase()
                    });
                    document["service"]!.AsArray().Add(new JsonObject
                    {
                        ["id"] = "#atproto_labeler",
                        ["type"] = "AtprotoLabeler",
                        ["serviceEndpoint"] = s_endpoint.AbsoluteUri
                    });
                    if (collection == "id")
                    {
                        document["id"] = null;
                    }
                    else
                    {
                        document[collection]!.AsArray().Add(JsonNode.Parse(malformedEntry));
                    }
                }

                await context.Response.WriteAsync(document.ToJsonString(), TestContext.Current.CancellationToken);
            }
            else if (context.Request.Path == "/xrpc/com.atproto.identity.signPlcOperation")
            {
                await context.Response.WriteAsync(
                    new JsonObject { ["operation"] = CreateSignedOperation(signingKey) }.ToJsonString(),
                    TestContext.Current.CancellationToken);
            }
            else
            {
                Assert.Equal("/xrpc/com.atproto.identity.submitPlcOperation", context.Request.Path);
                await context.Response.WriteAsync("{}", TestContext.Current.CancellationToken);
            }
        });
        using BlueskyAgent agent = CreateAgent(server);

        PlcLabelerIdentityUpdateResult result = await agent.UpdatePlcLabelerIdentity(
            signingKey,
            s_endpoint,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(PlcLabelerIdentityUpdateStage.VerificationFailed, result.Stage);
        Assert.Equal("signed-operation", result.SignedOperation!.Value.GetProperty("sig").GetString());
        Assert.Equal("PublicationUnverified", result.Error!.Error);
        Assert.NotNull(result.RecoveryAction);
        Assert.Equal(2, reads);
    }

    /// <summary>Returns a versioned PLC snapshot containing unrelated data and a later nullified fork.</summary>
    private static async Task WriteAuditLog(HttpContext context, bool legacy = false)
    {
        if (legacy)
        {
            await context.Response.WriteAsync(
                $$$$$"""
                [{"did":"{{{{{s_did}}}}}","cid":"original-version","nullified":false,
                  "operation":{"type":"create","signingKey":"did:key:zExisting","service":"https://pds.example"}}]
                """,
                TestContext.Current.CancellationToken);
            return;
        }

        await context.Response.WriteAsync(
            $$$$$"""
            [{"did":"{{{{{s_did}}}}}","cid":"original-version","nullified":false,
              "operation":{"type":"plc_operation",
                "verificationMethods":{"atproto":"did:key:zExisting"},
                "services":{"atproto_pds":{"type":"AtprotoPersonalDataServer","endpoint":"https://pds.example"},
                  "other_service":{"type":"OtherService","endpoint":"https://other.example","futureField":true}}}},
             {"did":"{{{{{s_did}}}}}","cid":"nullified-version","nullified":true,"operation":{"type":"plc_tombstone"}}]
            """,
            TestContext.Current.CancellationToken);
    }

    private static async Task WritePublishedDocument(HttpContext context, JsonObject signedRequest)
    {
        JsonArray verificationMethods = [];
        JsonObject requestedMethods = signedRequest["verificationMethods"]!.AsObject();
        foreach ((string name, JsonNode? node) in requestedMethods)
        {
            string didKey = node!.GetValue<string>();
            verificationMethods.Add(new JsonObject
            {
                ["id"] = $"{s_did}#{name}",
                ["type"] = "Multikey",
                ["controller"] = s_did.ToString(),
                ["publicKeyMultibase"] = didKey["did:key:".Length..]
            });
        }

        JsonArray services = [];
        JsonObject requestedServices = signedRequest["services"]!.AsObject();
        foreach ((string name, JsonNode? node) in requestedServices)
        {
            JsonObject service = node!.AsObject();
            services.Add(new JsonObject
            {
                ["id"] = $"#{name}",
                ["type"] = service["type"]!.GetValue<string>(),
                ["serviceEndpoint"] = service["endpoint"]!.GetValue<string>()
            });
        }

        JsonObject document = new()
        {
            ["id"] = s_did.ToString(),
            ["verificationMethod"] = verificationMethods,
            ["service"] = services
        };
        await context.Response.WriteAsync(document.ToJsonString(), TestContext.Current.CancellationToken);
    }

    private static JsonObject CreateSignedOperation(LabelSigningKey signingKey, JsonObject? request = null) =>
        new()
        {
            ["type"] = "plc_operation",
            ["prev"] = "original-version",
            ["sig"] = "signed-operation",
            ["verificationMethods"] = request?["verificationMethods"]?.DeepClone() ?? new JsonObject
            {
                ["atproto_label"] = $"did:key:{signingKey.ExportPublicKeyMultibase()}"
            },
            ["services"] = request?["services"]?.DeepClone() ?? new JsonObject
            {
                ["atproto_labeler"] = new JsonObject
                {
                    ["type"] = "AtprotoLabeler",
                    ["endpoint"] = s_endpoint.GetLeftPart(UriPartial.Authority)
                }
            }
        };
}
