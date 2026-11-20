// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Buffers.Binary;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.Diagnostics;
using System.IO.Pipes;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

using idunno.AtProto;
using idunno.Bluesky.AspNet.Labeler;
using idunno.Bluesky.Labeler.Administration;

namespace idunno.Bluesky.Labeler.Cli.Test;

public class AdministrationCliValidationTests
{
    [Fact]
    public void PlcSignInsRequestOnlyIdentityPermissions()
    {
        Uri clientId = new("https://labeler.example/native-client-metadata.json");
        AtProto.Authentication.OAuthOptions options = Program.CreatePlcOAuthOptions(clientId);
        Assert.Equal(clientId.AbsoluteUri, options.ClientId);
        Assert.Equal(["atproto", "identity:*"], options.Scopes);
    }

    [Fact]
    public void RemoteAdminSignInRequestsAdvertisedPermissionsForTheSelectedService()
    {
        Uri clientId = new("https://labeler.example/native-client-metadata.json");
        AtProtoServiceReference audience = new(new Did("did:plc:labeler"), "labeler_admin");
        const string encodedAudience = "did%3Aplc%3Alabeler%23labeler_admin";
        string advertisedScope = string.Join(
            ' ',
            "atproto",
            "identity:*",
            "repo:app.bsky.labeler.service?action=create",
            $"rpc:dev.idunno.labeler.admin.beginPolicyPublication?aud={encodedAudience}",
            $"rpc:dev.idunno.labeler.admin.resumePolicyPublication?aud={encodedAudience}",
            $"rpc:dev.idunno.labeler.admin.getPolicy?aud={encodedAudience}",
            $"rpc:dev.idunno.labeler.admin.verifyPolicy?aud={encodedAudience}",
            "rpc:dev.idunno.labeler.admin.getTeam?aud=did%3Aplc%3Aother%23labeler_admin");

        bool created = Program.TryCreateRemoteOAuthOptions(
            clientId: clientId,
            audience: audience,
            requiredMethods: ["beginPolicyPublication", "resumePolicyPublication", "getPolicy", "verifyPolicy"],
            advertisedScope: advertisedScope,
            options: out AtProto.Authentication.OAuthOptions? options);

        Assert.True(created);
        Assert.NotNull(options);
        Assert.Equal(clientId.AbsoluteUri, options.ClientId);
        Assert.Equal(
            [
                "atproto",
                $"rpc:dev.idunno.labeler.admin.beginPolicyPublication?aud={encodedAudience}",
                $"rpc:dev.idunno.labeler.admin.getPolicy?aud={encodedAudience}",
                $"rpc:dev.idunno.labeler.admin.resumePolicyPublication?aud={encodedAudience}",
                $"rpc:dev.idunno.labeler.admin.verifyPolicy?aud={encodedAudience}"
            ],
            options.Scopes);
    }

    [Fact]
    public void RemoteAdminSignInRejectsUnadvertisedPermissions()
    {
        Uri clientId = new("https://labeler.example/native-client-metadata.json");
        AtProtoServiceReference audience = new(new Did("did:plc:labeler"), "labeler_admin");

        bool created = Program.TryCreateRemoteOAuthOptions(
            clientId: clientId,
            audience: audience,
            requiredMethods: ["getTeam", "changeTeam"],
            advertisedScope: "rpc:dev.idunno.labeler.admin.getTeam?aud=did%3Aplc%3Alabeler%23labeler_admin",
            options: out AtProto.Authentication.OAuthOptions? options);

        Assert.False(created);
        Assert.Null(options);
    }

    [Fact]
    public void ReadOnlyPolicyCheckRequestsOnlyThePolicyReadPermission()
    {
        IReadOnlyList<string> methods = Program.SelectRemoteRequiredMethods(
            method: "getPolicy",
            hasWorkflow: true,
            workflowMethods: ["getPolicy"]);

        Assert.Equal(["getPolicy"], methods);
    }

    [Theory]
    [InlineData("https://labeler.example", true)]
    [InlineData("http://127.0.0.1:8080/health/labeler/ready", true)]
    [InlineData("not-a-uri", false)]
    [InlineData("/relative/path", false)]
    [InlineData("file:///C:/labeler", false)]
    public void UriOptionsUseTheCommandLineParser(string value, bool valid)
    {
        Option<Uri?> option = Program.CreateUriOption("--endpoint", "The endpoint.", required: true);
        RootCommand command = [];
        command.Options.Add(option);
        ParseResult parsed = command.Parse(["--endpoint", value]);
        Assert.Equal(valid, parsed.Errors.Count == 0);
        if (valid)
        {
            Assert.Equal(new Uri(value), parsed.GetValue(option));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MissingUriOptionsRespectRequiredness(bool required)
    {
        Option<Uri?> option = Program.CreateUriOption("--labeler-url", "The endpoint.", required);
        RootCommand command = [];
        command.Options.Add(option);
        ParseResult parsed = command.Parse([]);
        Assert.Equal(required, parsed.Errors.Count > 0);
        if (!required)
        {
            Assert.Null(parsed.GetValue(option));
        }

        Assert.NotEmpty(command.Parse(["--labeler-url"]).Errors);
    }

    [Theory]
    [InlineData("did:plc:administration-actor", null, true)]
    [InlineData("alice.bsky.social", null, true)]
    [InlineData("at://did:plc:administration-actor/app.bsky.feed.post/3k2l3z4q5x6y7", null, true)]
    [InlineData("at://did:plc:administration-actor/app.bsky.feed.post/3k2l3z4q5x6y7", "bafyreievgu2ty7qbiaaom5zhmkznsnajuzideek3lo7e65dwqlrvrxnmo4", true)]
    [InlineData("did:plc:administration-actor", "bafyreievgu2ty7qbiaaom5zhmkznsnajuzideek3lo7e65dwqlrvrxnmo4", false)]
    [InlineData("at://not-a-repository/app.bsky.feed.post/record", null, false)]
    [InlineData("did:plc:administration-actor", "not-a-cid", false)]
    public void LabelTargetRequiresValidAccountOrRecordUri(string target, string? cid, bool expected)
    {
        Assert.Equal(expected, Program.ValidateLabelTarget(target, cid));
    }

    [Theory]
    [InlineData("did:web:labeler.example#labeler_admin", true)]
    [InlineData("did:plc:administration-actor#labeler_admin", true)]
    [InlineData("did:web:labeler.example", false)]
    [InlineData("not-a-did#labeler_admin", false)]
    [InlineData("did:web:labeler.example#", false)]
    public void ServiceReferenceRequiresAnExactDidAndFragment(string text, bool expected)
    {
        Assert.Equal(expected, Program.TryParseServiceReference(text, out AtProtoServiceReference? reference));
        Assert.Equal(expected, reference is not null);
    }

    [Fact]
    public void PLCRecoveryRequiresTheExactPublishedKeyAndEndpoint()
    {
        Did did = new("did:plc:labeler");
        const string publicKey = "zreplacement";
        Uri endpoint = new("https://labeler.example");
        DidDocument document = JsonSerializer.Deserialize<DidDocument>(
            """
            {
              "id": "did:plc:labeler",
              "verificationMethod": [
                {
                  "id": "did:plc:labeler#atproto_label",
                  "type": "Multikey",
                  "controller": "did:plc:labeler",
                  "publicKeyMultibase": "zreplacement"
                }
              ],
              "service": [
                {
                  "id": "did:plc:labeler#atproto_labeler",
                  "type": "AtprotoLabeler",
                  "serviceEndpoint": "https://labeler.example"
                }
              ]
            }
            """,
            AtProtoJsonSerializerOptions.Default)!;

        Assert.True(Program.HasPublishedPlcIdentity(document, did, publicKey, endpoint));
        Assert.False(Program.HasPublishedPlcIdentity(document, did, "zother", endpoint));
        Assert.False(Program.HasPublishedPlcIdentity(document, did, publicKey, new Uri("https://other.example")));
    }

    [Theory]
    [InlineData("https://labeler.example", true)]
    [InlineData("https://labeler.example/", true)]
    [InlineData("http://labeler.example", false)]
    [InlineData("https://user@labeler.example", false)]
    [InlineData("https://labeler.example/admin", false)]
    [InlineData("https://labeler.example?mode=admin", false)]
    [InlineData("https://labeler.example#admin", false)]
    public void RemoteOriginMustBeHttpsAndHaveNoPathOrCredentials(string text, bool expected)
    {
        Assert.Equal(expected, Program.IsHttpsOrigin(new Uri(text)));
    }

    [Theory]
    [InlineData("https://labeler.example/native-client-metadata.json", true)]
    [InlineData("https://other.example/native-client-metadata.json", false)]
    [InlineData("http://labeler.example/native-client-metadata.json", false)]
    [InlineData("https://user@labeler.example/native-client-metadata.json", false)]
    [InlineData("https://labeler.example/native-client-metadata.json?client=1", false)]
    [InlineData("https://labeler.example:8443/native-client-metadata.json", false)]
    public void NativeOAuthMetadataMustShareTheLabelerOrigin(string text, bool expected)
    {
        Assert.Equal(expected, Program.IsNativeClientMetadataUri(new Uri(text), new Uri("https://labeler.example")));
    }

    [Theory]
    [InlineData("v9.0.0", true)]
    [InlineData("9.0.0", false)]
    [InlineData("v9.0", false)]
    [InlineData("v9.0.0-preview.1", false)]
    public void SelfUpdateRequiresAnExactStableReleaseTag(string tag, bool expected)
    {
        Assert.Equal(expected, Program.IsStableReleaseTag(tag));
    }

    [Theory]
    [InlineData("win-x64", "idunno.Bluesky.Labeler.Cli-win-x64.exe")]
    [InlineData("win-arm64", "idunno.Bluesky.Labeler.Cli-win-arm64.exe")]
    [InlineData("linux-x64", "idunno.Bluesky.Labeler.Cli-linux-x64")]
    [InlineData("linux-arm64", "idunno.Bluesky.Labeler.Cli-linux-arm64")]
    public void SelfUpdateSelectsTheDirectRuntimeExecutable(string runtimeIdentifier, string expected)
    {
        Assert.Equal(expected, Program.GetCliReleaseAssetName(runtimeIdentifier));
    }

    [Fact]
    public async Task SelfUpdateStagesBinaryBytesWithoutOverwritingExistingFiles()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        string directory = Directory.CreateTempSubdirectory().FullName;
        try
        {
            string stagedPath = Path.Combine(directory, "cli.new");
            byte[] executable = [0x4d, 0x5a, 0, 0xff];
            await Program.StageExecutableAsync(executable, stagedPath, token);
            Assert.Equal(executable, await File.ReadAllBytesAsync(stagedPath, token));
            if (!OperatingSystem.IsWindows())
            {
                Assert.Equal(
                    UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute,
                    File.GetUnixFileMode(stagedPath));
            }

            await Assert.ThrowsAsync<IOException>(() => Program.StageExecutableAsync([1, 2], stagedPath, token));
            Assert.Equal(executable, await File.ReadAllBytesAsync(stagedPath, token));
            string emptyPath = Path.Combine(directory, "empty.new");
            await Assert.ThrowsAsync<InvalidDataException>(() => Program.StageExecutableAsync([], emptyPath, token));
            Assert.False(File.Exists(emptyPath));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Theory]
    [InlineData("ghcr.io/blowdart/idunno-bluesky-labeler@sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", true)]
    [InlineData("ghcr.io/blowdart/idunno-bluesky-labeler-postgresql@sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", true)]
    [InlineData("ghcr.io/blowdart/idunno-bluesky-labeler-mysql@sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", true)]
    [InlineData("ghcr.io/blowdart/idunno-bluesky-labeler-base@sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", false)]
    [InlineData("ghcr.io/blowdart/idunno-bluesky-labeler-mysql:latest", false)]
    [InlineData("ghcr.io/blowdart/idunno-bluesky-labeler:latest", false)]
    [InlineData("ghcr.io/other/image@sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", false)]
    [InlineData("ghcr.io/blowdart/idunno-bluesky-labeler@sha256:xyz", false)]
    public void ServiceUpdatesRequireThePublishedImagePinnedByDigest(string image, bool expected)
    {
        Assert.Equal(expected, Program.IsLabelerImageDigest(image));
    }

    [Theory]
    [InlineData("https://labeler.example/health/labeler/ready", true)]
    [InlineData("http://127.0.0.1:8080/health/labeler/ready", true)]
    [InlineData("https://labeler.example/health/labeler/ready?token=secret", false)]
    [InlineData("https://labeler.example/health/labeler/live", false)]
    [InlineData("https://user@labeler.example/health/labeler/ready", false)]
    public void ServiceUpdateRequiresAPlainReadinessEndpoint(string text, bool expected)
    {
        Assert.Equal(expected, Program.IsReadinessUri(new Uri(text)));
    }

    [Theory]
    [InlineData("getTeam", true)]
    [InlineData("getPublicationState", true)]
    [InlineData("getAudit", true)]
    [InlineData("getOperation", true)]
    [InlineData("issueLabel", false)]
    [InlineData("changeTeam", false)]
    public void RemoteAdministrationMethodsUseTheMatchingHttpVerb(string method, bool expectedGet)
    {
        Assert.Equal(expectedGet, Program.IsRemoteGetMethod(method));
    }

    [Fact]
    public void RemoteOperationLookupEncodesItsQueryParameters()
    {
        Uri requestUri = Program.BuildRemoteRequestUri(
            adminUrl: new Uri("https://labeler.example"),
            lxm: "dev.idunno.labeler.admin.getOperation",
            method: "getOperation",
            operationId: "operation +/1",
            reason: "appeal & review",
            afterSequence: 0,
            limit: 16);

        Assert.Equal(
            "https://labeler.example/xrpc/dev.idunno.labeler.admin.getOperation?operationId=operation%20%2B%2F1&reason=appeal%20%26%20review",
            requestUri.AbsoluteUri);
    }

    [Theory]
    [InlineData("viewer", LabelTeamRole.Viewer)]
    [InlineData("MODERATOR", LabelTeamRole.Moderator)]
    [InlineData("Administrator", LabelTeamRole.Administrator)]
    public void ParsesOnlyDefinedTeamRoles(string text, LabelTeamRole expected)
    {
        Assert.True(Program.TryParseRole(text, out LabelTeamRole role));
        Assert.Equal(expected, role);
    }

    [Theory]
    [InlineData("99")]
    [InlineData("unknown")]
    [InlineData("")]
    public void RejectsUndefinedTeamRoles(string text)
    {
        Assert.False(Program.TryParseRole(text, out _));
    }

    [Theory]
    [InlineData("SetRole", LabelTeamAction.SetRole)]
    [InlineData("suspend", LabelTeamAction.Suspend)]
    [InlineData("RevokeLocal", LabelTeamAction.RevokeLocal)]
    [InlineData("TransferOwnership", LabelTeamAction.TransferOwnership)]
    public void ParsesSupportedTeamActions(string text, LabelTeamAction expected)
    {
        Assert.True(Program.TryParseAction(text, out LabelTeamAction action));
        Assert.Equal(expected, action);
    }

    [Theory]
    [InlineData("Enroll")]
    [InlineData("99")]
    [InlineData("unknown")]
    [InlineData("")]
    public void RejectsUnsupportedTeamActions(string text)
    {
        Assert.False(Program.TryParseAction(text, out _));
    }

    [Theory]
    [InlineData(LabelTeamAction.SetRole, true, false, true)]
    [InlineData(LabelTeamAction.SetRole, false, false, false)]
    [InlineData(LabelTeamAction.Suspend, false, false, true)]
    [InlineData(LabelTeamAction.Suspend, true, false, false)]
    [InlineData(LabelTeamAction.BindLocal, false, true, true)]
    [InlineData(LabelTeamAction.BindLocal, false, false, false)]
    [InlineData(LabelTeamAction.RevokeLocal, false, true, true)]
    [InlineData(LabelTeamAction.Remove, false, true, false)]
    [InlineData(LabelTeamAction.TransferOwnership, false, false, true)]
    [InlineData(LabelTeamAction.TransferOwnership, true, false, false)]
    [InlineData(LabelTeamAction.TransferOwnership, false, true, false)]
    public void TeamChangeOptionsMatchTheSelectedAction(
        LabelTeamAction action,
        bool hasRole,
        bool hasCredential,
        bool expected)
    {
        Assert.Equal(expected, Program.HasValidTeamChangeOptions(action, hasRole, hasCredential));
    }

    [Fact]
    public void TeamCommandsAreGroupedAndLegacyCommandsRemainHidden()
    {
        RootCommand root = Program.CreateRootCommand();
        Command team = root.Subcommands.Single(command => command.Name == "team");

        Assert.Equal(["list", "add", "update", "enable", "disable", "remove"], team.Subcommands.Select(command => command.Name));
        Assert.True(root.Subcommands.Single(command => command.Name == "enroll").Hidden);
        Assert.True(root.Subcommands.Single(command => command.Name == "change-team").Hidden);
        Assert.NotEmpty(root.Parse(["team", "disable", "did:plc:member", "--role", "Viewer"]).Errors);
    }

    [Fact]
    public async Task TeamTransportOptionsApplyOnBothSidesOfTheSubcommand()
    {
        RootCommand root = Program.CreateRootCommand();
        Command team = root.Subcommands.Single(command => command.Name == "team");
        Option<Uri?> adminUrl = Assert.IsType<Option<Uri?>>(team.Options.Single(option => option.Name == "--labeler-url"));
        Option<string?> localEndpoint = Assert.IsType<Option<string?>>(team.Options.Single(option => option.Name == "--local-endpoint"));
        Option<string?> adminHandle = Assert.IsType<Option<string?>>(team.Options.Single(option => option.Name == "--admin-handle"));
        Option<bool> rememberMe = Assert.IsType<Option<bool>>(team.Options.Single(option => option.Name == "--remember-me"));
        ParseResult parsed = root.Parse(
            [
                "team", "--labeler-url", "https://labeler.example", "--admin-handle", "admin.example",
                "--remember-me", "disable", "did:plc:member",
                "--local-endpoint", "test-pipe", "--operation-id", "operation", "--reason", "review", "--yes"
            ]);

        Assert.Empty(parsed.Errors);
        Assert.Equal(new Uri("https://labeler.example"), parsed.GetValue(adminUrl));
        Assert.Equal("test-pipe", parsed.GetValue(localEndpoint));
        Assert.Equal("admin.example", parsed.GetValue(adminHandle));
        Assert.True(parsed.GetValue(rememberMe));
        Assert.Equal(2, await parsed.InvokeAsync(cancellationToken: TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("team add did:plc:member --operation-id operation --reason review")]
    [InlineData("team add did:plc:member --role Viewer --operation-id operation")]
    [InlineData("team update --role Viewer --operation-id operation --reason review")]
    [InlineData("team disable did:plc:member --operation-id operation")]
    [InlineData("team remove --operation-id operation --reason review")]
    public void TeamMutationsRequireTheirTargetAndAuditOptions(string command)
    {
        Assert.NotEmpty(Program.CreateRootCommand().Parse(command.Split(' ', StringSplitOptions.RemoveEmptyEntries)).Errors);
    }

    [Theory]
    [InlineData("team list", false, "getTeam", "", -1, -1, null)]
    [InlineData("team list", true, "getTeam", "", -1, -1, null)]
    [InlineData("team", false, "getTeam", "", -1, -1, null)]
    [InlineData("team add member.example --role Moderator --operation-id operation --reason review --yes", false, "enroll", "member.example", -1, 1, null)]
    [InlineData("team update did:plc:member --role Viewer --operation-id operation --reason review --yes", false, "changeTeam", "did:plc:member", 1, 0, null)]
    [InlineData("team enable did:plc:member --operation-id operation --reason review --yes", false, "changeTeam", "did:plc:member", 3, -1, null)]
    [InlineData("team disable did:plc:member --operation-id operation --reason review --yes", true, "changeTeam", "did:plc:member", 2, -1, null)]
    [InlineData("team remove did:plc:member --operation-id operation --reason review --yes", false, "changeTeam", "did:plc:member", 4, -1, null)]
    [InlineData("team update did:plc:member --action BindLocal --credential uid:42 --operation-id operation --reason review --yes", false, "changeTeam", "did:plc:member", 5, -1, "uid:42")]
    [InlineData("team update did:plc:member --action TransferOwnership --operation-id operation --reason review --yes", false, "changeTeam", "did:plc:member", 7, -1, null)]
    [InlineData("enroll member.example --role Moderator --operation-id operation --reason review --yes", false, "enroll", "member.example", -1, 1, null)]
    [InlineData("change-team did:plc:member --action Remove --operation-id operation --reason review --yes", false, "changeTeam", "did:plc:member", 4, -1, null)]
    [InlineData("issue did:plc:member spam --reason review --yes", false, "issueLabel", "did:plc:member", -1, -1, null)]
    [InlineData("negate did:plc:member spam --reason review --yes", false, "negateLabel", "did:plc:member", -1, -1, null)]
    [InlineData("team add member.example --role Moderator --reason review --yes", false, "enroll", "member.example", -1, 1, null)]
    [InlineData("team update did:plc:member --role Viewer --reason review --yes", false, "changeTeam", "did:plc:member", 1, 0, null)]
    [InlineData("team enable did:plc:member --reason review --yes", false, "changeTeam", "did:plc:member", 3, -1, null)]
    [InlineData("team disable did:plc:member --reason review --yes", false, "changeTeam", "did:plc:member", 2, -1, null)]
    [InlineData("team remove did:plc:member --reason review --yes", false, "changeTeam", "did:plc:member", 4, -1, null)]
    [InlineData("policy begin --version v1 --expected-revision 1 --create --reason review --yes", false, "beginPolicyPublication", "", -1, -1, null)]
    [InlineData("policy abandon --expected-revision 1 --reason review --yes", false, "abandonPolicyPublication", "", -1, -1, null)]
    [InlineData("retire preflight --action beginRetirement --reason review", false, "prepareRetirement", "beginRetirement", -1, -1, null)]
    [InlineData("retire pause --reason review --yes", false, "pauseRetirement", "", -1, -1, null)]
    [InlineData("retire resume --reason review --yes", false, "resumeRetirement", "", -1, -1, null)]
    [InlineData("rotation begin --replacement-key-id key-1 --reason review --yes", false, "beginRotation", "", -1, -1, null)]
    [InlineData("team update did:plc:member --operation-id operation --reason review --yes", false, null, "", -1, -1, null)]
    [InlineData("team update did:plc:member --role Viewer --credential uid:42 --operation-id operation --reason review --yes", false, null, "", -1, -1, null)]
    [InlineData("team update did:plc:member --action BindLocal --operation-id operation --reason review --yes", false, null, "", -1, -1, null)]
    [InlineData("team enable member.example --operation-id operation --reason review --yes", false, null, "", -1, -1, null)]
    [InlineData("team add member.example --role unknown --operation-id operation --reason review --yes", false, null, "", -1, -1, null)]
    public async Task TeamCommandsSendTheirExpectedLocalAdministrationIntent(
        string command,
        bool transportBeforeSubcommand,
        string? expectedMethod,
        string expectedTarget,
        int expectedAction,
        int expectedRole,
        string? expectedCredential)
    {
        if (!OperatingSystem.IsWindows() && !OperatingSystem.IsLinux())
        {
            return;
        }

        string name = $"xenon-team-{Guid.NewGuid():N}";
        string endpoint = OperatingSystem.IsWindows() ? name : Path.Combine(Path.GetTempPath(), name + ".sock");
        using CancellationTokenSource stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        stop.CancelAfter(TimeSpan.FromSeconds(15));
        using NamedPipeServerStream? pipe = OperatingSystem.IsWindows()
            ? new(name, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly)
            : null;
        using Socket? listener = OperatingSystem.IsLinux()
            ? new(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified)
            : null;
        if (listener is not null)
        {
            listener.Bind(new UnixDomainSocketEndPoint(endpoint));
            listener.Listen(1);
        }

        Task<string> received = pipe is not null
            ? ReceiveTeamPipeCommandAsync(pipe, stop.Token)
            : ReceiveTeamSocketCommandAsync(listener!, stop.Token);
        try
        {
            string[] words = command.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            string[] args = transportBeforeSubcommand
                ? [words[0], "--local-endpoint", endpoint, .. words[1..]]
                : [.. words, "--local-endpoint", endpoint];
            ParseResult parsed = Program.CreateRootCommand().Parse(args);
            Assert.Empty(parsed.Errors);
            Assert.Equal(expectedMethod is null ? 2 : 0, await parsed.InvokeAsync(cancellationToken: stop.Token));
            if (expectedMethod is null)
            {
                Assert.False(received.IsCompleted);
                return;
            }

            using JsonDocument document = JsonDocument.Parse(await received);
            JsonElement request = document.RootElement;
            Assert.Equal(expectedMethod, request.GetProperty("method").GetString());
            Assert.Equal(expectedTarget, request.GetProperty("target").GetString());
            string? operation = request.GetProperty("operationId").GetString();
            if (expectedMethod == "getTeam")
            {
                Assert.Equal("", operation);
            }
            else if (words.Contains("--operation-id"))
            {
                Assert.Equal("operation", operation);
            }
            else
            {
                Assert.True(Guid.TryParseExact(operation, "N", out _));
            }
            Assert.Equal(expectedMethod == "getTeam" ? "Administrative inspection." : "review", request.GetProperty("reason").GetString());
            if (expectedAction < 0)
            {
                Assert.False(request.TryGetProperty("action", out _));
            }
            else
            {
                Assert.Equal(expectedAction, request.GetProperty("action").GetInt32());
            }

            if (expectedRole < 0)
            {
                Assert.False(request.TryGetProperty("role", out _));
            }
            else
            {
                Assert.Equal(expectedRole, request.GetProperty("role").GetInt32());
            }

            Assert.Equal(expectedCredential, request.TryGetProperty("credential", out JsonElement credential) ? credential.GetString() : null);
        }
        finally
        {
            stop.Cancel();
            try
            {
                await received;
            }
            catch (OperationCanceledException) when (stop.IsCancellationRequested)
            {
            }

            listener?.Dispose();
            if (listener is not null)
            {
                File.Delete(endpoint);
            }
        }
    }

    [Fact]
    public async Task OmittedOperationIdsAreUniqueAndDisplayedBeforeUse()
    {
        using StringWriter output = new();
        string first = await Program.GetOrCreateOperationIdAsync(null, output);
        string second = await Program.GetOrCreateOperationIdAsync(null, output);

        Assert.True(Guid.TryParseExact(first, "N", out _));
        Assert.True(Guid.TryParseExact(second, "N", out _));
        Assert.NotEqual(first, second);
        Assert.Contains($"Operation ID: {first}", output.ToString(), StringComparison.Ordinal);
        Assert.Contains($"Operation ID: {second}", output.ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("provided-id")]
    [InlineData("")]
    public async Task SuppliedOperationIdsAreNotReplaced(string supplied)
    {
        using StringWriter output = new();
        Assert.Equal(supplied, await Program.GetOrCreateOperationIdAsync(supplied, output));
        Assert.Equal("", output.ToString());
    }

    [Theory]
    [InlineData("team add member.example --role Viewer --reason review --yes", null, true, "Select exactly one transport", false)]
    [InlineData("team add member.example --role Viewer --reason review --yes", null, true, "Select exactly one transport", true)]
    [InlineData("issue did:plc:member spam --reason review --yes", null, true, "Select exactly one transport", false)]
    [InlineData("team update did:plc:member --role Viewer --reason review --yes", null, true, "Select exactly one transport", false)]
    [InlineData("retire preflight --action beginRetirement --reason review", null, true, "Select exactly one transport", false)]
    [InlineData("rotation begin --replacement-key-id key-1 --reason review --yes", null, true, "Select exactly one transport", false)]
    [InlineData("policy begin --version v1 --expected-revision 1 --create --reason review --yes", null, true, "Select exactly one transport", false)]
    [InlineData("team add member.example --role Viewer --reason review --yes", "provided-id", false, "Select exactly one transport", true)]
    [InlineData("team add member.example --role Viewer --reason review --yes", "", false, "Supply a valid DID/handle", false)]
    [InlineData("team add member.example --role Viewer --reason review --yes", " ", false, "Supply a valid DID/handle", false)]
    public async Task GeneratedOperationIdRemainsVisibleWhenACommandFails(
        string command,
        string? supplied,
        bool generated,
        string expectedError,
        bool verbose)
    {
        ProcessStartInfo start = new("dotnet")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        start.ArgumentList.Add(typeof(Program).Assembly.Location);
        foreach (string argument in command.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            start.ArgumentList.Add(argument);
        }
        if (verbose)
        {
            start.ArgumentList.Add("--verbose");
        }
        if (supplied is not null)
        {
            start.ArgumentList.Add("--operation-id");
            start.ArgumentList.Add(supplied);
        }

        using Process process = Process.Start(start) ?? throw new InvalidOperationException("The CLI process could not be started.");
        using CancellationTokenSource watchdog = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        watchdog.CancelAfter(TimeSpan.FromSeconds(15));
        try
        {
            Task<string> output = process.StandardOutput.ReadToEndAsync(watchdog.Token);
            Task<string> diagnostics = process.StandardError.ReadToEndAsync(watchdog.Token);
            await process.WaitForExitAsync(watchdog.Token);
            Assert.Equal(2, process.ExitCode);
            string result = await output;
            string error = await diagnostics;
            Assert.Contains(expectedError, error, StringComparison.Ordinal);
            Assert.DoesNotContain("Operation ID:", error, StringComparison.Ordinal);
            if (generated)
            {
                Assert.Matches(@"Operation ID: [a-f0-9]{32}", result);
            }
            else
            {
                Assert.Equal("", result);
            }
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync(CancellationToken.None);
            }
        }
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(true, true)]
    [InlineData(false, true)]
    public async Task OperationIdIsDisplayedBeforeTheResponseAndSentInTheSameRequest(bool generated, bool verbose)
    {
        if (!OperatingSystem.IsWindows() && !OperatingSystem.IsLinux())
        {
            return;
        }

        string name = $"xenon-operation-{Guid.NewGuid():N}";
        string endpoint = OperatingSystem.IsWindows() ? name : Path.Combine(Path.GetTempPath(), name + ".sock");
        using CancellationTokenSource stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        stop.CancelAfter(TimeSpan.FromSeconds(15));
        using NamedPipeServerStream? pipe = OperatingSystem.IsWindows()
            ? new(name, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly)
            : null;
        using Socket? listener = OperatingSystem.IsLinux()
            ? new(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified)
            : null;
        if (listener is not null)
        {
            listener.Bind(new UnixDomainSocketEndPoint(endpoint));
            listener.Listen(1);
        }

        ProcessStartInfo start = new("dotnet")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        foreach (string argument in new[]
        {
            typeof(Program).Assembly.Location, "team", "add", "member.example", "--role", "Viewer",
            "--reason", "review", "--yes", "--local-endpoint", endpoint
        })
        {
            start.ArgumentList.Add(argument);
        }
        if (!generated)
        {
            start.ArgumentList.Add("--operation-id");
            start.ArgumentList.Add("provided-id");
        }
        if (verbose)
        {
            start.ArgumentList.Add("--verbose");
        }

        using Process process = Process.Start(start) ?? throw new InvalidOperationException("The CLI process could not be started.");
        Task<string>? received = null;
        try
        {
            Task<string> diagnostics = process.StandardError.ReadToEndAsync(stop.Token);
            string? generatedLine = null;
            if (generated)
            {
                generatedLine = await process.StandardOutput.ReadLineAsync(stop.Token);
                Assert.NotNull(generatedLine);
                Assert.Matches(@"Operation ID: [a-f0-9]{32}", generatedLine);
            }

            Task<string> output = process.StandardOutput.ReadToEndAsync(stop.Token);
            received = pipe is not null
                ? ReceiveTeamPipeCommandAsync(pipe, stop.Token)
                : ReceiveTeamSocketCommandAsync(listener!, stop.Token);
            using JsonDocument document = JsonDocument.Parse(await received.WaitAsync(stop.Token));
            await process.WaitForExitAsync(stop.Token);
            string? operation = document.RootElement.GetProperty("operationId").GetString();
            if (generated)
            {
                Assert.True(Guid.TryParseExact(operation, "N", out _));
                Assert.Contains($"Operation ID: {operation}", generatedLine!, StringComparison.Ordinal);
            }
            else
            {
                Assert.Equal("provided-id", operation);
            }
            Assert.DoesNotContain("Operation ID:", await diagnostics, StringComparison.Ordinal);
            Assert.Equal(0, process.ExitCode);
            Assert.Equal(verbose ? """{"succeeded":true}""" : "", (await output).TrimEnd('\r', '\n'));
        }
        finally
        {
            stop.Cancel();
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync(CancellationToken.None);
            }

            try
            {
                if (received is not null)
                {
                    await received;
                }
            }
            catch (OperationCanceledException) when (stop.IsCancellationRequested)
            {
            }

            listener?.Dispose();
            if (listener is not null)
            {
                File.Delete(endpoint);
            }
        }
    }

    [Theory]
    [InlineData("operation")]
    [InlineData("policy operation")]
    [InlineData("policy resume --reason review")]
    [InlineData("policy recover --version v1 --expected-revision 1 --create --labeler-handle labeler.example --reason review")]
    [InlineData("retire begin --reason review --confirmation proof")]
    [InlineData("retire complete --reason review --attestation evidence --confirmation proof")]
    [InlineData("retire purge --reason review --confirmation proof")]
    [InlineData("retire request-key-removal --reason review --inventory evidence --confirmation proof")]
    [InlineData("retire attest-key-removal --reason review --inventory evidence --attestation evidence --confirmation proof")]
    [InlineData("retire confirm-host-stopped --reason review --host-id 89c81bd7-ec21-45a6-b0d6-18fbe9981707 --attestation evidence --confirmation proof")]
    [InlineData("rotation confirm-host-stopped --reason review --host-id 89c81bd7-ec21-45a6-b0d6-18fbe9981707")]
    [InlineData("rotation capture-boundary --reason review")]
    [InlineData("rotation verify-publication --reason review")]
    [InlineData("rotation resign-batch --reason review")]
    [InlineData("rotation complete --reason review")]
    public void LookupsAndContinuationsRequireTheExistingOperationId(string command)
    {
        string[] words = command.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        RootCommand root = Program.CreateRootCommand();

        Assert.Contains(root.Parse(words).Errors, error => error.Message.Contains("--operation-id", StringComparison.Ordinal));
        Assert.Empty(root.Parse([.. words, "--operation-id", "existing"]).Errors);
    }

    [Theory]
    [InlineData("policy save --file policy.json --reason review")]
    [InlineData("policy publish --version v1 --expected-revision 1 --create --labeler-handle labeler.example --reason review")]
    [InlineData("retire advance --reason review")]
    [InlineData("retire grace --days 1 --reason review")]
    [InlineData("retire extend-grace --days 1 --reason review")]
    public void NewOperationsAcceptAnOmittedOperationId(string command)
    {
        Assert.Empty(Program.CreateRootCommand().Parse(command.Split(' ', StringSplitOptions.RemoveEmptyEntries)).Errors);
    }

    /// <summary>Accepts one local pipe connection and captures its framed administration command.</summary>
    /// <param name="pipe">The listening pipe.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The captured command JSON.</returns>
    private static async Task<string> ReceiveTeamPipeCommandAsync(NamedPipeServerStream pipe, CancellationToken token)
    {
        await pipe.WaitForConnectionAsync(token);
        return await ReplyToTeamCommandAsync(pipe, token);
    }

    /// <summary>Accepts one local socket connection and captures its framed administration command.</summary>
    /// <param name="listener">The listening socket.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The captured command JSON.</returns>
    private static async Task<string> ReceiveTeamSocketCommandAsync(Socket listener, CancellationToken token)
    {
        using Socket connection = await listener.AcceptAsync(token);
        using NetworkStream stream = new(connection, ownsSocket: false);
        return await ReplyToTeamCommandAsync(stream, token);
    }

    /// <summary>Returns a successful receipt after reading a bounded local administration frame.</summary>
    /// <param name="stream">The connected local transport stream.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>The captured command JSON.</returns>
    private static async Task<string> ReplyToTeamCommandAsync(Stream stream, CancellationToken token)
    {
        byte[] header = new byte[4];
        await stream.ReadExactlyAsync(header, token);
        int length = BinaryPrimitives.ReadInt32BigEndian(header);
        Assert.InRange(length, 1, 1024 * 1024);
        byte[] request = new byte[length];
        await stream.ReadExactlyAsync(request, token);

        byte[] response = Encoding.UTF8.GetBytes("""{"succeeded":true}""");
        BinaryPrimitives.WriteInt32BigEndian(header, response.Length);
        await stream.WriteAsync(header, token);
        await stream.WriteAsync(response, token);
        await stream.FlushAsync(token);
        return Encoding.UTF8.GetString(request);
    }

    [Theory]
    [InlineData("Unauthorized", 4)]
    [InlineData("InvalidRequest", 2)]
    [InlineData("DependencyUnavailable", 5)]
    [InlineData(null, 5)]
    public void ServiceErrorsMapToStableExitCodes(string? error, int expected)
    {
        Assert.Equal(expected, Program.MapServiceErrorToExitCode(error));
    }
}
