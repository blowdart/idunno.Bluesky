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
    [InlineData("VerifiedActive", 10, "The public policy check is up to date.", true)]
    [InlineData("VerifiedActive", 0, "The public policy check has expired. Check it again to allow new reports and labels.", false)]
    [InlineData("VerifiedActive", 31, "The public policy needs to be checked again.", false)]
    [InlineData("VerifiedActive", null, "The public policy needs to be checked again.", false)]
    [InlineData("Unverified", 10, "No policy has passed the public policy check yet.", false)]
    [InlineData("Publishing", 10, "Publication is pending. Check the result or open Publication for the next step.", false)]
    [InlineData("Withdrawing", 10, "Wait for label withdrawals to finish before checking the policy.", false)]
    [InlineData("Conflict", 10, "Publication needs attention. Open Publication to recover.", false)]
    [InlineData("Unexpected", null, "Policy status is unknown. Reload before continuing.", false)]
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
