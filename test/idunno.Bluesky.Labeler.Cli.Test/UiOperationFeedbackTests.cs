// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.Bluesky.AspNet.Labeler.UI;

namespace idunno.Bluesky.Labeler.Cli.Test;

/// <summary>Prevents read-only state inspection from being presented as a completed mutation.</summary>
public class UiOperationFeedbackTests
{
    [Theory]
    [InlineData("getRetirementState", "", false)]
    [InlineData("getRetirementState", "saved-workflow-id", false)]
    [InlineData("getOperation", "command-id", false)]
    [InlineData("getPolicy", "command-id", false)]
    [InlineData("searchModeration", "command-id", false)]
    [InlineData("beginRetirement", "", false)]
    [InlineData("beginRetirement", "command-id", true)]
    [InlineData("changeTeam", "command-id", true)]
    [InlineData("savePolicy", "command-id", true)]
    public void OnlyMutationCommandsWithAnOperationIdProduceCompletionFeedback(string method, string operationId, bool expected) =>
        Assert.Equal(expected, UiPageHelpers.ShouldShowOperationCompletion(method, operationId));

    [Theory]
    [InlineData("NotStarted", "Not Started")]
    [InlineData("Active", "Active")]
    [InlineData("AwaitingHostAcknowledgement", "Awaiting Host Acknowledgement")]
    [InlineData("HTTPStatus", "HTTP Status")]
    public void EnumLabelsSeparatePascalCaseWords(string value, string expected) =>
        Assert.Equal(expected, UiPageHelpers.FormatEnumLabel(value));

    [Theory]
    [InlineData("tools.ozone.report.defs#reasonMisleadingSpam", "Spam")]
    [InlineData("com.atproto.moderation.defs#reasonSpam", "Spam (legacy)")]
    [InlineData("example.custom#reason", "example.custom#reason")]
    [InlineData(null, "")]
    public void ReportReasonNamesPreserveUnknownIdentifiers(string? value, string expected) =>
        Assert.Equal(expected, ReportReasonOptions.Format(value));

    [Theory]
    [InlineData("audit-export-started", "Audit download started")]
    [InlineData("policy-observation", "Public policy checked")]
    [InlineData("authenticate-browser", "Browser sign-in")]
    [InlineData("deleteDraft", "Draft deletion")]
    [InlineData("team-read", "Team viewed")]
    [InlineData("policy-draft", "Policy draft")]
    [InlineData("remote-administration-enabled", "Remote administration enabled")]
    [InlineData("maintenance-AwaitingHostAcknowledgement", "Maintenance Awaiting Host Acknowledgement")]
    [InlineData("custom-action", "Custom action")]
    [InlineData("beginRetirement", "Begin Retirement")]
    [InlineData("HTTPStatus", "HTTP Status")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void AuditActionNamesRetainReadableUnknownActions(string? value, string expected) =>
        Assert.Equal(expected, UiPageHelpers.FormatAuditAction(value));

    [Theory]
    [InlineData("beginRetirement", "permanent retirement")]
    [InlineData("purgeRetiredData", "deleting retained label history")]
    [InlineData("confirmRetirementHostStopped", "recording that this server process has stopped")]
    [InlineData("attestKeyRemoval", "recording signing-key removal")]
    public void RetirementConfirmationTitlesDescribeActions(string value, string expected) =>
        Assert.Equal(expected, UiPageHelpers.FormatRetirementAction(value));
}
