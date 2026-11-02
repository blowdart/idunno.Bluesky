// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.CommandLine;
using System.CommandLine.Parsing;
using System.Net;

using idunno.AtProto;
using idunno.AtProto.Authentication;
using idunno.AtProto.OAuthCallback;

using Microsoft.Extensions.Logging.Abstractions;

namespace idunno.Bluesky.Labeler.Cli.Test;

public class CliExperienceTests
{
    [Theory]
    [InlineData("forgetme")]
    [InlineData("status --rememberme")]
    public void RemovedCredentialCommandSpellingsAreRejected(string arguments)
    {
        ParseResult parsed = Program.CreateRootCommand().Parse(arguments.Split(' '));
        Assert.NotEmpty(parsed.Errors);
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData(null, true)]
    [InlineData("Labeler-account authority to publish the declaration.", false)]
    [InlineData("Labeler-account authority to publish the declaration.", true)]
    public async Task SignInNoticeWarnsOnlyForLabelerAccountOperations(string? purpose, bool rememberMe)
    {
        using StringWriter diagnostics = new();
        await Program.WriteSignInNoticeAsync(
            diagnostics: diagnostics,
            handle: new Handle("blowdart.me"),
            labelerPurpose: purpose,
            rememberMe: rememberMe);

        string expected = "🔑  Xenon starting sign-in for blowdart.me." + Environment.NewLine;
        if (purpose is not null)
        {
            expected += $"⚠   {purpose}" + Environment.NewLine;
        }
        expected += (rememberMe
            ? "💾  Credential persistence was requested with --remember-me."
            : "ℹ   This sign-in is temporary. Use --remember-me to save credentials.") + Environment.NewLine;
        Assert.Equal(expected, diagnostics.ToString());
    }

    [Theory]
    [InlineData("yes", false, true)]
    [InlineData("Y", false, true)]
    [InlineData("", false, false)]
    [InlineData("no", false, false)]
    [InlineData("yes", true, false)]
    public async Task UnencryptedStorageRequiresIndependentInteractiveConsent(string answer, bool redirected, bool expected)
    {
        using StringReader input = new(answer + Environment.NewLine);
        using StringWriter diagnostics = new();
        bool permitted = await Program.ConfirmCredentialStorageAsync(
            input: input,
            diagnostics: diagnostics,
            inputRedirected: redirected,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(expected, permitted);
        if (redirected)
        {
            Assert.Contains("Neither --yes nor redirected input", diagnostics.ToString(), StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task BrowserLaunchFailureDoesNotExposeItsCommandLine()
    {
        using StringWriter diagnostics = new();
        bool opened = await Program.OpenSignInBrowserAsync(
            () => throw new System.ComponentModel.Win32Exception("https://provider.example/?state=private-state"),
            diagnostics);

        Assert.False(opened);
        Assert.Contains("browser could not be opened", diagnostics.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("private-state", diagnostics.ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null, null, "https://labeler.example", "https://labeler.example/native-client-metadata.json")]
    [InlineData(null, "https://labeler.example/custom.json", "https://labeler.example", "https://labeler.example/custom.json")]
    [InlineData("https://publisher.example/native.json", "https://labeler.example/custom.json", "https://labeler.example", "https://publisher.example/native.json")]
    [InlineData(null, "bad-uri", "https://labeler.example", null)]
    [InlineData(null, null, null, null)]
    [InlineData(null, null, "http://labeler.example", null)]
    public void PublicationMetadataDefaultsWithoutHidingInvalidExplicitValues(string? labeler, string? administrator, string? origin, string? expected)
    {
        Assert.Equal(expected is null ? null : new Uri(expected), Program.SelectPolicyClientMetadata(
            labelerMetadata: labeler is null ? null : new Uri(labeler),
            administratorMetadata: administrator,
            origin: origin is null ? null : new Uri(origin)));
    }

    [Theory]
    [InlineData("invalid_scope: secret-description", "invalid_scope")]
    [InlineData("https://provider.example/?code=private-code", "authentication error")]
    [InlineData("access_denied: private description", "access_denied")]
    public void AuthenticationDiagnosticsContainOnlyAllowlistedCategories(string message, string expected)
    {
        Assert.Equal(expected, Program.ClassifyOAuthFailure(new OAuthException(message)));
    }

    [Fact]
    public void RemoteOptionsAcceptClearNames()
    {
        RootCommand root = Program.CreateRootCommand();
        ParseResult parsed = root.Parse(
            [
                "status", "--labeler-url", "https://labeler.example", "--admin-service", "did:web:labeler.example#labeler_admin",
                "--admin-handle", "admin.example", "--oauth-client-id", "https://labeler.example/native-client-metadata.json", "--remember-me"
            ]);

        Assert.Empty(parsed.Errors);
        Assert.Empty(root.Parse(["status", "--labeler-url", "https://labeler.example", "--admin-handle", "admin.example"]).Errors);
        Assert.DoesNotContain(root.Parse(["status"]).Errors, error => error.Message.Contains("--remember-me", StringComparison.Ordinal));
    }

    [Fact]
    public void CountsCommandAcceptsLocalAndRemoteTransports()
    {
        RootCommand root = Program.CreateRootCommand();

        Assert.Empty(root.Parse(["counts", "--local-endpoint", "idunno-labeler-admin"]).Errors);
        Assert.Empty(root.Parse(["counts", "--labeler-url", "https://labeler.example", "--admin-handle", "admin.example"]).Errors);
        Assert.Contains("unresolved cases, reports, and appeals", root.Subcommands.Single(command => command.Name == "counts").Description, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("atproto rpc:dev.idunno.labeler.admin.getTeam?aud=did%3Aweb%3Alabeler.example%23labeler_admin", "did:web:labeler.example#labeler_admin")]
    [InlineData("rpc:dev.idunno.labeler.admin.getTeam?aud=did%3Aweb%3Alabeler.example%23labeler_admin rpc:dev.idunno.labeler.admin.getAudit?aud=did%3Aweb%3Alabeler.example%23labeler_admin", "did:web:labeler.example#labeler_admin")]
    [InlineData("rpc:dev.idunno.labeler.admin.getTeam?aud=not-a-service", null)]
    [InlineData("rpc:dev.idunno.labeler.admin.getTeam?aud=did%3Aweb%3Aone.example%23labeler_admin rpc:dev.idunno.labeler.admin.getAudit?aud=did%3Aweb%3Atwo.example%23labeler_admin", null)]
    [InlineData("atproto repo:app.bsky.labeler.service?action=create", null)]
    public void AdminServiceDiscoveryRequiresOneValidAdministrationAudience(string? scope, string? expected)
    {
        bool discovered = Program.TryDiscoverAdminService(scope, out AtProtoServiceReference? audience);

        Assert.Equal(expected is not null, discovered);
        Assert.Equal(expected, audience?.ToString());
    }

    [Fact]
    public void RootHelpExplainsOptionalCredentialSaving()
    {
        string? description = Program.CreateRootCommand().Description;

        Assert.NotNull(description);
        Assert.Contains("--remember-me", description, StringComparison.Ordinal);
        Assert.Contains("temporary by default", description, StringComparison.Ordinal);
        Assert.Contains("DPAPI", description, StringComparison.Ordinal);
        Assert.Contains("unencrypted", description, StringComparison.Ordinal);
    }

    [Fact]
    public void ForgetMeIsAvailableAsARootCommand()
    {
        ParseResult parsed = Program.CreateRootCommand().Parse(["forget-me"]);

        Assert.Empty(parsed.Errors);
    }

    [Theory]
    [InlineData("status", "--admin-url")]
    [InlineData("status", "--audience")]
    [InlineData("status", "--handle")]
    [InlineData("status", "--client-id")]
    [InlineData("rotation publish-plc", "--handle")]
    [InlineData("rotation publish-plc", "--client-id")]
    [InlineData("rotation recover-plc", "--handle")]
    [InlineData("rotation recover-plc", "--client-id")]
    [InlineData("policy publish", "--labeler-client-id")]
    public void UnreleasedOptionNamesAreNotRetainedAsAliases(string command, string oldName)
    {
        Command selected = Program.CreateRootCommand();
        foreach (string part in command.Split(' '))
        {
            selected = selected.Subcommands.Single(value => value.Name == part);
        }

        Assert.DoesNotContain(selected.Options, option => option.Name == oldName || option.Aliases.Contains(oldName));
    }

    [Theory]
    [InlineData("publish")]
    [InlineData("recover")]
    public void PolicyPublicationAllowsSharedNativeMetadata(string operation)
    {
        ParseResult parsed = Program.CreateRootCommand().Parse(
            [
                "policy", operation, "--labeler-url", "https://labeler.example",
                "--admin-service", "did:web:labeler.example#labeler_admin", "--admin-handle", "admin.example",
                "--labeler-handle", "labeler.example", "--reason", "Test", "--operation-id", "publication/test",
                "--expected-revision", "1", "--version", "test-v1", "--create", "--remember-me"
            ]);

        Assert.Empty(parsed.Errors);
    }

    [Theory]
    [InlineData("""{"succeeded":true,"changed":true}""", 200, 0, "Administration succeeded.")]
    [InlineData("""{"succeeded":true,"moderation":{"openCounts":{"cases":3,"reports":3,"appeals":2}}}""", 200, 0, "Administration succeeded.")]
    [InlineData("""{"succeeded":true,"changed":false}""", 200, 0, "No change was needed.")]
    [InlineData("""{"succeeded":false,"error":"Unauthorized"}""", 200, 4, "failed")]
    [InlineData("""{"succeeded":false,"error":"InvalidRequest"}""", 400, 2, "failed")]
    [InlineData("""{"succeeded":true}""", 403, 4, "denied")]
    [InlineData("""{"succeeded":true}""", 500, 5, "unavailable")]
    [InlineData("{}", 200, 5, "no valid success receipt")]
    [InlineData("[]", 200, 5, "no valid success receipt")]
    [InlineData("""{"succeeded":"true"}""", 200, 5, "no valid success receipt")]
    [InlineData("not json", 200, 5, "invalid JSON")]
    public async Task FeedbackPreservesJsonAndRequiresARealSuccessReceipt(string body, int status, int expected, string message)
    {
        using StringWriter output = new();
        using StringWriter diagnostics = new();
        int result = await Program.WriteResponseAsync(
            body: body,
            statusCode: (HttpStatusCode)status,
            output: output,
            diagnostics: diagnostics,
            verbose: true);

        Assert.Equal(expected, result);
        Assert.Equal(body + Environment.NewLine, output.ToString());
        Assert.Contains(message, diagnostics.ToString(), StringComparison.Ordinal);
        Assert.StartsWith(expected == 0 ? "✔   " : "✖   ", diagnostics.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task CountsOutputUsesDistinctWorkflowIcons()
    {
        using StringWriter output = new();
        using StringWriter diagnostics = new();
        int result = await Program.WriteResponseAsync(
            body: """{"succeeded":true,"moderation":{"openCounts":{"cases":12,"reports":0,"appeals":3}}}""",
            statusCode: HttpStatusCode.OK,
            output: output,
            diagnostics: diagnostics);

        Assert.Equal(0, result);
        Assert.Equal(
            "ℹ   Moderation:" + Environment.NewLine +
            "  📋  Cases: 12" + Environment.NewLine +
            "  📨  Reports: 0" + Environment.NewLine +
            "  ⚖   Appeals: 3" + Environment.NewLine,
            output.ToString());
        Assert.StartsWith("✔   ", diagnostics.ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("members", "👤  ")]
    [InlineData("labels", "🏷️  ")]
    [InlineData("issuer", "🪪  ")]
    [InlineData("sequence", "#   ")]
    [InlineData("streamId", "🌊  ")]
    [InlineData("issuedThrough", "🏷️  ")]
    [InlineData("publishedThrough", "📡  ")]
    [InlineData("phase", "🚦  ")]
    [InlineData("retirementUpperBound", "🎯  ")]
    [InlineData("retirementThrough", "🏁  ")]
    [InlineData("other", "•   ")]
    public async Task ReadableFieldPrefixesPadNarrowSymbolsAndWideEmoji(string field, string prefix)
    {
        using System.Text.Json.JsonDocument document = System.Text.Json.JsonDocument.Parse($"{{\"{field}\":1}}");
        using StringWriter output = new();
        await Program.WriteReadableResultAsync(document.RootElement, output, 0);

        string name = field switch
        {
            "issuedThrough" => "Labels issued",
            "publishedThrough" => "Labels published",
            _ => field
        };
        name = char.ToUpperInvariant(name[0]) + name[1..];
        Assert.Equal($"{prefix}{name}: 1{Environment.NewLine}", output.ToString());
    }

    [Theory]
    [InlineData(0, "Full label history available.")]
    [InlineData(1, "Available from label 2 onwards.")]
    [InlineData(12, "Available from label 13 onwards.")]
    [InlineData(long.MaxValue, "Available from label 9223372036854775808 onwards.")]
    public async Task ReadableReplayFloorExplainsAvailableHistory(long floor, string expected)
    {
        using System.Text.Json.JsonDocument document = System.Text.Json.JsonDocument.Parse($"{{\"replayFloor\":{floor}}}");
        using StringWriter output = new();
        await Program.WriteReadableResultAsync(document.RootElement, output, 0);

        Assert.Equal($"⏮   Label history: {expected}{Environment.NewLine}", output.ToString());
    }

    [Fact]
    public async Task ReadableStateShowsIssuerThenPhaseBeforeStreamIdentifier()
    {
        using System.Text.Json.JsonDocument document = System.Text.Json.JsonDocument.Parse(
            """{"state":{"streamId":"test-stream","issuer":"did:plc:test","issuedThrough":12,"phase":"Active","revision":24}}""");
        using StringWriter output = new();
        await Program.WriteReadableResultAsync(document.RootElement, output, 0);

        Assert.Equal(
            "ℹ   State:" + Environment.NewLine +
            "  🪪  Issuer: did:plc:test" + Environment.NewLine +
            "  🚦  Phase: Active" + Environment.NewLine +
            "  🌊  StreamId: test-stream" + Environment.NewLine +
            "  🏷️  Labels issued: 12" + Environment.NewLine,
            output.ToString());
    }

    [Theory]
    [InlineData("Active", false)]
    [InlineData("Retiring", true)]
    [InlineData("Distributing", true)]
    [InlineData("Grace", true)]
    [InlineData("Retired", true)]
    [InlineData("Purging", true)]
    [InlineData("Purged", true)]
    public async Task RetirementCheckpointsAreHiddenUntilRetirementBegins(string phase, bool visible)
    {
        using System.Text.Json.JsonDocument document = System.Text.Json.JsonDocument.Parse(
            $$$"""{"state":{"retirementUpperBound":0,"phase":"{{{phase}}}","retirementThrough":0,"issuedThrough":12}}""");
        using StringWriter output = new();
        await Program.WriteReadableResultAsync(document.RootElement, output, 0);

        Assert.Equal(visible, output.ToString().Contains("RetirementUpperBound:", StringComparison.Ordinal));
        Assert.Equal(visible, output.ToString().Contains("RetirementThrough:", StringComparison.Ordinal));
        Assert.Contains($"Phase: {phase}", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("Labels issued: 12", output.ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("""{"succeeded":true,"state":{"issuer":"did:plc:test","sequence":42}}""", 200, 0)]
    [InlineData("""{"succeeded":false,"error":"Unauthorized"}""", 403, 4)]
    [InlineData("not json", 500, 5)]
    public async Task DefaultOutputDoesNotPrintRawJson(string body, int status, int expected)
    {
        using StringWriter output = new();
        using StringWriter diagnostics = new();
        int result = await Program.WriteResponseAsync(
            body: body,
            statusCode: (HttpStatusCode)status,
            output: output,
            diagnostics: diagnostics);

        Assert.Equal(expected, result);
        Assert.DoesNotContain(body, output.ToString(), StringComparison.Ordinal);
        if (expected == 0)
        {
            Assert.Contains("Issuer: did:plc:test", output.ToString(), StringComparison.Ordinal);
            Assert.Contains("Sequence: 42", output.ToString(), StringComparison.Ordinal);
        }
        else
        {
            Assert.Empty(output.ToString());
        }
    }

    [Theory]
    [InlineData("--verbose status")]
    [InlineData("status --verbose")]
    [InlineData("policy status --verbose")]
    [InlineData("rotation publish-plc --help --verbose")]
    public void VerboseIsAvailableThroughoutTheCommandTree(string arguments)
    {
        ParseResult parsed = Program.CreateRootCommand().Parse(arguments.Split(' '));
        Assert.Empty(parsed.Errors);
        Assert.True(parsed.GetValue<bool>("--verbose"));
    }

    [Fact]
    public async Task CallbackTimeoutStopsWithoutReturningCallbackData()
    {
        using StringWriter diagnostics = new();
        string? result = await Program.WaitForSignInCallbackAsync(
            wait: _ => Task.FromException<string>(new OperationCanceledException()),
            diagnostics: diagnostics,
            cancellationToken: CancellationToken.None);

        Assert.Null(result);
        Assert.Contains("timed out", diagnostics.ToString(), StringComparison.Ordinal);
        Assert.Contains("No further workflow steps", diagnostics.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task CallerCancellationIsNotMisreportedAsTimeout()
    {
        using CancellationTokenSource cancellation = new();
        await cancellation.CancelAsync();
        using StringWriter diagnostics = new();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Program.WaitForSignInCallbackAsync(
            wait: token => Task.FromCanceled<string>(token),
            diagnostics: diagnostics,
            cancellationToken: cancellation.Token));

        Assert.Empty(diagnostics.ToString());
    }

    [Theory]
    [InlineData("?code=private-code&state=private-state", "Xenon sign-in received")]
    [InlineData("?error=access_denied&state=private-state", "Xenon sign-in was not completed")]
    public async Task CallbackUsesQuietStaticXenonBranding(string query, string expectedHeading)
    {
        using CancellationTokenSource watchdog = new(TimeSpan.FromSeconds(30));
        await using CallbackServer callback = await Program.CreateCallbackAsync(watchdog.Token);
        Assert.Same(NullLoggerFactory.Instance, callback.LoggerFactory);
        Task<string> waiting = callback.WaitForCallbackAsync(cancellationToken: watchdog.Token);
        using HttpClient client = new();
        using HttpResponseMessage response = await client.GetAsync(new Uri(callback.Uri, query), watchdog.Token);
        string html = await response.Content.ReadAsStringAsync(watchdog.Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains($"</h1>", html, StringComparison.Ordinal);
        Assert.Contains(expectedHeading, html, StringComparison.Ordinal);
        Assert.Contains("<body><p aria-hidden=\"true\">&nbsp;</p><h1>", html, StringComparison.Ordinal);
        Assert.Contains("<title>Xenon sign-in", html, StringComparison.Ordinal);
        Assert.DoesNotContain("</head>Xenon sign-in<body>", html, StringComparison.Ordinal);
        Assert.Contains("data:image/svg+xml;base64,", html, StringComparison.Ordinal);
        Assert.Contains($"<p id=\"callback-build\" style=\"text-align:right;font-size:.75rem\">Build <code>{Program.GetBuildCommitId()}</code></p>", html, StringComparison.Ordinal);
        Assert.Contains("aria-hidden=\"true\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("private-code", html, StringComparison.Ordinal);
        Assert.DoesNotContain("private-state", html, StringComparison.Ordinal);
        Assert.True(response.Headers.Contains("Content-Security-Policy"));
        Assert.Equal(query, await waiting);
    }
}
