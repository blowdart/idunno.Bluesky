// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using ReportPage = idunno.Bluesky.AspNet.Labeler.UI.Areas.Xenon.Pages.Reports.IndexModel;

namespace idunno.Bluesky.Labeler.Cli.Test;

/// <summary>Checks report explanations against policy freshness and lifecycle, independently of wall-clock time.</summary>
public class ReportAdmissionNoticeTests
{
    [Theory]
    [InlineData("VerifiedActive", 10, true)]
    [InlineData("VerifiedActive", 0, false)]
    [InlineData("VerifiedActive", 31, false)]
    [InlineData("Unverified", 10, false)]
    [InlineData("Conflict", 10, false)]
    public void VerificationLinkTracksPolicyFreshness(string phase, int remainingMinutes, bool expected)
    {
        DateTimeOffset now = new(2030, 1, 1, 0, 0, 0, TimeSpan.Zero);
        Assert.Equal(expected, ReportPage.IsVerificationCurrent(
            phase,
            now.AddMinutes(remainingMinutes),
            now,
            30 * 60 * 1000));
    }

    [Theory]
    [InlineData("Unverified", "Active", false, 10, "until an administrator verifies")]
    [InlineData("Publishing", "Active", true, 10, "while the policy update completes")]
    [InlineData("Withdrawing", "Active", true, 10, "while the policy update completes")]
    [InlineData("Conflict", "Active", true, 10, "published declaration differs")]
    [InlineData("VerifiedActive", "Active", true, 0, "no longer current")]
    [InlineData("VerifiedActive", "Active", true, -1, "no longer current")]
    [InlineData("VerifiedActive", "Active", true, 31, "no longer current")]
    [InlineData("VerifiedActive", "Active", true, null, "no longer current")]
    [InlineData("VerifiedActive", "Active", true, 10, "Eligible reports will automatically become cases")]
    [InlineData("VerifiedActive", "Active", false, 10, null)]
    [InlineData("VerifiedActive", "Retiring", true, 10, "staff reconciliation")]
    [InlineData("Unverified", "Retired", true, 10, "staff reconciliation")]
    [InlineData("VerifiedActive", "Purged", false, 10, null)]
    [InlineData("Unexpected", "Active", true, 10, "status is unknown")]
    public void NoticeMatchesTheAdmissionGate(string phase, string lifecycle, bool waiting, int? remainingMinutes, string? expected)
    {
        DateTimeOffset now = new(2030, 1, 1, 0, 0, 0, TimeSpan.Zero);
        string? notice = ReportPage.DescribeAdmission(
            phase: phase,
            verifiedUntil: remainingMinutes is int minutes ? now.AddMinutes(minutes) : null,
            databaseTime: now,
            freshnessMilliseconds: 30 * 60 * 1000,
            lifecycle: lifecycle,
            hasWaitingReports: waiting);

        if (expected is null)
        {
            Assert.Null(notice);
        }
        else
        {
            Assert.Contains(expected, Assert.IsType<string>(notice), StringComparison.Ordinal);
        }
    }
}
