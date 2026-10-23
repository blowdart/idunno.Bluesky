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

    [Fact]
    public async Task PlcIdentityUpdatePreservesExistingEntriesAndVerifiesPublishedDocument()
    {
        using LabelSigningKey signingKey = LabelSigningKey.Generate(LabelSigningCurve.P256);
        JsonObject? signedRequest = null;
        int documentReads = 0;
        int submissions = 0;
        using TestServer server = TestServerBuilder.CreateServer(TestServerBuilder.DefaultUri, async context =>
        {
            context.Response.ContentType = "application/json";
            if (context.Request.Path == $"/{s_did}")
            {
                documentReads++;
                if (documentReads == 1)
                {
                    await context.Response.WriteAsync(
                        $$"""
                        {"id":"{{s_did}}",
                        "verificationMethod":[{"id":"{{s_did}}#atproto","type":"Multikey","controller":"{{s_did}}","publicKeyMultibase":"zExisting"}],
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
                Assert.Equal("OtherService", services.GetProperty("other_service").GetProperty("type").GetString());
                Assert.Equal(
                    s_endpoint.GetLeftPart(UriPartial.Authority),
                    services.GetProperty("atproto_labeler").GetProperty("endpoint").GetString());

                await context.Response.WriteAsync(
                    new JsonObject { ["operation"] = CreateSignedOperation(signingKey) }.ToJsonString(),
                    TestContext.Current.CancellationToken);
                return;
            }

            Assert.Equal("/xrpc/com.atproto.identity.submitPlcOperation", context.Request.Path);
            submissions++;
            using JsonDocument submit = await JsonDocument.ParseAsync(
                context.Request.Body,
                cancellationToken: TestContext.Current.CancellationToken);
            Assert.Equal("signed-operation", submit.RootElement.GetProperty("operation").GetProperty("sig").GetString());
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

    private static JsonObject CreateSignedOperation(LabelSigningKey signingKey) =>
        new()
        {
            ["type"] = "plc_operation",
            ["sig"] = "signed-operation",
            ["verificationMethods"] = new JsonObject
            {
                ["atproto_label"] = $"did:key:{signingKey.ExportPublicKeyMultibase()}"
            },
            ["services"] = new JsonObject
            {
                ["atproto_labeler"] = new JsonObject
                {
                    ["type"] = "AtprotoLabeler",
                    ["endpoint"] = s_endpoint.GetLeftPart(UriPartial.Authority)
                }
            }
        };
}
