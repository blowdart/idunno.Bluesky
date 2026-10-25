// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.CommandLine;
using System.CommandLine.Parsing;
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

    [Theory]
    [InlineData("https://labeler.example", true)]
    [InlineData("http://127.0.0.1:8080/health/labeler/ready", true)]
    [InlineData("not-a-uri", false)]
    [InlineData("/relative/path", false)]
    [InlineData("file:///C:/labeler", false)]
    public void UriOptionsUseTheCommandLineParser(string value, bool valid)
    {
        Option<Uri?> option = Program.CreateUriOption("--endpoint", "The endpoint.", required: true);
        RootCommand command = new();
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
        Option<Uri?> option = Program.CreateUriOption("--admin-url", "The endpoint.", required);
        RootCommand command = new();
        command.Options.Add(option);
        ParseResult parsed = command.Parse([]);
        Assert.Equal(required, parsed.Errors.Count > 0);
        if (!required)
        {
            Assert.Null(parsed.GetValue(option));
        }

        Assert.NotEmpty(command.Parse(["--admin-url"]).Errors);
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
    [InlineData("ghcr.io/blowdart/idunno-bluesky-labeler@sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", true)]
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
    public void TeamChangeOptionsMatchTheSelectedAction(
        LabelTeamAction action,
        bool hasRole,
        bool hasCredential,
        bool expected)
    {
        Assert.Equal(expected, Program.HasValidTeamChangeOptions(action, hasRole, hasCredential));
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
