// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.CommandLine;
using System.Net;
using System.Text;
using System.Text.Json;

using idunno.AtProto;
using idunno.AtProto.Authentication;
using idunno.Bluesky.Labeler.Moderation;

using Microsoft.Extensions.DependencyInjection;

namespace idunno.Bluesky.Labeler.Cli.Test;

public class PolicyCliTests
{
    private const string Issuer = "did:plc:labeler";
    private const string RecordCid = "bafyreievgu2ty7qbiaaom5zhmkznsnajuzideek3lo7e65dwqlrvrxnmo4";
    private const string Declaration = """
        {
          "$type":"app.bsky.labeler.service",
          "createdAt":"2026-01-01T00:00:00+00:00",
          "policies":{
            "labelValues":["custom-spam"],
            "labelValueDefinitions":[{
              "identifier":"custom-spam","blurs":"content","severity":"alert",
              "locales":[{"lang":"en","name":"Spam","description":"Unwanted messages."}],
              "futureDefinitionField":{"preserve":true}
            }],
            "futurePolicyField":[1,2]
          },
          "subjectTypes":["account","record"],
          "futureField":{"preserve":true}
        }
        """;

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void PublicationSignInRequestsOnlyTheSelectedRecordWriteAction(bool create)
    {
        Uri metadata = new("https://labeler.example/native-client-metadata.json");
        OAuthOptions options = Program.CreatePolicyOAuthOptions(metadata, create);
        Assert.Equal(metadata.AbsoluteUri, options.ClientId);
        Assert.Equal(["atproto", $"repo:app.bsky.labeler.service?action={(create ? "create" : "update")}"], options.Scopes);
    }

    [Theory]
    [InlineData("Publishing", "publication", Issuer, "v1", null, true)]
    [InlineData("VerifiedActive", "publication", Issuer, "v1", null, true)]
    [InlineData("Conflict", "publication", Issuer, "v1", null, false)]
    [InlineData("Unverified", "publication", Issuer, "v1", null, false)]
    [InlineData("Publishing", "replacement", Issuer, "v1", null, false)]
    [InlineData("Publishing", null, Issuer, "v1", null, false)]
    [InlineData("Publishing", "publication", "did:plc:other", "v1", null, false)]
    [InlineData("Publishing", "publication", Issuer, "v2", null, false)]
    [InlineData("Publishing", "publication", Issuer, "v1", RecordCid, false)]
    public void RecoveryRequiresTheExactLivePublicationNotJustTheSameVersion(
        string phase,
        string? operationId,
        string issuer,
        string version,
        string? previousCid,
        bool expected)
    {
        Program.PolicyDefinition definition = Definition();
        Program.PolicyState intent = new(
            Issuer: Issuer, Phase: "Publishing", PublicationOperationId: "publication", Definition: definition);
        Program.PolicyState current = intent with
        {
            Phase = phase,
            PublicationOperationId = operationId,
            Issuer = issuer,
            Definition = definition with { Version = version },
            PreviousCid = previousCid
        };
        Assert.Equal(expected, Program.MatchesPolicyPublication(current, intent));
        Assert.False(Program.MatchesPolicyPublication(current, intent with { PublicationOperationId = null }));
        Assert.False(Program.MatchesPolicyPublication(current with { Definition = null }, intent));
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, false)]
    [InlineData(true, true)]
    [InlineData(false, true)]
    public async Task ConditionalPublicationUsesGeneratedEnvelopesAndPreservesExactDeclaration(bool create, bool conflict)
    {
        using PublicationHandler handler = new(create, conflict);
        ServiceCollection services = new();
        services.AddHttpClient("idunno.atproto").ConfigurePrimaryHttpMessageHandler(() => handler);
        using ServiceProvider provider = services.BuildServiceProvider();
        DateTimeOffset instant = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        Uri pds = new("https://pds.example");
        using PublicationAgent labeler = new(
            service: pds,
            httpClientFactory: provider.GetRequiredService<IHttpClientFactory>());
        string header = Convert.ToBase64String(Encoding.UTF8.GetBytes("""{"alg":"none"}""")).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        string claims = Convert.ToBase64String(Encoding.UTF8.GetBytes(
            $$"""{"sub":"{{Issuer}}","exp":{{instant.AddHours(1).ToUnixTimeSeconds()}}}"""))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
        labeler.Credentials = new(
            service: pds, authenticationType: AuthenticationType.UsernamePassword,
            accessJwt: $"{header}.{claims}.", refreshToken: "test-refresh");
        (bool succeeded, HttpStatusCode status, string? error) = await Program.WritePolicyRecordAsync(
            labeler: labeler, policy: Definition().ToPolicy(), previousCid: create ? null : RecordCid,
            cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(!conflict, succeeded);
        Assert.Equal(conflict ? HttpStatusCode.BadRequest : HttpStatusCode.OK, status);
        Assert.Equal(conflict ? "InvalidSwap" : null, error);
        Assert.Equal(1, handler.Writes);
    }

    [Theory]
    [InlineData("status")]
    [InlineData("version", "--version", "v1")]
    [InlineData("versions", "--after-version", "v1", "--limit", "8")]
    [InlineData("history", "--after-sequence", "1", "--limit", "8")]
    [InlineData("operation", "--operation-id", "publication")]
    [InlineData("save", "--file", "policy.json", "--reason", "Reviewed", "--operation-id", "draft", "--yes")]
    [InlineData("begin", "--version", "v1", "--expected-revision", "1", "--create", "--reason", "Reviewed", "--operation-id", "publication", "--yes")]
    [InlineData("verify", "--reason", "Reviewed", "--yes")]
    [InlineData("abandon", "--expected-revision", "2", "--reason", "Reviewed", "--operation-id", "abandon", "--yes")]
    public void ExplicitPolicyCommandsParseWithoutGenericMethodOrCallerOverrides(params string[] arguments)
    {
        RootCommand root = new();
        root.Subcommands.Add(Program.CreatePolicyCommand());
        Assert.Empty(root.Parse(["policy", .. arguments]).Errors);
        Assert.NotEmpty(root.Parse(["policy", .. arguments, "--method", "changeTeam"]).Errors);
        Assert.NotEmpty(root.Parse(["policy", .. arguments, "--caller", Issuer]).Errors);
    }

    [Theory]
    [InlineData("getPolicy")]
    [InlineData("getPolicyVersion")]
    [InlineData("getPolicyVersions")]
    [InlineData("getPolicyHistory")]
    [InlineData("getPolicyOperation")]
    public void ConfidentialPolicyReadsRemainPostRequests(string method)
    {
        Assert.False(Program.IsRemoteGetMethod(method));
        Uri uri = Program.BuildRemoteRequestUri(
            adminUrl: new("https://labeler.example"), lxm: $"dev.idunno.labeler.admin.{method}",
            method: method, operationId: "confidential", reason: "confidential", afterSequence: 1, limit: 8);
        Assert.Empty(uri.Query);
    }

    /// <summary>Builds complete intent with omitted defaults and unknown nested fields.</summary>
    private static Program.PolicyDefinition Definition()
    {
        using JsonDocument document = JsonDocument.Parse(Declaration);
        return new(
            Version: "v1", Declaration: document.RootElement.Clone(),
            AllowAccounts: true, AllowRecords: true, Rules: "Reviewed moderation policy.");
    }

    /// <summary>Supplies explicit authentication for transport-only tests without evaluating credential expiry.</summary>
    /// <param name="service">The test PDS.</param>
    /// <param name="httpClientFactory">The configured request-capture factory.</param>
    private sealed class PublicationAgent(Uri service, IHttpClientFactory httpClientFactory)
        : AtProtoAgent(service, httpClientFactory, new() { EnableBackgroundTokenRefresh = false })
    {
        /// <inheritdoc/>
        public override bool IsAuthenticated => Credentials is not null && Did is not null;
    }

    /// <summary>Checks the actual SDK request and supplies deterministic conditional-write outcomes.</summary>
    private sealed class PublicationHandler(bool create, bool conflict) : HttpMessageHandler
    {
        /// <summary>Gets the number of writes actually attempted.</summary>
        public int Writes { get; private set; }

        /// <inheritdoc/>
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Writes++;
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal(create ? "/xrpc/com.atproto.repo.createRecord" : "/xrpc/com.atproto.repo.putRecord", request.RequestUri!.AbsolutePath);
            using JsonDocument body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));
            Assert.Equal(Issuer, body.RootElement.GetProperty("repo").GetString());
            Assert.Equal("app.bsky.labeler.service", body.RootElement.GetProperty("collection").GetString());
            Assert.Equal("self", body.RootElement.GetProperty("rkey").GetString());
            Assert.True(body.RootElement.GetProperty("validate").GetBoolean());
            Assert.False(body.RootElement.TryGetProperty("swapCommit", out _));
            if (create)
            {
                Assert.False(body.RootElement.TryGetProperty("swapRecord", out _));
            }
            else
            {
                Assert.Equal(RecordCid, body.RootElement.GetProperty("swapRecord").GetString());
            }

            JsonElement record = body.RootElement.GetProperty("record");
            Assert.Equal("app.bsky.labeler.service", record.GetProperty("$type").GetString());
            Assert.Equal("2026-01-01T00:00:00+00:00", record.GetProperty("createdAt").GetString());
            Assert.True(record.GetProperty("futureField").GetProperty("preserve").GetBoolean());
            JsonElement policies = record.GetProperty("policies");
            Assert.Equal(2, policies.GetProperty("futurePolicyField").GetArrayLength());
            JsonElement definition = policies.GetProperty("labelValueDefinitions")[0];
            Assert.False(definition.TryGetProperty("defaultSetting", out _));
            Assert.True(definition.GetProperty("futureDefinitionField").GetProperty("preserve").GetBoolean());
            return new(conflict ? HttpStatusCode.BadRequest : HttpStatusCode.OK)
            {
                Content = new StringContent(conflict
                    ? """{"error":"InvalidSwap","message":"Concurrent conditional update."}"""
                    : $$"""{"uri":"at://{{Issuer}}/app.bsky.labeler.service/self","cid":"{{RecordCid}}"}""",
                    Encoding.UTF8,
                    "application/json")
            };
        }
    }
}
