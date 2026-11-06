// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Net;
using System.Security.Cryptography;
using System.Text.Json;

using idunno.AtProto;
using idunno.AtProto.Authentication;
using idunno.AtProto.Labels;
using idunno.Bluesky.Labeler;
using idunno.Bluesky.Labeler.Agent;
using idunno.Bluesky.Labeler.Agent.Errors;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Time.Testing;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace idunno.Bluesky.Labeler.Agent.Test;

public sealed class LabelerAgentTests
{
    private const string AccountDid = "did:plc:ec72yg6n2sydzjvtovvdlxrk";
    private static readonly FakeTimeProvider s_clock = new(new DateTimeOffset(2035, 1, 1, 0, 0, 0, TimeSpan.Zero));
    private static readonly Uri s_pds = new("https://pds.test.invalid");
    private static readonly Uri s_labeler = new("https://labeler.test.invalid");
    private static readonly AtProtoServiceReference s_audience = new(new Did("did:web:labeler.test.invalid"), Audience.Issuance);

    [Fact]
    public void WithAccountAgentRejectsAnUnauthenticatedAgent()
    {
        using TestServerFixture server = CreateServer();
        using AtProtoAgent accountAgent = CreateAccountAgent(server.Server);

        Assert.Throws<AuthenticationRequiredException>(() => CreateBuilder(server.Server).WithAccountAgent(accountAgent));
    }

    [Fact]
    public async Task OwnedAccountAgentRequiresAuthenticationForAdministrativeCalls()
    {
        using TestServerFixture server = CreateServer();
        using LabelerAgent labeler = CreateBuilder(server.Server).Build();

        await Assert.ThrowsAsync<AuthenticationRequiredException>(() => labeler.GetAvailableLabels(
            cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DisposingLabelerAgentDisposesItsOwnedAccountAgent()
    {
        using TestServerFixture server = CreateServer();
        LabelerAgent labeler = CreateBuilder(server.Server).Build();
        await Login(labeler.AccountAgent);

        labeler.Dispose();

        Assert.False(labeler.AccountAgent.IsAuthenticated);
    }

    [Fact]
    public async Task DisposingLabelerAgentLeavesItsBorrowedAccountAgentAuthenticated()
    {
        using TestServerFixture server = CreateServer();
        using AtProtoAgent accountAgent = CreateAccountAgent(server.Server);
        await Login(accountAgent);
        LabelerAgent labeler = LabelerAgentBuilder.Create()
            .WithLabelerService(s_labeler, s_audience.Did)
            .WithAccountAgent(accountAgent)
            .Build();

        labeler.Dispose();

        Assert.True(accountAgent.IsAuthenticated);
    }

    [Fact]
    public async Task IssueLabelRequestsMethodBoundServiceAuthAndSendsOnlyThatCredentialToLabeler()
    {
        string? serviceAuthAudience = null;
        string? serviceAuthMethod = null;
        string? serviceAuthAuthorization = null;
        string? labelerAuthorization = null;
        string? labelerRequestUri = null;
        JsonElement? labelerCommand = null;
        string serviceJwt = CreateJwt(null, s_audience.ToString());
        using TestServerFixture server = CreateServer(async context =>
        {
            if (context.Request.Host.Host == s_labeler.Host)
            {
                labelerRequestUri = context.Request.Path + context.Request.QueryString;
            }

            if (context.Request.Host.Host == s_pds.Host &&
                context.Request.Path == "/xrpc/com.atproto.server.getServiceAuth")
            {
                serviceAuthAudience = context.Request.Query["aud"].ToString();
                serviceAuthMethod = context.Request.Query["lxm"].ToString();
                serviceAuthAuthorization = context.Request.Headers.Authorization.ToString();
                await context.Response.WriteAsJsonAsync(new { token = serviceJwt });
                return;
            }

            if (context.Request.Host.Host == s_labeler.Host &&
                context.Request.Path == "/xrpc/dev.idunno.labeler.issuance.issueLabel")
            {
                labelerAuthorization = context.Request.Headers.Authorization.ToString();
                using JsonDocument command = JsonDocument.Parse(await new StreamReader(context.Request.Body).ReadToEndAsync());
                labelerCommand = command.RootElement.Clone();
                await context.Response.WriteAsJsonAsync(new
                {
                    operationId = "operation-1",
                    sequence = 42,
                    changed = true
                });
                return;
            }

            context.Response.StatusCode = StatusCodes.Status404NotFound;
        });

        using LabelerAgent labeler = CreateBuilder(server.Server).Build();
        await Login(labeler.AccountAgent);
        AtProtoHttpResult<LabelOperationReceipt> result = await labeler.IssueLabel(
            operationId: "operation-1",
            target: LabelTarget.Account(new Did(AccountDid)),
            value: "spam",
            reason: "Automated review",
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(
            result.Succeeded,
            $"Status: {result.StatusCode}; error: {result.AtErrorDetail?.Error}; message: {result.AtErrorDetail?.Message}; " +
            $"service auth method: {serviceAuthMethod}; labeler request: {labelerRequestUri}");
        Assert.Equal("operation-1", result.Result.OperationId);
        Assert.Equal(42, result.Result.Sequence);
        Assert.True(result.Result.Changed);
        Assert.Equal(s_audience.ToString(), serviceAuthAudience);
        Assert.Equal("dev.idunno.labeler.issuance.issueLabel", serviceAuthMethod);
        Assert.StartsWith("Bearer ", serviceAuthAuthorization);
        Assert.Equal($"Bearer {serviceJwt}", labelerAuthorization);
        Assert.NotEqual(serviceAuthAuthorization, labelerAuthorization);
        Assert.Equal("operation-1", labelerCommand!.Value.GetProperty("operationId").GetString());
        Assert.Equal(AccountDid, labelerCommand.Value.GetProperty("target").GetString());
    }

    [Fact]
    public async Task IssueAndNegateWithoutOperationIdsGenerateDistinctGuids()
    {
        Dictionary<string, string> operationIds = [];
        string serviceJwt = CreateJwt(null, s_audience.ToString());
        using TestServerFixture server = CreateServer(async context =>
        {
            if (context.Request.Host.Host == s_pds.Host &&
                context.Request.Path == "/xrpc/com.atproto.server.getServiceAuth")
            {
                await context.Response.WriteAsJsonAsync(new { token = serviceJwt });
                return;
            }

            if (context.Request.Host.Host == s_labeler.Host &&
                context.Request.Path.Value is "/xrpc/dev.idunno.labeler.issuance.issueLabel" or
                    "/xrpc/dev.idunno.labeler.issuance.negateLabel")
            {
                using JsonDocument command = JsonDocument.Parse(await new StreamReader(context.Request.Body).ReadToEndAsync());
                string operationId = command.RootElement.GetProperty("operationId").GetString()
                    ?? throw new InvalidDataException("The generated operation ID was missing.");
                operationIds.Add(context.Request.Path.Value!, operationId);
                await context.Response.WriteAsJsonAsync(new
                {
                    operationId,
                    sequence = 42,
                    changed = true
                });
                return;
            }

            context.Response.StatusCode = StatusCodes.Status404NotFound;
        });

        using LabelerAgent labeler = CreateBuilder(server.Server).Build();
        await Login(labeler.AccountAgent);
        LabelTarget target = LabelTarget.Account(new Did(AccountDid));

        AtProtoHttpResult<LabelOperationReceipt> issueResult = await labeler.IssueLabel(
            target: target,
            value: "spam",
            reason: "Automated review",
            cancellationToken: TestContext.Current.CancellationToken);
        AtProtoHttpResult<LabelOperationReceipt> negateResult = await labeler.NegateLabel(
            target: target,
            value: "spam",
            reason: "Review outcome changed",
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(issueResult.Succeeded);
        Assert.True(negateResult.Succeeded);
        Assert.True(Guid.TryParseExact(issueResult.Result.OperationId, "N", out _));
        Assert.True(Guid.TryParseExact(negateResult.Result.OperationId, "N", out _));
        Assert.NotEqual(issueResult.Result.OperationId, negateResult.Result.OperationId);
        Assert.Equal(issueResult.Result.OperationId, operationIds["/xrpc/dev.idunno.labeler.issuance.issueLabel"]);
        Assert.Equal(negateResult.Result.OperationId, operationIds["/xrpc/dev.idunno.labeler.issuance.negateLabel"]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReceiptRecoveryAndAvailableLabelsUseTheirAdministrationMethods(bool additionalFields)
    {
        List<string> serviceAuthMethods = [];
        string? receiptOperationId = null;
        string? availableLabelsReason = null;
        string serviceJwt = CreateJwt(null, s_audience.ToString());
        using TestServerFixture server = CreateServer(async context =>
        {
            if (context.Request.Host.Host == s_pds.Host &&
                context.Request.Path == "/xrpc/com.atproto.server.getServiceAuth")
            {
                serviceAuthMethods.Add(context.Request.Query["lxm"].ToString());
                await context.Response.WriteAsJsonAsync(new { token = serviceJwt });
                return;
            }

            if (context.Request.Host.Host == s_labeler.Host &&
                context.Request.Path == "/xrpc/dev.idunno.labeler.issuance.getOperationReceipt")
            {
                receiptOperationId = context.Request.Query["operationId"].ToString();
                await context.Response.WriteAsync(additionalFields
                    ? $$$"""{"$type":"dev.idunno.labeler.issuance.getOperationReceipt#output","operationId":"{{{receiptOperationId}}}","sequence":17,"changed":false,"future":{"value":true}}"""
                    : $$$"""{"operationId":"{{{receiptOperationId}}}","sequence":17,"changed":false}""");
                return;
            }

            if (context.Request.Host.Host == s_labeler.Host &&
                context.Request.Path == "/xrpc/dev.idunno.labeler.issuance.getAvailableLabels")
            {
                availableLabelsReason = context.Request.Query["reason"].ToString();
                if (additionalFields)
                {
                    await context.Response.WriteAsync("""
                        {"$type":"dev.idunno.labeler.issuance.getAvailableLabels#output","future":true,
                         "moderation":{"future":{},"labelChoices":[
                           {"value":"spam","applied":false,"sequence":0,"otherVersion":false,"uriWide":false,"future":[]},
                           {"value":"misleading","applied":false,"sequence":0,"otherVersion":false,"uriWide":false,"future":null}]}}
                        """);
                    return;
                }

                await context.Response.WriteAsJsonAsync(new
                {
                    moderation = new
                    {
                        labelChoices = new[]
                        {
                            new
                            {
                                value = "spam",
                                applied = false,
                                sequence = 0,
                                otherVersion = false,
                                uriWide = false
                            },
                            new
                            {
                                value = "misleading",
                                applied = false,
                                sequence = 0,
                                otherVersion = false,
                                uriWide = false
                            }
                        }
                    }
                });
                return;
            }

            context.Response.StatusCode = StatusCodes.Status404NotFound;
        });

        using LabelerAgent labeler = CreateBuilder(server.Server).Build();
        await Login(labeler.AccountAgent);

        AtProtoHttpResult<LabelOperationReceipt> receipt = await labeler.GetLabelOperationReceipt(
            "operation-2",
            cancellationToken: TestContext.Current.CancellationToken);
        AtProtoHttpResult<IReadOnlyList<string>> available = await labeler.GetAvailableLabels(
            reason: "Inspect policy",
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(receipt.Succeeded);
        Assert.Equal("operation-2", receipt.Result.OperationId);
        Assert.Equal(17, receipt.Result.Sequence);
        Assert.False(receipt.Result.Changed);
        Assert.Equal("operation-2", receiptOperationId);
        Assert.True(available.Succeeded);
        Assert.Equal(["spam", "misleading"], available.Result);
        Assert.Equal("Inspect policy", availableLabelsReason);
        Assert.Equal(
            ["dev.idunno.labeler.issuance.getOperationReceipt", "dev.idunno.labeler.issuance.getAvailableLabels"],
            serviceAuthMethods);
    }

    [Fact]
    public async Task IssuanceMethodsPreserveStandardAtProtoErrors()
    {
        List<string> failedMethods = [];
        string serviceJwt = CreateJwt(null, s_audience.ToString());
        using TestServerFixture server = CreateServer(async context =>
        {
            if (context.Request.Host.Host == s_pds.Host &&
                context.Request.Path == "/xrpc/com.atproto.server.getServiceAuth")
            {
                await context.Response.WriteAsJsonAsync(new { token = serviceJwt });
                return;
            }

            if (context.Request.Host.Host == s_labeler.Host &&
                context.Request.Path.Value?.StartsWith(
                    "/xrpc/dev.idunno.labeler.issuance.",
                    StringComparison.Ordinal) is true)
            {
                failedMethods.Add(context.Request.Path.Value!);
                (int status, string error) = context.Request.Path.Value switch
                {
                    "/xrpc/dev.idunno.labeler.issuance.issueLabel" => (StatusCodes.Status400BadRequest, "ValueNotDeclared"),
                    "/xrpc/dev.idunno.labeler.issuance.negateLabel" => (StatusCodes.Status503ServiceUnavailable, "OutcomeUnknown"),
                    "/xrpc/dev.idunno.labeler.issuance.getOperationReceipt" => (StatusCodes.Status400BadRequest, "OperationNotFound"),
                    "/xrpc/dev.idunno.labeler.issuance.getAvailableLabels" when context.Request.Query["reason"] == "InvalidRequest" =>
                        (StatusCodes.Status400BadRequest, "InvalidRequest"),
                    "/xrpc/dev.idunno.labeler.issuance.getAvailableLabels" when context.Request.Query["reason"] == "Forbidden" =>
                        (StatusCodes.Status403Forbidden, "Forbidden"),
                    "/xrpc/dev.idunno.labeler.issuance.getAvailableLabels" when context.Request.Query["reason"] == "AuthenticationRequired" =>
                        (StatusCodes.Status401Unauthorized, "AuthenticationRequired"),
                    "/xrpc/dev.idunno.labeler.issuance.getAvailableLabels" =>
                        (StatusCodes.Status503ServiceUnavailable, "PolicyUnavailable"),
                    _ => throw new InvalidOperationException("Unexpected issuance endpoint.")
                };
                context.Response.StatusCode = status;
                await context.Response.WriteAsJsonAsync(new { error, message = "Labeler request failed." });
                return;
            }

            context.Response.StatusCode = StatusCodes.Status404NotFound;
        });

        using LabelerAgent labeler = CreateBuilder(server.Server).Build();
        await Login(labeler.AccountAgent);
        LabelTarget target = LabelTarget.Account(new Did(AccountDid));

        AtProtoHttpResult<LabelOperationReceipt> issue = await labeler.IssueLabel(
            operationId: "issue-denied",
            target: target,
            value: "spam",
            reason: "Test an issuance denial.",
            cancellationToken: TestContext.Current.CancellationToken);
        AtProtoHttpResult<LabelOperationReceipt> negate = await labeler.NegateLabel(
            operationId: "negate-denied",
            target: target,
            value: "spam",
            reason: "Test a negation denial.",
            cancellationToken: TestContext.Current.CancellationToken);
        AtProtoHttpResult<LabelOperationReceipt> receipt = await labeler.GetLabelOperationReceipt(
            "receipt-denied",
            cancellationToken: TestContext.Current.CancellationToken);
        AtProtoHttpResult<IReadOnlyList<string>> available = await labeler.GetAvailableLabels(
            cancellationToken: TestContext.Current.CancellationToken);
        AtProtoHttpResult<IReadOnlyList<string>> invalidRequest = await labeler.GetAvailableLabels(
            reason: "InvalidRequest",
            cancellationToken: TestContext.Current.CancellationToken);
        AtProtoHttpResult<IReadOnlyList<string>> forbidden = await labeler.GetAvailableLabels(
            reason: "Forbidden",
            cancellationToken: TestContext.Current.CancellationToken);
        AtProtoHttpResult<IReadOnlyList<string>> authenticationRequired = await labeler.GetAvailableLabels(
            reason: "AuthenticationRequired",
            cancellationToken: TestContext.Current.CancellationToken);

        AssertMappedError(issue, typeof(ValueNotDeclared), HttpStatusCode.BadRequest, "ValueNotDeclared");
        AssertMappedError(negate, typeof(OutcomeUnknown), HttpStatusCode.ServiceUnavailable, "OutcomeUnknown");
        AssertMappedError(receipt, typeof(OperationNotFound), HttpStatusCode.BadRequest, "OperationNotFound");
        AssertMappedError(available, typeof(PolicyUnavailable), HttpStatusCode.ServiceUnavailable, "PolicyUnavailable");
        AssertMappedError(invalidRequest, typeof(InvalidRequest), HttpStatusCode.BadRequest, "InvalidRequest");
        AssertMappedError(forbidden, typeof(Forbidden), HttpStatusCode.Forbidden, "Forbidden");
        AssertMappedError(
            authenticationRequired,
            typeof(AuthenticationRequired),
            HttpStatusCode.Unauthorized,
            "AuthenticationRequired");
        Assert.Equal(
            [
                "/xrpc/dev.idunno.labeler.issuance.issueLabel",
                "/xrpc/dev.idunno.labeler.issuance.negateLabel",
                "/xrpc/dev.idunno.labeler.issuance.getOperationReceipt",
                "/xrpc/dev.idunno.labeler.issuance.getAvailableLabels",
                "/xrpc/dev.idunno.labeler.issuance.getAvailableLabels",
                "/xrpc/dev.idunno.labeler.issuance.getAvailableLabels",
                "/xrpc/dev.idunno.labeler.issuance.getAvailableLabels"
            ],
            failedMethods);
    }

    [Fact]
    public async Task AppliedLabelQueryDoesNotForwardAccountCredentials()
    {
        string? labelerAuthorization = null;
        string? labelerPath = null;
        using TestServerFixture server = CreateServer(async context =>
        {
            if (context.Request.Host.Host == s_labeler.Host &&
                context.Request.Path == "/xrpc/com.atproto.label.queryLabels")
            {
                labelerAuthorization = context.Request.Headers.Authorization.ToString();
                labelerPath = context.Request.Path;
                await context.Response.WriteAsJsonAsync(new { labels = Array.Empty<object>(), cursor = (string?)null });
                return;
            }

            context.Response.StatusCode = StatusCodes.Status404NotFound;
        });

        using LabelerAgent labeler = CreateBuilder(server.Server).Build();
        await Login(labeler.AccountAgent);

        AtProtoHttpResult<PagedReadOnlyCollection<Label>> result = await labeler.GetAppliedLabels(
            new Did(AccountDid),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Equal("/xrpc/com.atproto.label.queryLabels", labelerPath);
        Assert.True(string.IsNullOrEmpty(labelerAuthorization));
    }

    [Fact]
    public async Task ProtectedCallsRecheckAuthenticationAfterTheAccountLogsOut()
    {
        using TestServerFixture server = CreateServer();
        using LabelerAgent labeler = CreateBuilder(server.Server).Build();
        await Login(labeler.AccountAgent);
        await labeler.AccountAgent.Logout(TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<AuthenticationRequiredException>(() => labeler.GetAvailableLabels(
            cancellationToken: TestContext.Current.CancellationToken));
    }

    private static LabelerAgentBuilder CreateBuilder(TestServer server)
        => LabelerAgentBuilder.Create()
            .WithLabelerService(s_labeler, s_audience.Did)
            .ConfigureAccountAgent(builder =>
            {
                builder.ForService(s_pds);
                builder.WithTimeProvider(s_clock);
                builder.HttpClientFactory = new TestHttpClientFactory(server);
                builder.BackgroundTokenRefreshEnabled = false;
            });

    private static AtProtoAgent CreateAccountAgent(TestServer server)
        => new(
            s_pds,
            new TestHttpClientFactory(server),
            new AtProtoAgentOptions
            {
                PlcDirectoryServer = s_pds,
                OAuthOptions = new OAuthOptions("http://localhost"),
                EnableBackgroundTokenRefresh = false,
                TimeProvider = s_clock
            });

    private static async Task Login(AtProtoAgent agent)
    {
        AtProtoHttpResult<bool> result = await agent.Login(
            identifier: "bot.test.invalid",
            password: "password",
            service: s_pds,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.True(agent.IsAuthenticated);
    }

    private static TestServerFixture CreateServer(Func<HttpContext, Task>? handler = null)
    {
        return new TestServerFixture(handler);
    }

    private sealed class TestServerFixture : IDisposable
    {
        private readonly IHost _host;

        internal TestServerFixture(Func<HttpContext, Task>? handler)
        {
            _host = new HostBuilder()
                .ConfigureWebHost(builder => builder.UseTestServer().Configure(app => app.Run(async context =>
            {
                if (context.Request.Path == "/xrpc/com.atproto.server.createSession")
                {
                    await context.Response.WriteAsJsonAsync(new
                    {
                        accessJwt = CreateJwt(AccountDid, $"did:web:{s_pds.Host}"),
                        refreshJwt = "refresh-token",
                        handle = "bot.test.invalid",
                        did = AccountDid
                    });
                }
                else if (context.Request.Path == "/xrpc/com.atproto.server.deleteSession")
                {
                    await context.Response.WriteAsJsonAsync(new { });
                }
                else if (handler is not null)
                {
                    await handler(context);
                }
                else
                {
                    context.Response.StatusCode = StatusCodes.Status404NotFound;
                }
            })))
                .Build();

            _host.Start();
        }

        internal TestServer Server => _host.GetTestServer();

        public void Dispose() => _host.Dispose();
    }

    private static string CreateJwt(string? subject, string audience)
    {
        using RSA rsa = RSA.Create(2048);
        Dictionary<string, object> claims = [];
        if (subject is not null)
        {
            claims["sub"] = subject;
        }

        DateTime now = s_clock.GetUtcNow().UtcDateTime;
        SecurityTokenDescriptor descriptor = new()
        {
            Audience = audience,
            Claims = claims,
            IssuedAt = now,
            Expires = now.AddMinutes(10),
            SigningCredentials = new SigningCredentials(new RsaSecurityKey(rsa), SecurityAlgorithms.RsaSha256Signature)
        };

        return new JsonWebTokenHandler { SetDefaultTimesOnTokenCreation = false }.CreateToken(descriptor);
    }

    private static void AssertMappedError<TResult>(
        AtProtoHttpResult<TResult> result,
        Type expectedErrorType,
        HttpStatusCode expectedStatus,
        string expectedError)
        where TResult : class
    {
        Assert.False(result.Succeeded);
        Assert.Equal(expectedStatus, result.StatusCode);
        Assert.Equal(expectedErrorType, result.AtErrorDetail?.GetType());
        Assert.Equal(expectedError, result.AtErrorDetail?.Error);
        Assert.Equal("Labeler request failed.", result.AtErrorDetail?.Message);
    }

    private sealed class TestHttpClientFactory(TestServer server) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => server.CreateClient();
    }
}
