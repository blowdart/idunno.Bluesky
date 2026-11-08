// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.CommandLine;
using System.Globalization;
using System.Net;
using System.Text;

using idunno.AtProto;
using idunno.AtProto.Labels;
using idunno.Bluesky.Labeler;

using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

using Sample = Samples.LabelerAgent.Program;

namespace idunno.Bluesky.Labeler.Cli.Test;

public sealed class LabelerAgentSampleTests
{
    private const string AccountDid = "did:plc:ec72yg6n2sydzjvtovvdlxrk";
    private const string RecordUri = $"at://{AccountDid}/app.bsky.feed.post/3lxyz";
    private static readonly Uri s_labelerHost = new("https://labeler.test.invalid");

    [Theory]
    [InlineData("--account", AccountDid, true)]
    [InlineData("--account", "person.example", true)]
    [InlineData("--aturi", RecordUri, true)]
    [InlineData("--aturi", "at://person.example/app.bsky.feed.post/3lxyz", true)]
    [InlineData("--post", RecordUri, true)]
    [InlineData("--account", "not-an-account", false)]
    [InlineData("--account", "", false)]
    [InlineData("--aturi", "at://did:plc:example", false)]
    [InlineData("--aturi", "at://did:plc:example/app.bsky.feed.post", false)]
    [InlineData("--aturi", "https://example.com/post", false)]
    public void QueryValidatesTargetsWithoutRequiringCredentials(string option, string target, bool valid)
    {
        ParseResult result = Sample.CreateRootCommand().Parse(
            ["query", "--labeler-host", s_labelerHost.AbsoluteUri, option, target]);

        Assert.Equal(valid, result.Errors.Count == 0);
    }

    [Theory]
    [InlineData("https://labeler.test.invalid", true)]
    [InlineData("http://labeler.test.invalid", false)]
    [InlineData("https://labeler.test.invalid/xrpc", false)]
    [InlineData("https://user:password@labeler.test.invalid", false)]
    [InlineData("https://labeler.test.invalid/?secret=value", false)]
    [InlineData("https://labeler.test.invalid/#fragment", false)]
    public void QueryRequiresARootHttpsHost(string host, bool valid)
    {
        ParseResult result = Sample.CreateRootCommand().Parse(["query", "--labeler-host", host, "--account", AccountDid]);

        Assert.Equal(valid, result.Errors.Count == 0);
    }

    [Fact]
    public void QueryRequiresExactlyOneTargetAndAHost()
    {
        Assert.NotEmpty(Sample.CreateRootCommand().Parse(["query", "--labeler-host", s_labelerHost.AbsoluteUri]).Errors);
        Assert.NotEmpty(Sample.CreateRootCommand().Parse(["query", "--account", AccountDid]).Errors);
        Assert.NotEmpty(Sample.CreateRootCommand().Parse(
            ["query", "--labeler-host", s_labelerHost.AbsoluteUri, "--account", AccountDid, "--aturi", RecordUri]).Errors);
        Assert.NotEmpty(Sample.CreateRootCommand().Parse(
            ["query", "--labeler-host", s_labelerHost.AbsoluteUri, "--account", AccountDid, "--label-value", "spam"]).Errors);
    }

    [Theory]
    [InlineData("label")]
    [InlineData("negate")]
    [InlineData("remove")]
    public void MutationValidationStillRequiresLabelValuesAndExactlyOneTarget(string command)
    {
        string[] common = [command, "--labeler-host", s_labelerHost.AbsoluteUri, "--username", "bot.example", "--api-key", "test-password"];
        Assert.Empty(Sample.CreateRootCommand().Parse([.. common, "--account", AccountDid, "--label-value", "spam"]).Errors);
        Assert.Empty(Sample.CreateRootCommand().Parse([.. common, "--post", RecordUri, "--label-value", "spam"]).Errors);
        Assert.NotEmpty(Sample.CreateRootCommand().Parse([.. common, "--account", AccountDid]).Errors);
        Assert.NotEmpty(Sample.CreateRootCommand().Parse([.. common, "--label-value", "spam"]).Errors);
        Assert.NotEmpty(Sample.CreateRootCommand().Parse(
            [.. common, "--account", AccountDid, "--aturi", RecordUri, "--label-value", "spam"]).Errors);
    }

    [Theory]
    [InlineData(null, AccountDid, false)]
    [InlineData(RecordUri, null, false)]
    [InlineData(null, AccountDid, true)]
    public async Task QueryFollowsAllPagesWithoutAuthenticationAndShowsLabelState(string? record, string? account, bool emptyFirstPage)
    {
        string subject = record ?? account!;
        int requests = 0;
        using QueryHandler handler = new(request =>
        {
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.Equal(s_labelerHost.Host, request.RequestUri!.Host);
            Assert.Equal("/xrpc/com.atproto.label.queryLabels", request.RequestUri.AbsolutePath);
            Assert.Null(request.Headers.Authorization);
            Dictionary<string, Microsoft.Extensions.Primitives.StringValues> query = QueryHelpers.ParseQuery(request.RequestUri.Query);
            Assert.Equal(subject, query["uriPatterns"].ToString());
            Assert.Equal("250", query["limit"].ToString());
            Assert.False(query.ContainsKey("sources"));
            requests++;
            if (requests == 1)
            {
                Assert.False(query.ContainsKey("cursor"));
                return Json(HttpStatusCode.OK, emptyFirstPage
                    ? """{"labels":[],"cursor":"next/& page"}"""
                    : $$"""{"labels":[{{LabelJson(subject, false)}}],"cursor":"next/& page"}""");
            }

            Assert.Equal(2, requests);
            Assert.Equal("next/& page", query["cursor"].ToString());
            return Json(HttpStatusCode.OK, $$"""{"labels":[{{LabelJson(subject, true)}}]}""");
        });
        using ServiceProvider services = CreateServices(handler);
        using AtProtoAgent agent = CreateAgent(services);
        using StringWriter output = new(CultureInfo.InvariantCulture);
        using StringWriter error = new(CultureInfo.InvariantCulture);
        Dictionary<Did, int> lookups = [];

        int exitCode = await Sample.RunQueryAsync(
            agent: agent,
            labelerHost: s_labelerHost,
            recordText: record,
            accountText: account,
            output: output,
            error: error,
            cancellationToken: TestContext.Current.CancellationToken,
            timeProvider: CreateClock(),
            resolveHandle: (did, _) =>
            {
                lookups[did] = lookups.GetValueOrDefault(did) + 1;
                return Task.FromResult(new Handle(did.Value == AccountDid ? "person.example" : "labeler.example"));
            });

        Assert.Equal(0, exitCode);
        Assert.Equal(2, requests);
        Assert.Empty(error.ToString());
        Assert.Contains("\U0001F3F7  spam", output.ToString());
        Assert.Contains(record is null ? "Targeted at: @person.example" : "Targeted at: at://person.example/app.bsky.feed.post/3lxyz", output.ToString());
        Assert.Contains("Issued by: @labeler.example", output.ToString());
        Assert.Contains("\U0001F6AB  Negated", output.ToString());
        if (!emptyFirstPage)
        {
            Assert.Contains("\u23F1\uFE0F  Expired:", output.ToString());
        }
        else
        {
            Assert.DoesNotContain("\u23F1\uFE0F", output.ToString());
        }
        Assert.DoesNotContain("did:", output.ToString());
        Assert.DoesNotContain("CID:", output.ToString());
        Assert.All(lookups.Values, count => Assert.Equal(1, count));
        Assert.DoesNotContain("No labels found", output.ToString());
    }

    [Theory]
    [InlineData(false, 0, "No labels found.", "")]
    [InlineData(true, 1, "", "Label query failed (403 Forbidden): Forbidden.")]
    public async Task QueryReportsEmptyResultsAndProtocolFailures(bool fail, int expectedExitCode, string expectedOutput, string expectedError)
    {
        using QueryHandler handler = new(_ => fail
            ? Json(HttpStatusCode.Forbidden, """{"error":"Forbidden","message":"Unavailable"}""")
            : Json(HttpStatusCode.OK, """{"labels":[]}"""));
        using ServiceProvider services = CreateServices(handler);
        using AtProtoAgent agent = CreateAgent(services);
        using StringWriter output = new(CultureInfo.InvariantCulture);
        using StringWriter error = new(CultureInfo.InvariantCulture);

        int exitCode = await Sample.RunQueryAsync(
            agent: agent,
            labelerHost: s_labelerHost,
            recordText: null,
            accountText: AccountDid,
            output: output,
            error: error,
            cancellationToken: TestContext.Current.CancellationToken,
            timeProvider: CreateClock());

        Assert.Equal(expectedExitCode, exitCode);
        Assert.Equal(expectedOutput, output.ToString().Trim());
        Assert.Equal(expectedError, error.ToString().Trim());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task QueryDoesNotReportSuccessAfterAPaginationFailure(bool repeatCursor)
    {
        int requests = 0;
        using QueryHandler handler = new(_ =>
        {
            requests++;
            return requests == 1 || repeatCursor
                ? Json(HttpStatusCode.OK, $$"""{"labels":[{{LabelJson(AccountDid, false)}}],"cursor":"next"}""")
                : Json(HttpStatusCode.Forbidden, """{"error":"Forbidden"}""");
        });
        using ServiceProvider services = CreateServices(handler);
        using AtProtoAgent agent = CreateAgent(services);
        using StringWriter output = new(CultureInfo.InvariantCulture);
        using StringWriter error = new(CultureInfo.InvariantCulture);

        int exitCode = await Sample.RunQueryAsync(
            agent: agent,
            labelerHost: s_labelerHost,
            recordText: null,
            accountText: AccountDid,
            output: output,
            error: error,
            cancellationToken: TestContext.Current.CancellationToken,
            timeProvider: CreateClock(),
            resolveHandle: (_, _) => Task.FromResult(Handle.Invalid));

        Assert.Equal(1, exitCode);
        Assert.Equal(2, requests);
        Assert.Contains("\U0001F3F7  spam", output.ToString());
        Assert.DoesNotContain("\U0001F6AB", output.ToString());
        Assert.Contains("Issued by: Handle unavailable", output.ToString());
        Assert.Contains("Targeted at: Handle unavailable", output.ToString());
        Assert.DoesNotContain("did:", output.ToString());
        Assert.Contains(repeatCursor ? "repeated pagination cursor" : "Label query failed", error.ToString());
    }

    [Theory]
    [InlineData(-1, false, "Expired")]
    [InlineData(-1, true, "Expired")]
    [InlineData(0, false, "Expired")]
    [InlineData(0, true, "Expired")]
    [InlineData(1, false, "Expires")]
    [InlineData(1, true, "Expires")]
    [InlineData(null, false, "Does not expire")]
    [InlineData(null, true, "Does not expire")]
    public void QueryFormatsLocalDatesAndLabelStateOnSeparateLines(int? expiryOffset, bool negated, string expectedState)
    {
        FakeTimeProvider clock = CreateClock();
        DateTimeOffset created = clock.GetUtcNow().AddDays(-1);
        DateTimeOffset? expiresAt = expiryOffset is int offset ? clock.GetUtcNow().AddMinutes(offset) : null;
        Label label = new(
            version: 1,
            source: new Did("did:plc:labeler"),
            uri: AccountDid,
            cid: null,
            value: "spam",
            isNegationLabel: negated,
            creationTimestamp: created,
            signature: null)
        {
            ExpiresAt = expiresAt
        };

        string output = Sample.FormatQueryLabel(
            label: label,
            source: "@labeler.example",
            target: "@person.example",
            timeProvider: clock);

        List<string> expected =
        [
            "\U0001F3F7  spam",
            "\U0001F4E1  Issued by: @labeler.example",
            "\U0001F3AF  Targeted at: @person.example"
        ];
        if (negated)
        {
            expected.Add("\U0001F6AB  Negated");
        }

        expected.Add($"\U0001F4C5  {(negated ? "Negated at" : "Created")}: {created.ToOffset(TimeSpan.FromHours(-8)).ToString("f", CultureInfo.CurrentCulture)} / {created.UtcDateTime.ToString("f", CultureInfo.CurrentCulture)} UTC");

        if (!negated)
        {
            expected.Add(expiresAt is DateTimeOffset expiry
                ? $"\u23F1\uFE0F  {expectedState}: {expiry.ToOffset(TimeSpan.FromHours(-8)).ToString("f", CultureInfo.CurrentCulture)} / {expiry.UtcDateTime.ToString("f", CultureInfo.CurrentCulture)} UTC"
                : $"\u23F1\uFE0F  {expectedState}");
        }
        Assert.Equal(expected, output.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries));
    }

    [Fact]
    public void QueryShowsRecordVersionWithItsOwnSymbolOnlyWhenPresent()
    {
        Label label = new(
            version: 1,
            source: new Did("did:plc:labeler"),
            uri: RecordUri,
            cid: new Cid("bafyreievgu2ty7qbiaaom5zhmkznsnajuzideek3lo7e65dwqlrvrxnmo4"),
            value: "spam",
            isNegationLabel: false,
            creationTimestamp: CreateClock().GetUtcNow(),
            signature: null);

        string output = Sample.FormatQueryLabel(
            label: label,
            source: "@labeler.example",
            target: "at://person.example/app.bsky.feed.post/3lxyz",
            timeProvider: CreateClock());

        Assert.Contains(
            $"{Environment.NewLine}\U0001F50E  CID: {label.Cid}{Environment.NewLine}",
            output);
    }

    [Theory]
    [InlineData(false, true, "Label applied")]
    [InlineData(false, false, "Already applied")]
    [InlineData(true, true, "Label negated")]
    [InlineData(true, false, "Nothing to negate")]
    public void MutationsDescribeChangesAndNoOpsWithoutReceiptInternals(bool negate, bool changed, string outcome)
    {
        string output = Sample.FormatMutationResult(
            negate: negate,
            changed: changed,
            labelValue: "beansonista",
            target: "bot.idunno.blue");

        string symbol = negate ? "\U0001F6AB" : "\U0001F3F7";
        Assert.Equal($"{symbol}  {outcome}: 'beansonista' for bot.idunno.blue.", output);
    }

    [Fact]
    public void MutationErrorsHaveAConsistentlySpacedErrorSymbol()
    {
        Assert.Equal("\u274C  Login failed.", Sample.FormatError("Login failed."));
        ParseResult result = Sample.CreateRootCommand(CreateClock()).Parse(
            [
                "label", "--labeler-host", s_labelerHost.AbsoluteUri,
                "--username", "bot.example", "--api-key", "test-password",
                "--account", AccountDid, "--label-value", "spam", "--expires-in", "00:00:00"
            ]);

        Assert.Contains(result.Errors, error => error.Message == "\u274C  --expires-in must be a positive duration.");
    }

    [Theory]
    [InlineData("--expires-in", "01:00:00", true)]
    [InlineData("--expires-in", "1.02:30:00", true)]
    [InlineData("--expires-in", "00:00:00", false)]
    [InlineData("--expires-in", "-01:00:00", false)]
    [InlineData("--expires-in", "not-a-duration", false)]
    [InlineData("--expires-in", "10675199.02:48:05.4775807", false)]
    [InlineData("--expires-at", "2030-01-03T00:00:01Z", true)]
    [InlineData("--expires-at", "2030-01-02T17:00:00-08:00", true)]
    [InlineData("--expires-at", "2030-01-03T00:00:00Z", false)]
    [InlineData("--expires-at", "2030-01-02T16:00:00-08:00", false)]
    [InlineData("--expires-at", "2029-01-01T00:00:00Z", false)]
    [InlineData("--expires-at", "not-a-date", false)]
    public void LabelValidatesOptionalExpiry(string option, string value, bool valid)
    {
        ParseResult result = Sample.CreateRootCommand(CreateClock()).Parse(
            [
                "label", "--labeler-host", s_labelerHost.AbsoluteUri,
                "--username", "bot.example", "--api-key", "test-password",
                "--account", AccountDid, "--label-value", "spam", option, value
            ]);

        Assert.Equal(valid, result.Errors.Count == 0);
    }

    [Theory]
    [InlineData("label")]
    [InlineData("negate")]
    [InlineData("remove")]
    [InlineData("list")]
    [InlineData("query")]
    public void ExpiryOptionsCannotBeCombinedOrUsedOutsideLabel(string command)
    {
        string[] common =
        [
            command, "--labeler-host", s_labelerHost.AbsoluteUri
        ];
        if (command != "query")
        {
            common = [.. common, "--username", "bot.example", "--api-key", "test-password"];
        }

        string[] target = ["--account", AccountDid, "--label-value", "spam"];
        string[] arguments = command switch
        {
            "list" => common,
            "query" => [.. common, "--account", AccountDid],
            _ => [.. common, .. target]
        };
        Assert.Empty(Sample.CreateRootCommand(CreateClock()).Parse(arguments).Errors);
        ParseResult both = Sample.CreateRootCommand(CreateClock()).Parse(
            [.. arguments, "--expires-in", "01:00:00", "--expires-at", "2030-01-04T00:00:00Z"]);
        Assert.NotEmpty(both.Errors);
        if (command != "label")
        {
            Assert.NotEmpty(Sample.CreateRootCommand(CreateClock()).Parse(
                [.. arguments, "--expires-in", "01:00:00"]).Errors);
            Assert.NotEmpty(Sample.CreateRootCommand(CreateClock()).Parse(
                [.. arguments, "--expires-at", "2030-01-04T00:00:00Z"]).Errors);
        }
    }

    [Fact]
    public void RelativeExpiryStartsAtIssuanceAndElapsedAbsoluteExpiryIsRejected()
    {
        FakeTimeProvider clock = CreateClock();
        DateTimeOffset initial = clock.GetUtcNow();
        DateTimeOffset absolute = initial.AddMinutes(1);
        Assert.True(Sample.TryResolveExpiry(
            expiresIn: null,
            expiresAt: absolute.ToString("O", CultureInfo.InvariantCulture),
            timeProvider: clock,
            expiry: out DateTimeOffset? originalExpiry,
            error: out _));
        Assert.Equal(absolute, originalExpiry);
        clock.Advance(TimeSpan.FromMinutes(2));

        Assert.False(Sample.TryResolveExpiry(
            expiresIn: null,
            expiresAt: absolute.ToString("O", CultureInfo.InvariantCulture),
            timeProvider: clock,
            expiry: out _,
            error: out string error));
        Assert.Equal("--expires-at must be in the future.", error);
        Assert.True(Sample.TryResolveExpiry(
            expiresIn: "01:00:00",
            expiresAt: null,
            timeProvider: clock,
            expiry: out DateTimeOffset? relativeExpiry,
            error: out _));
        Assert.Equal(initial.AddMinutes(62), relativeExpiry);
        Assert.True(Sample.TryResolveExpiry(
            expiresIn: null,
            expiresAt: null,
            timeProvider: clock,
            expiry: out DateTimeOffset? permanentExpiry,
            error: out _));
        Assert.Null(permanentExpiry);
    }

    [Fact]
    public void RemoveIsAnAliasForTheSameNegateCommand()
    {
        RootCommand root = Sample.CreateRootCommand(CreateClock());
        string[] options =
        [
            "--labeler-host", s_labelerHost.AbsoluteUri,
            "--username", "bot.example", "--api-key", "test-password",
            "--account", AccountDid, "--label-value", "beansonista"
        ];
        ParseResult negate = root.Parse(["negate", .. options]);
        ParseResult remove = root.Parse(["remove", .. options]);

        Assert.Empty(remove.Errors);
        Assert.Same(negate.CommandResult.Command, remove.CommandResult.Command);
        Assert.Same(negate.CommandResult.Command.Action, remove.CommandResult.Command.Action);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task NegationResolvesHandlesAndPreservesRecordScope(bool record, bool missing)
    {
        Cid cid = new("bafyreievgu2ty7qbiaaom5zhmkznsnajuzideek3lo7e65dwqlrvrxnmo4");
        LabelTarget target = record
            ? LabelTarget.Record(new AtUri("at://bot.idunno.blue/app.bsky.feed.post/3lxyz"), cid)
            : LabelTarget.Account(new Handle("bot.idunno.blue"));
        int lookups = 0;
        LabelTarget? resolved = await Sample.ResolveNegationTargetAsync(
            target: target,
            resolveHandle: (handle, _) =>
            {
                lookups++;
                Assert.Equal("bot.idunno.blue", handle.Value);
                return Task.FromResult(missing ? null : new Did(AccountDid));
            },
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(1, lookups);
        if (missing)
        {
            Assert.Null(resolved);
            return;
        }

        Assert.NotNull(resolved);
        Assert.Equal(AccountDid, resolved.Repository.Value);
        Assert.Equal(target.Kind, resolved.Kind);
        Assert.Equal(record ? RecordUri : null, resolved.RecordUri?.ToString());
        Assert.Equal(record ? cid : null, resolved.Cid);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NegationDoesNotResolveOrChangeDidTargets(bool record)
    {
        LabelTarget target = record
            ? LabelTarget.Record(new AtUri(RecordUri))
            : LabelTarget.Account(new Did(AccountDid));
        LabelTarget? resolved = await Sample.ResolveNegationTargetAsync(
            target: target,
            resolveHandle: (_, _) => throw new InvalidOperationException("DID targets must not require handle resolution."),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Same(target, resolved);
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public void QueryStrikesThroughOnlyNegatedLabelNamesWhenAnsiIsEnabled(bool negated, bool useAnsi)
    {
        Label label = new(
            version: 1,
            source: new Did("did:plc:labeler"),
            uri: AccountDid,
            cid: null,
            value: "beansonista",
            isNegationLabel: negated,
            creationTimestamp: CreateClock().GetUtcNow(),
            signature: null);
        string output = Sample.FormatQueryLabel(
            label: label,
            source: "@labeler.example",
            target: "@person.example",
            timeProvider: CreateClock(),
            useAnsi: useAnsi);

        string value = negated && useAnsi ? "\u001b[9mbeansonista\u001b[29m" : "beansonista";
        Assert.StartsWith($"\U0001F3F7  {value}{Environment.NewLine}", output);
        Assert.DoesNotContain('\u001b', output[(output.IndexOf(Environment.NewLine, StringComparison.Ordinal) + Environment.NewLine.Length)..]);
        if (!negated || !useAnsi)
        {
            Assert.DoesNotContain('\u001b', output);
        }
    }

    /// <summary>Creates a fixed clock with a local zone distinct from UTC for label expiry and presentation tests.</summary>
    private static FakeTimeProvider CreateClock()
    {
        FakeTimeProvider clock = new(new DateTimeOffset(2030, 1, 3, 0, 0, 0, TimeSpan.Zero));
        clock.SetLocalTimeZone(TimeZoneInfo.CreateCustomTimeZone(
            id: "Test local",
            baseUtcOffset: TimeSpan.FromHours(-8),
            displayName: "Test local",
            standardDisplayName: "Test local"));

        return clock;
    }

    /// <summary>Creates label data including a negation flag and an expiry evaluated against the fixed test clock.</summary>
    private static string LabelJson(string subject, bool negated)
        => $$"""{"src":"did:plc:labeler","uri":"{{subject}}","val":"spam","cts":"2030-01-01T00:00:00Z","exp":"2030-01-02T00:00:00Z","neg":{{(negated ? "true" : "false")}}}""";

    /// <summary>Creates a protocol response for the query handler.</summary>
    private static HttpResponseMessage Json(HttpStatusCode status, string body)
        => new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    /// <summary>Uses the standard HTTP client factory with a deterministic in-memory transport.</summary>
    private static ServiceProvider CreateServices(QueryHandler handler)
    {
        ServiceCollection services = new();
        services.AddHttpClient("idunno.atproto").ConfigurePrimaryHttpMessageHandler(() => handler);

        return services.BuildServiceProvider();
    }

    /// <summary>Creates an unauthenticated agent using the test transport.</summary>
    private static AtProtoAgent CreateAgent(ServiceProvider services)
        => AtProtoAgentBuilder.Create().WithHttpClientFactory(services.GetRequiredService<IHttpClientFactory>()).Build();

    /// <summary>Answers only the requests expected by each query test.</summary>
    private sealed class QueryHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        /// <summary>Returns a deterministic response without contacting a server.</summary>
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(respond(request));
    }
}
