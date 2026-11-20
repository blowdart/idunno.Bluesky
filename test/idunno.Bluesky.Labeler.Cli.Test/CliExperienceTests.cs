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
    [Fact]
    public void EveryCommandOptionAndArgumentHasHelpText()
    {
        List<string> missing = [];
        void CheckCommand(Command command, string path)
        {
            if (string.IsNullOrWhiteSpace(command.Description))
            {
                missing.Add($"{path} (command)");
            }

            foreach (Option option in command.Options)
            {
                if (string.IsNullOrWhiteSpace(option.Description))
                {
                    missing.Add($"{path} {option.Name} (option)");
                }
            }

            foreach (Argument argument in command.Arguments)
            {
                if (string.IsNullOrWhiteSpace(argument.Description))
                {
                    missing.Add($"{path} {argument.Name} (argument)");
                }
            }

            foreach (Command subcommand in command.Subcommands)
            {
                CheckCommand(subcommand, $"{path} {subcommand.Name}");
            }
        }

        CheckCommand(Program.CreateRootCommand(), "root");
        Assert.True(missing.Count == 0, $"Missing descriptions: {string.Join(", ", missing)}");
    }

    [Theory]
    [InlineData("status", "current policy", "verification state")]
    [InlineData("version", "one retained policy", "version ID")]
    [InlineData("versions", "list retained policy", "page")]
    [InlineData("history", "administration", "verification events")]
    [InlineData("operation", "recorded result", "operation ID")]
    [InlineData("save", "unpublished", "JSON file")]
    [InlineData("begin", "block reports", "no PDS write")]
    [InlineData("publish", "conditional PDS write", "labeler-account")]
    [InlineData("recover", "original operation ID", "conditionally write or verify")]
    [InlineData("resume", "paused label withdrawal", "does not write to the PDS")]
    [InlineData("verify", "update local state", "--check-only")]
    [InlineData("abandon", "pending publication", "issuance remain blocked")]
    public void PolicyCommandDescriptionsExplainTheirDistinctPurposes(string name, string purpose, string distinction)
    {
        Command command = Program.CreatePolicyCommand().Subcommands.Single(command => command.Name == name);

        Assert.Contains(purpose, command.Description, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(distinction, command.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PolicyCommandsDoNotReuseHelpDescriptions()
    {
        List<string?> descriptions = [.. Program.CreatePolicyCommand().Subcommands.Select(command => command.Description)];

        Assert.Equal(descriptions.Count, descriptions.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void PolicyVerifyHelpExplainsAuditReasonAndConfirmation()
    {
        Command verify = Program.CreatePolicyCommand().Subcommands.Single(command => command.Name == "verify");
        string? reason = verify.Options.Single(option => option.Name == "--reason").Description;
        string? yes = verify.Options.Single(option => option.Name == "--yes").Description;
        string? checkOnly = verify.Options.Single(option => option.Name == "--check-only").Description;

        Assert.Contains("audit reason", reason, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("confirmation prompt", yes, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("authorization", yes, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("without changing local verification state", checkOnly, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PolicyVerificationConfirmationExplainsItsLocalEffect()
    {
        string prompt = Program.GetPolicyConfirmationPrompt("verify");

        Assert.Contains("Verify the published policy", prompt, StringComparison.Ordinal);
        Assert.Contains("local verification state", prompt, StringComparison.Ordinal);
        Assert.Contains("without changing the published policy", prompt, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("begin")]
    [InlineData("publish")]
    [InlineData("recover")]
    [InlineData("resume")]
    public void PolicyPublicationConfirmationStillWarnsOfIrreversibleWithdrawals(string command)
    {
        string prompt = Program.GetPolicyConfirmationPrompt(command);

        Assert.Contains("irreversible batched negation of removed label values", prompt, StringComparison.Ordinal);
    }

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
            rememberMe: rememberMe,
            verbose: true);

        string expected = "🔑  Xenon starting sign-in for blowdart.me." + Environment.NewLine;
        if (purpose is not null)
        {
            expected += $"⚠   {purpose}" + Environment.NewLine;
        }
        expected += (rememberMe
            ? "💾  Credential persistence was requested with --remember-me."
            : "ℹ   Reusing saved credentials when available; otherwise this sign-in is temporary. Use --remember-me to save a new session.") + Environment.NewLine;
        Assert.Equal(expected, diagnostics.ToString());
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData(null, true)]
    [InlineData("Labeler-account authority to publish the declaration.", false)]
    [InlineData("Labeler-account authority to publish the declaration.", true)]
    public async Task DefaultSignInNoticeShowsOnlyAccountPurposeWarnings(string? purpose, bool rememberMe)
    {
        using StringWriter diagnostics = new();
        await Program.WriteSignInNoticeAsync(
            diagnostics: diagnostics,
            handle: new Handle("blowdart.me"),
            labelerPurpose: purpose,
            rememberMe: rememberMe);

        Assert.Equal(purpose is null ? string.Empty : $"⚠   {purpose}{Environment.NewLine}", diagnostics.ToString());
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
        Assert.Empty(root.Parse(["counts", "--labeler-url", "https://labeler.example"]).Errors);
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
        Assert.Contains("reused automatically", description, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(new string[0], null)]
    [InlineData(new[] { "operator.example" }, "operator.example")]
    [InlineData(new[] { "operator.example", "other.example" }, null)]
    public void RememberedAccountSelectionRequiresOneUnambiguousMatch(string[] accounts, string? expected)
    {
        Assert.Equal(expected, Program.SelectRememberedAccount(accounts));
    }

    [Fact]
    public void ForgetMeIsAvailableAsARootCommand()
    {
        ParseResult parsed = Program.CreateRootCommand().Parse(["forget-me"]);

        Assert.Empty(parsed.Errors);
    }

    [Fact]
    public void ListCredentialsIsAvailableAsARootCommand()
    {
        ParseResult parsed = Program.CreateRootCommand().Parse(["list-credentials"]);

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
            verbose: true,
            cancellationToken: TestContext.Current.CancellationToken);

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
            diagnostics: diagnostics,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(0, result);
        Assert.Equal(
            "ℹ   Moderation:" + Environment.NewLine +
            "  📋  Cases: 12" + Environment.NewLine +
            "  📨  Reports: 0" + Environment.NewLine +
            "  ⚖   Appeals: 3" + Environment.NewLine,
            output.ToString());
        Assert.Empty(diagnostics.ToString());
    }

    [Theory]
    [InlineData(0, true, false, "Viewer", "👁️", "🟢  Status: Active")]
    [InlineData(1, false, false, "Moderator", "⚖️", "⏸   Status: Inactive")]
    [InlineData(2, false, true, "Administrator", "👑", "🚫  Status: Removed")]
    [InlineData(2, true, true, "Administrator", "👑", "🚫  Status: Removed")]
    [InlineData(99, true, false, "Unknown (99)", "🛡️", "🟢  Status: Active")]
    public async Task TeamOutputUsesDidHeadingsVerifiedHandlesAndNamedRoles(
        int role,
        bool active,
        bool removed,
        string expectedRole,
        string expectedIcon,
        string expectedStatus)
    {
        string body = $$$"""
            {"succeeded":true,"members":[{"did":"did:plc:member","role":{{{role}}},"active":{{{active.ToString().ToLowerInvariant()}}},"removed":{{{removed.ToString().ToLowerInvariant()}}},"notificationEmail":null}]}
            """;
        using StringWriter output = new();
        using StringWriter diagnostics = new();
        int resolutions = 0;
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        int result = await Program.WriteResponseAsync(
            body: body,
            statusCode: HttpStatusCode.OK,
            output: output,
            diagnostics: diagnostics,
            resolveHandle: (did, token) =>
            {
                Assert.Equal("did:plc:member", did.Value);
                Assert.Equal(cancellationToken, token);
                resolutions++;
                return Task.FromResult(new Handle("member.example"));
            },
            cancellationToken: cancellationToken);

        string heading = !active || removed ? "\u001b[9mdid:plc:member\u001b[29m" : "did:plc:member";
        Assert.Equal(0, result);
        Assert.Equal(1, resolutions);
        Assert.Equal(
            "👤  Members:" + Environment.NewLine +
            $"  🔗  DID: {heading}" + Environment.NewLine +
            "    🪪  Handle: @member.example" + Environment.NewLine +
            $"    {expectedIcon}  Role: {expectedRole}" + Environment.NewLine +
            $"    {expectedStatus}" + Environment.NewLine +
            "    📧  Notification email: (none)" + Environment.NewLine,
            output.ToString());
        Assert.Empty(diagnostics.ToString());
    }

    [Theory]
    [InlineData("invalid", "Unavailable (no verified handle)")]
    [InlineData("failure", "Unavailable (lookup failed)")]
    [InlineData("timeout", "Unavailable (lookup timed out)")]
    public async Task TeamOutputRetainsDidWhenVerifiedHandleIsUnavailable(string outcome, string expected)
    {
        using StringWriter output = new();
        using StringWriter diagnostics = new();
        int result = await Program.WriteResponseAsync(
            body: """{"succeeded":true,"members":[{"did":"did:plc:member","role":0,"active":true,"removed":false}]}""",
            statusCode: HttpStatusCode.OK,
            output: output,
            diagnostics: diagnostics,
            resolveHandle: (_, _) => outcome switch
            {
                "failure" => Task.FromException<Handle>(new HttpRequestException()),
                "timeout" => Task.FromException<Handle>(new OperationCanceledException()),
                _ => Task.FromResult(Handle.Invalid)
            },
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(0, result);
        Assert.Contains("🔗  DID: did:plc:member", output.ToString(), StringComparison.Ordinal);
        Assert.Contains($"🪪  Handle: {expected}", output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task TeamOutputGroupsEachPersonUnderTheirOwnDid()
    {
        using StringWriter output = new();
        using StringWriter diagnostics = new();
        int result = await Program.WriteResponseAsync(
            body: """
                {"succeeded":true,"members":[
                  {"did":"did:plc:first","role":2,"active":true,"removed":false,"notificationEmail":"first@example.com"},
                  {"did":"did:plc:second","role":1,"active":false,"removed":false}
                ]}
                """,
            statusCode: HttpStatusCode.OK,
            output: output,
            diagnostics: diagnostics,
            resolveHandle: (did, _) => Task.FromResult(new Handle(did.Value == "did:plc:first" ? "first.example" : "second.example")),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(0, result);
        Assert.Equal(
            "👤  Members:" + Environment.NewLine +
            "  🔗  DID: did:plc:first" + Environment.NewLine +
            "    🪪  Handle: @first.example" + Environment.NewLine +
            "    👑  Role: Administrator" + Environment.NewLine +
            "    🟢  Status: Active" + Environment.NewLine +
            "    📧  Notification email: first@example.com" + Environment.NewLine +
            "  🔗  DID: \u001b[9mdid:plc:second\u001b[29m" + Environment.NewLine +
            "    🪪  Handle: @second.example" + Environment.NewLine +
            "    ⚖️  Role: Moderator" + Environment.NewLine +
            "    ⏸   Status: Inactive" + Environment.NewLine,
            output.ToString());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EmptyAndVerboseTeamOutputDoNotResolveHandles(bool verbose)
    {
        string body = verbose
            ? """{"succeeded":true,"members":[{"did":"did:plc:member","role":0,"active":true,"removed":false}]}"""
            : """{"succeeded":true,"members":[]}""";
        using StringWriter output = new();
        using StringWriter diagnostics = new();
        int result = await Program.WriteResponseAsync(
            body: body,
            statusCode: HttpStatusCode.OK,
            output: output,
            diagnostics: diagnostics,
            verbose: verbose,
            resolveHandle: (_, _) => throw new InvalidOperationException("No handle lookup was expected."),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(0, result);
        Assert.Equal(
            verbose ? body + Environment.NewLine : "👤  Members:" + Environment.NewLine + "  ℹ   None" + Environment.NewLine,
            output.ToString());
    }

    [Fact]
    public async Task TeamOutputSanitizesUntrustedTextBeforeAddingStrikethrough()
    {
        using StringWriter output = new();
        using StringWriter diagnostics = new();
        await Program.WriteResponseAsync(
            body: """{"succeeded":true,"members":[{"did":"bad\u001b[0m","role":0,"active":false,"removed":true,"notificationEmail":"unsafe\u001b[0m"}]}""",
            statusCode: HttpStatusCode.OK,
            output: output,
            diagnostics: diagnostics,
            resolveHandle: (_, _) => throw new InvalidOperationException("An invalid DID cannot be resolved."),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Contains("DID: \u001b[9mbad [0m\u001b[29m", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("Handle: Unavailable (invalid DID)", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("Notification email: unsafe [0m", output.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("\u001b[0m", output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task TeamHandleLookupHonorsCancellation()
    {
        using CancellationTokenSource cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        using StringWriter output = new();
        using StringWriter diagnostics = new();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Program.WriteResponseAsync(
            body: """{"succeeded":true,"members":[{"did":"did:plc:member","role":0,"active":true,"removed":false}]}""",
            statusCode: HttpStatusCode.OK,
            output: output,
            diagnostics: diagnostics,
            resolveHandle: (_, token) =>
            {
                cancellation.Cancel();
                return Task.FromCanceled<Handle>(token);
            },
            cancellationToken: cancellation.Token));
    }

    [Theory]
    [InlineData("members", "👤  ")]
    [InlineData("labels", "🏷️  ")]
    [InlineData("issuer", "🪪  ")]
    [InlineData("did", "🔗  ")]
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
        await Program.WriteReadableResultAsync(
            element: document.RootElement,
            output: output,
            depth: 0,
            cancellationToken: TestContext.Current.CancellationToken);

        string name = field switch
        {
            "did" => "DID",
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
        await Program.WriteReadableResultAsync(
            element: document.RootElement,
            output: output,
            depth: 0,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal($"⏮   Label history: {expected}{Environment.NewLine}", output.ToString());
    }

    [Fact]
    public async Task ReadableStateShowsIssuerThenPhaseBeforeStreamIdentifier()
    {
        using System.Text.Json.JsonDocument document = System.Text.Json.JsonDocument.Parse(
            """{"state":{"streamId":"test-stream","issuer":"did:plc:test","issuedThrough":12,"phase":"Active","revision":24}}""");
        using StringWriter output = new();
        await Program.WriteReadableResultAsync(
            element: document.RootElement,
            output: output,
            depth: 0,
            cancellationToken: TestContext.Current.CancellationToken);

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
        await Program.WriteReadableResultAsync(
            element: document.RootElement,
            output: output,
            depth: 0,
            cancellationToken: TestContext.Current.CancellationToken);

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
            diagnostics: diagnostics,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(expected, result);
        Assert.DoesNotContain(body, output.ToString(), StringComparison.Ordinal);
        if (expected == 0)
        {
            Assert.Empty(diagnostics.ToString());
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
