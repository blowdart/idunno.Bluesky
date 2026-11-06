// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;

using idunno.Bluesky.Labeler.Administration;
using idunno.Bluesky.Labeler.Moderation;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace idunno.Bluesky.AspNet.Labeler.UI.Areas.Xenon.Pages.Reports;

/// <summary>Lists reports accepted by the independent moderation intake.</summary>
public sealed class IndexModel : PageModel
{
    /// <summary>Gets or sets the exclusive report identifier for the next page.</summary>
    [BindProperty(SupportsGet = true)]
    public long AfterId { get; set; }

    /// <summary>Gets or sets the report handling filter, initially showing unresolved reports.</summary>
    [BindProperty(SupportsGet = true)]
    public string? Status { get; set; }

    /// <summary>Gets or sets the report workflow queue, defaulting to unresolved work.</summary>
    [BindProperty(SupportsGet = true)]
    public LabelCaseWorkflow Queue { get; set; } = LabelCaseWorkflow.Unresolved;

    /// <summary>Gets the report response.</summary>
    internal JsonElement Data { get; private set; }

    /// <summary>Gets the counted report navigation and global ordering.</summary>
    internal TableNavigation? Table { get; private set; }

    /// <summary>Gets the independently paged retained-report table navigation.</summary>
    internal TableNavigation? PendingTable { get; private set; }

    /// <summary>Gets the confidential retained-report response.</summary>
    internal JsonElement PendingData { get; private set; }

    /// <summary>Gets the policy or lifecycle explanation relevant to report admission.</summary>
    internal string? PolicyNotice { get; private set; }

    /// <summary>Gets an explicit failure to read policy or lifecycle status.</summary>
    internal string? PolicyStatusError { get; private set; }

    /// <summary>Gets whether this retained-report page contains reports awaiting admission.</summary>
    internal bool HasWaitingReports { get; private set; }

    /// <summary>Gets whether this retained-report page contains out-of-scope reports.</summary>
    internal bool HasOutOfScopeReports { get; private set; }

    /// <summary>Gets whether this retained-report page contains submissions from muted reporters.</summary>
    internal bool HasMutedReporterReports { get; private set; }

    /// <summary>Gets whether the admission message should link to policy verification.</summary>
    internal bool PolicyVerificationNeedsRefresh { get; private set; }

    /// <summary>Loads a bounded page of accepted reports.</summary>
    public async Task OnGetAsync()
    {
        if (!Request.Query.ContainsKey(nameof(Status)))
        {
            Status = Request.Query.ContainsKey(nameof(Queue)) ? "All" : "Unresolved";
        }

        if (!Request.Query.ContainsKey(nameof(Queue)) && Request.Query.ContainsKey(nameof(Status)))
        {
            Queue = Status switch
            {
                "Handled" => LabelCaseWorkflow.Resolved,
                "All" => LabelCaseWorkflow.All,
                _ => LabelCaseWorkflow.Unresolved
            };
        }

        if (!ModelState.IsValid || AfterId < 0 || Status is not ("Unresolved" or "Handled" or "All") ||
            Queue is not (LabelCaseWorkflow.Unresolved or LabelCaseWorkflow.InReview or LabelCaseWorkflow.Escalated or LabelCaseWorkflow.Resolved or LabelCaseWorkflow.All))
        {
            ViewData["Error"] = "Choose an available report status and refresh the page before trying again.";
            return;
        }

        Table = TableNavigation.Read(Request, new(
            Kind: LabelTableKind.Reports,
            ReportHandled: Status switch { "Unresolved" => false, "Handled" => true, _ => null },
            Workflow: Queue));
        Data = await UiPageHelpers.ReadTableAsync(this, Table).ConfigureAwait(false);
        PendingTable = TableNavigation.Read(
            Request,
            new(Kind: LabelTableKind.PendingReports, Sort: LabelTableSort.Received, Ascending: true),
            parameterPrefix: "Pending");
        PendingData = await UiPageHelpers.ReadTableAsync(this, PendingTable).ConfigureAwait(false);
        if (PendingData.ValueKind == JsonValueKind.Object &&
            PendingData.TryGetProperty("moderation", out JsonElement moderation) &&
            moderation.TryGetProperty("pendingReports", out JsonElement pendingReports))
        {
            foreach (JsonElement report in pendingReports.EnumerateArray())
            {
                if (report.GetProperty("status").GetString() == "PendingVerification")
                {
                    HasWaitingReports = true;
                }
                else if (report.GetProperty("status").GetString() == "NotApplicable")
                {
                    HasOutOfScopeReports = true;
                }
                else if (report.GetProperty("status").GetString() == "ReporterMuted")
                {
                    HasMutedReporterReports = true;
                }
            }
        }

        AdministrationResponse policy = await UiPageHelpers.DispatchAsync(
            this,
            new AdministrationCommand(Method: "getPolicy", Reason: "Inspect report admission policy.")).ConfigureAwait(false);
        AdministrationResponse publication = await UiPageHelpers.DispatchAsync(
            this,
            new AdministrationCommand(Method: "getPublicationState", Reason: "Inspect report admission lifecycle.")).ConfigureAwait(false);
        if (!policy.Succeeded || !publication.Succeeded || publication.State is null)
        {
            PolicyStatusError = "We could not check the policy or retirement status, so we cannot tell whether new reports can enter case review. Refresh the page.";
            return;
        }

        ModerationPolicyView? state = policy.Moderation?.Policy;
        DateTimeOffset databaseTime = state?.DatabaseTime ?? default;
        long freshnessMilliseconds = state?.VerificationFreshnessMilliseconds ?? 0;
        PolicyVerificationNeedsRefresh = state is null ||
            !IsVerificationCurrent(state.Phase, state.VerifiedUntil, databaseTime, freshnessMilliseconds);
        PolicyNotice = DescribeAdmission(
            phase: state?.Phase ?? "Unverified",
            verifiedUntil: state?.VerifiedUntil,
            databaseTime: databaseTime,
            freshnessMilliseconds: freshnessMilliseconds,
            lifecycle: publication.State.Phase,
            hasWaitingReports: HasWaitingReports);
    }

    /// <summary>Determines whether an actively verified policy remains within its database-clock freshness window.</summary>
    internal static bool IsVerificationCurrent(
        string phase,
        DateTimeOffset? verifiedUntil,
        DateTimeOffset databaseTime,
        long freshnessMilliseconds) =>
        phase == "VerifiedActive" &&
        verifiedUntil is { } deadline &&
        deadline > databaseTime &&
        !PolicyVerificationClock.IsRollbackBeyondTolerance(deadline.AddMilliseconds(-freshnessMilliseconds), databaseTime);

    /// <summary>Explains the current admission gate without treating an empty retained queue as a verification failure.</summary>
    internal static string? DescribeAdmission(
        string phase,
        DateTimeOffset? verifiedUntil,
        DateTimeOffset databaseTime,
        long freshnessMilliseconds,
        string lifecycle,
        bool hasWaitingReports)
    {
        if (lifecycle != "Active")
        {
            return hasWaitingReports
                ? "These reports remain retained for staff reconciliation. Automatic case creation has stopped because the labeler is retiring or retired."
                : null;
        }

        return phase switch
        {
            "Unverified" => "New reports are held until an administrator verifies the active policy.",
            "Publishing" or "Withdrawing" => "New reports are held while the policy update completes.",
            "Conflict" => "New reports are held because the published declaration differs from the selected policy. An administrator must reconcile it.",
            "VerifiedActive" when !IsVerificationCurrent(phase, verifiedUntil, databaseTime, freshnessMilliseconds) =>
                "New reports are held because policy verification is no longer current.",
            "VerifiedActive" when hasWaitingReports =>
                "Previously held reports are being checked against the verified policy. Eligible reports will automatically become cases.",
            "VerifiedActive" => null,
            _ => "The policy state is not recognized. Report admission status is unknown; reload the page."
        };
    }
}
