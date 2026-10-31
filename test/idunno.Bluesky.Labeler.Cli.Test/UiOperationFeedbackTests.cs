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
}
