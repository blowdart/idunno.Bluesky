// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using PolicyPage = idunno.Bluesky.AspNet.Labeler.UI.Areas.Xenon.Pages.Policy.IndexModel;

namespace idunno.Bluesky.Labeler.Cli.Test;

/// <summary>Checks policy verification status and whether a fresh policy needs manual verification.</summary>
public class PolicyVerificationStatusTests
{
    [Theory]
    [InlineData(0, false, "No drafts to publish")]
    [InlineData(1, true, "Begin publication")]
    [InlineData(5, true, "Begin publication")]
    public void PublicationButtonExplainsItsAvailability(int draftCount, bool expectedEnabled, string expectedLabel)
    {
        Assert.Equal(expectedEnabled, PolicyPage.CanBeginPublication(draftCount));
        Assert.Equal(expectedLabel, PolicyPage.PublicationButtonText(draftCount));
    }

    [Theory]
    [InlineData("VerifiedActive", 10, "The current policy is already verified.", true)]
    [InlineData("VerifiedActive", 0, "Policy verification has expired.", false)]
    [InlineData("VerifiedActive", 31, "The current policy verification is no longer current.", false)]
    [InlineData("VerifiedActive", null, "The current policy verification is no longer current.", false)]
    [InlineData("Unverified", 10, "The current policy is Unverified.", false)]
    [InlineData("Publishing", 10, "The current policy is Publishing.", false)]
    [InlineData("Withdrawing", 10, "The current policy is Withdrawing.", false)]
    [InlineData("Conflict", 10, "The current policy is Conflict.", false)]
    public void VerificationTabDescribesStateAndOnlyDisablesCurrentVerification(
        string phase,
        int? remainingMinutes,
        string expectedStatus,
        bool expectedCurrent)
    {
        DateTimeOffset now = new(2030, 1, 1, 0, 0, 0, TimeSpan.Zero);
        DateTimeOffset? verifiedUntil = remainingMinutes is int minutes ? now.AddMinutes(minutes) : null;

        Assert.Equal(expectedStatus, PolicyPage.DescribeVerificationStatus(
            phase,
            verifiedUntil,
            now,
            30 * 60 * 1000));
        Assert.Equal(expectedCurrent, PolicyPage.IsVerificationCurrent(
            phase,
            verifiedUntil,
            now,
            30 * 60 * 1000));
    }
}
