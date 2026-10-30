// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.CommandLine;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace idunno.Bluesky.Labeler.Cli.Test;

public partial class RetirementCliTests
{
    [Theory]
    [InlineData("beginRetirement")]
    [InlineData("purgeRetiredData")]
    [InlineData("completeRetirement")]
    [InlineData("requestKeyRemoval")]
    [InlineData("attestKeyRemoval")]
    [InlineData("confirmRetirementHostStopped")]
    public void BlanketConfirmationNeverReplacesTheActionChallenge(string action)
    {
        Program.AdministrationCommand intent = Intent(action);
        Assert.True(Program.RequiresRetirementConfirmation(action));
        Assert.False(Program.HasValidRetirementIntent(intent with { Confirmed = true }, preview: false));
        Assert.False(Program.HasValidRetirementIntent(intent with { Confirmation = " " }, preview: false));
        Assert.True(Program.HasValidRetirementIntent(intent with { Confirmation = "server-action-proof" }, preview: false));
        Assert.True(Program.HasValidRetirementIntent(intent with { Method = "prepareRetirement", Target = action }, preview: true));
    }

    [Theory]
    [InlineData("beginGracePeriod", 0, false)]
    [InlineData("beginGracePeriod", 1, true)]
    [InlineData("beginGracePeriod", 365, true)]
    [InlineData("beginGracePeriod", 366, false)]
    [InlineData("extendGracePeriod", -1, false)]
    [InlineData("extendGracePeriod", 0, false)]
    [InlineData("extendGracePeriod", 1, true)]
    [InlineData("extendGracePeriod", 365, true)]
    [InlineData("extendGracePeriod", 366, false)]
    [InlineData("beginRetirement", 30, false)]
    public void GraceMustBeExplicitAndBounded(string action, int days, bool expected)
    {
        Program.AdministrationCommand intent = Intent(action) with { GracePeriodDays = days, Confirmation = "proof" };
        Assert.Equal(expected, Program.HasValidRetirementIntent(intent, preview: false));
        Assert.Equal(expected, Program.HasValidRetirementIntent(
            intent with { Method = "prepareRetirement", Target = action, Confirmation = null }, preview: true));
    }

    [Theory]
    [InlineData("completeRetirement", null, null, false)]
    [InlineData("completeRetirement", null, " ", false)]
    [InlineData("completeRetirement", null, "Reviewed distribution and outages.", true)]
    [InlineData("requestKeyRemoval", null, null, false)]
    [InlineData("requestKeyRemoval", " ", null, false)]
    [InlineData("requestKeyRemoval", "Hosts and external copies.", null, true)]
    [InlineData("attestKeyRemoval", "Hosts and external copies.", null, false)]
    [InlineData("attestKeyRemoval", null, "Removed listed copies.", false)]
    [InlineData("attestKeyRemoval", "Hosts and external copies.", "Partial removal; offline backup outstanding.", true)]
    [InlineData("beginRetirement", "Irrelevant inventory.", null, false)]
    [InlineData("resumeRetirement", null, "Irrelevant attestation.", false)]
    public void PreviewAndExecutionRequireTheSameActionSpecificEvidence(
        string action,
        string? inventory,
        string? attestation,
        bool expected)
    {
        Program.AdministrationCommand intent = Intent(action) with
        {
            Inventory = inventory,
            Attestation = attestation,
            Confirmation = "proof"
        };
        Assert.Equal(expected, Program.HasValidRetirementIntent(intent, preview: false));
        Assert.Equal(expected, Program.HasValidRetirementIntent(
            intent with { Method = "prepareRetirement", Target = action, Confirmation = null }, preview: true));
    }

    [Theory]
    [InlineData("grace")]
    [InlineData("extend-grace")]
    public void GraceCommandHasNoDefaultDuration(string name)
    {
        Command command = Program.CreateRetirementCommand();
        Assert.NotEmpty(command.Parse([name, "--operation-id", "grace", "--reason", "Reviewed outages."]).Errors);
        Assert.Empty(command.Parse([name, "--days", "1", "--operation-id", "grace", "--reason", "Reviewed outages."]).Errors);
        Command step = command.Subcommands.Single(value => value.Name == name);
        Option<int> days = Assert.IsType<Option<int>>(step.Options.Single(value => value.Name == "--days"));
        Assert.False(days.HasDefaultValue);
    }

    [Theory]
    [InlineData("status", "getRetirementState", true)]
    [InlineData("preflight", "prepareRetirement", false)]
    [InlineData("pause", "pauseRetirement", false)]
    [InlineData("resume", "resumeRetirement", false)]
    [InlineData("request-key-removal", "requestKeyRemoval", false)]
    [InlineData("attest-key-removal", "attestKeyRemoval", false)]
    [InlineData("confirm-host-stopped", "confirmRetirementHostStopped", false)]
    public void NewSurfacesUseTheSharedProtocol(string name, string method, bool get)
    {
        Assert.Contains(Program.CreateRetirementCommand().Subcommands, value => value.Name == name);
        Assert.Equal(get, Program.IsRemoteGetMethod(method));
    }

    [Fact]
    public async Task BareYesCannotExecuteAnIrreversibleAction()
    {
        Command command = Program.CreateRetirementCommand();
        int result = await command.Parse([
            "begin", "--operation-id", "begin", "--reason", "Close the service.", "--yes"
        ]).InvokeAsync(cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(2, result);
    }

    [Fact]
    public void PreflightRequiresTheExactTargetActionAndOperationIdentity()
    {
        Program.AdministrationCommand intent = Intent("beginRetirement") with { Method = "prepareRetirement", Target = "beginRetirement" };
        Assert.True(Program.HasValidRetirementIntent(intent, preview: true));
        Assert.False(Program.HasValidRetirementIntent(intent with { Target = "begin" }, preview: true));
        Assert.False(Program.HasValidRetirementIntent(intent with { OperationId = " " }, preview: true));
        Assert.False(Program.HasValidRetirementIntent(intent with { Reason = " " }, preview: true));
        Assert.NotEmpty(Program.CreateRetirementCommand().Parse([
            "preflight", "--operation-id", "preview", "--reason", "Close the service."
        ]).Errors);
    }

    [Fact]
    public void WireShapePreservesIntentAndUsesProofRatherThanTheLegacyBoolean()
    {
        Program.AdministrationCommand intent = Intent("attestKeyRemoval");
        Program.AdministrationCommand preview = intent with { Method = "prepareRetirement", Target = intent.Method };
        using JsonDocument document = JsonDocument.Parse(JsonSerializer.Serialize(preview, WireJsonContext.Default.AdministrationCommand));
        JsonElement json = document.RootElement;
        Assert.Equal("prepareRetirement", json.GetProperty("method").GetString());
        Assert.Equal("attestKeyRemoval", json.GetProperty("target").GetString());
        Assert.Equal(intent.OperationId, json.GetProperty("operationId").GetString());
        Assert.Equal(intent.Reason, json.GetProperty("reason").GetString());
        Assert.Equal(intent.Inventory, json.GetProperty("inventory").GetString());
        Assert.Equal(intent.Attestation, json.GetProperty("attestation").GetString());
        Assert.Equal(0, json.GetProperty("gracePeriodDays").GetInt32());
        Assert.False(json.GetProperty("confirmed").GetBoolean());
        Assert.False(json.TryGetProperty("confirmation", out _));
        string executed = JsonSerializer.Serialize(intent with { Confirmation = "action-specific" }, WireJsonContext.Default.AdministrationCommand);
        Program.AdministrationCommand restored = JsonSerializer.Deserialize(executed, WireJsonContext.Default.AdministrationCommand)!;
        Assert.Equal(intent with { Confirmation = "action-specific" }, restored);
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("00000000-0000-0000-0000-000000000000", false)]
    [InlineData("89c81bd7-ec21-45a6-b0d6-18fbe9981707", true)]
    public void StoppedHostRequiresExactRegistrationAndSeparateEvidence(string? host, bool valid)
    {
        Program.AdministrationCommand intent = Intent("confirmRetirementHostStopped") with
        {
            HostInstanceId = Guid.TryParse(host, out Guid parsed) ? parsed : null,
            Confirmation = "host-specific-proof"
        };
        Assert.Equal(valid, Program.HasValidRetirementIntent(intent, preview: false));
        Assert.Equal(valid, Program.HasValidRetirementIntent(
            intent with { Method = "prepareRetirement", Target = intent.Method, Confirmation = null }, preview: true));
        Assert.False(Program.HasValidRetirementIntent(intent with { Attestation = " " }, preview: false));
        Assert.False(Program.HasValidRetirementIntent(intent with { Confirmation = null, Confirmed = true }, preview: false));
        Assert.False(Program.HasValidRetirementIntent(
            intent with { Method = "beginRetirement", Attestation = null, HostInstanceId = Guid.Parse("89c81bd7-ec21-45a6-b0d6-18fbe9981707") },
            preview: false));
    }

    [Fact]
    public void StoppedHostWirePreservesTheExactGuidAndAttestation()
    {
        Program.AdministrationCommand intent = Intent("confirmRetirementHostStopped");
        using JsonDocument document = JsonDocument.Parse(JsonSerializer.Serialize(
            intent with { Method = "prepareRetirement", Target = intent.Method },
            WireJsonContext.Default.AdministrationCommand));
        Assert.Equal(intent.HostInstanceId, document.RootElement.GetProperty("hostInstanceId").GetGuid());
        Assert.Equal(intent.Attestation, document.RootElement.GetProperty("attestation").GetString());
        Assert.Equal("confirmRetirementHostStopped", document.RootElement.GetProperty("target").GetString());
        Assert.NotEmpty(Program.CreateRetirementCommand().Parse([
            "confirm-host-stopped", "--operation-id", "stopped", "--reason", "Verified shutdown."
        ]).Errors);
    }

    /// <summary>Creates complete immutable test intent without inventing grace defaults.</summary>
    private static Program.AdministrationCommand Intent(string action) => new(
        Method: action,
        OperationId: "stable-command-id",
        Reason: "Approved retirement action.",
        Attestation: action is "completeRetirement" or "attestKeyRemoval" or "confirmRetirementHostStopped" ? "Operator evidence." : null,
        Inventory: action is "requestKeyRemoval" or "attestKeyRemoval" ? "Hosts, public fingerprints, external copies." : null,
        HostInstanceId: action == "confirmRetirementHostStopped" ? Guid.Parse("89c81bd7-ec21-45a6-b0d6-18fbe9981707") : null);

    /// <summary>Checks the transport contract without enabling reflection-based serialization.</summary>
    [JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonSerializable(typeof(Program.AdministrationCommand))]
    private sealed partial class WireJsonContext : JsonSerializerContext;
}
