// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

using idunno.AtProto;
using idunno.Bluesky.Labeler.Issuance;
using idunno.Bluesky.Labeler.Moderation;

using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.DependencyInjection;

namespace idunno.Bluesky.AspNet.Labeler.UI.Areas.Xenon.Pages.Cases;

/// <summary>Displays one case and submits revision-fenced case management or atomic label decisions.</summary>
public sealed class DetailModel : PageModel
{
    /// <summary>Gets the case identifier from the route.</summary>
    [BindProperty(SupportsGet = true)]
    public long Id { get; set; }

    /// <summary>Gets the individual report selected for review, independently of its age.</summary>
    [BindProperty(SupportsGet = true)]
    public long? ReportId { get; set; }

    /// <summary>Gets or sets the appeal selected from the appeals list or search results.</summary>
    [BindProperty(SupportsGet = true)]
    public long? AppealId { get; set; }

    /// <summary>Gets or sets the newest-first history page.</summary>
    [BindProperty(SupportsGet = true)]
    public long HistoryPage { get; set; } = 1;

    /// <summary>Gets the integrity-protected observed label state, policy and case revision.</summary>
    internal string? DecisionState { get; private set; }

    /// <summary>Gets configured labels and their exact-subject current state.</summary>
    internal ModerationLabelChoiceView[] LabelChoices { get; private set; } = [];

    /// <summary>Gets active labels on a reported record's author without making them part of the record decision.</summary>
    internal string[] AccountLabels { get; private set; } = [];

    /// <summary>Gets account label expiry metadata without making account labels part of a record decision.</summary>
    internal ModerationLabelChoiceView[] AccountLabelChoices { get; private set; } = [];

    /// <summary>Gets the validated duration for new labels, preserving immutable retry intent.</summary>
    internal LabelDurationDisplay Duration { get; private set; } = new();

    /// <summary>Gets protected case-management intent independently of label policy availability.</summary>
    internal string? EscalationState { get; private set; }

    /// <summary>Gets protected intent for handling the case's reports without changing labels or deciding appeals.</summary>
    internal string? AcknowledgeState { get; private set; }

    /// <summary>Gets a bounded page of reports linked to this case.</summary>
    internal ModerationReportView[] Reports { get; private set; } = [];

    /// <summary>Gets the individual report selected for this decision.</summary>
    internal ModerationReportView? CurrentReport { get; private set; }

    /// <summary>Gets pending appeals available for explicit review.</summary>
    internal ModerationAppealView[] Appeals { get; private set; } = [];

    /// <summary>Gets whether the selected report or appeal has already been handled.</summary>
    internal bool SelectedReviewCompleted { get; private set; }

    /// <summary>Gets the case detail response.</summary>
    internal JsonElement Data { get; private set; }

    /// <summary>Gets the case history response.</summary>
    internal ModerationActivityPageView? History { get; private set; }

    /// <summary>Gets whether active subject labels were successfully loaded.</summary>
    internal bool LabelsAvailable { get; private set; }

    /// <summary>Retains the exact submitted intent for recovery when storage cannot confirm the commit outcome.</summary>
    internal KeyValuePair<string, string>[]? RetryFields { get; private set; }

    /// <summary>Retains submitted selections after a rejected decision only while the observed case and labels remain unchanged.</summary>
    internal Microsoft.AspNetCore.Http.IFormCollection? SubmittedDecision { get; private set; }

    /// <summary>Gets a read-only public preview without replacing the case's exact subject or CID.</summary>
    internal SubjectPreview? Preview { get; private set; }

    /// <summary>Gets active moderator and administrator choices, labeled with verified handles.</summary>
    internal IReadOnlyList<AssigneeOption> Assignees { get; private set; } = [];

    /// <summary>Gets the currently selected immutable moderation policy version.</summary>
    internal string? PolicyVersion { get; private set; }

    /// <summary>Gets whether the selected policy declares more than one distinct label value.</summary>
    internal bool CanReplaceLabels { get; private set; }

    /// <summary>Gets appeals whose captured label outcomes are no longer active and current.</summary>
    internal HashSet<long> ChangedAppealIds { get; private set; } = [];

    /// <summary>Compares captured appeal sequences with the active exact-subject label observations.</summary>
    internal static bool HasChangedChallenges(ModerationAppealView appeal, ModerationLabelChoiceView[] choices) =>
        appeal.Challenges.Any(challenge => !choices.Any(choice =>
            choice.Value == challenge.Label.GetProperty("val").GetString() &&
            choice.Sequence == challenge.Sequence && choice.Applied));

    /// <summary>Determines replacement availability from declared policy values rather than retained applied labels.</summary>
    internal static bool HasReplacementLabels(JsonElement declaration) =>
        declaration.TryGetProperty("policies", out JsonElement policies) &&
        policies.TryGetProperty("labelValues", out JsonElement values) &&
        values.EnumerateArray().Select(value => value.GetString()).Distinct(StringComparer.Ordinal).Take(2).Count() > 1;

    /// <summary>Loads case detail and immutable workflow history.</summary>
    /// <returns>An action result that displays the case.</returns>
    public async Task<IActionResult> OnGetAsync()
    {
        if (!ModelState.IsValid || Id <= 0 || ReportId is <= 0 || AppealId is <= 0 || HistoryPage <= 0)
        {
            ViewData["Error"] = "Check the case number, report number and history page, then try again.";
            return Page();
        }
        AdministrationMemberView[] moderators = ViewData["AvailableModerators"] as AdministrationMemberView[] ?? [];
        IDidHandleResolver handles = HttpContext.RequestServices.GetRequiredService<IDidHandleResolver>();
        await Parallel.ForEachAsync(
            source: moderators,
            parallelOptions: new ParallelOptions { MaxDegreeOfParallelism = 8, CancellationToken = HttpContext.RequestAborted },
            body: async (moderator, token) => await handles.ResolveHandleAsync(new Did(moderator.Did), token).ConfigureAwait(false)).ConfigureAwait(false);
        Assignees = [.. moderators.Select(moderator =>
        {
            string label = handles.TryGetCachedHandle(new Did(moderator.Did), out Handle? handle) && handle != Handle.Invalid
                ? "@" + handle.Value
                : $"Handle unavailable ({moderator.Did})";
            return new AssigneeOption(moderator.Did, label);
        }).OrderBy(option => option.Label, StringComparer.OrdinalIgnoreCase)];

        AdministrationResponse response = await UiPageHelpers.DispatchAsync(
            this,
            new AdministrationCommand(
                Method: "getCase",
                Reason: "Xenon case inspection.",
                Moderation: new(CaseId: Id))).ConfigureAwait(false);
        Data = UiPageHelpers.ToElement(response);
        if (!response.Succeeded)
        {
            return Page();
        }
        if (response.Moderation?.Case is { } inspected)
        {
            EscalationState = EscalationProtector().Protect(JsonSerializer.Serialize(
                new CaseEscalationForm(Id, inspected.Revision, Guid.NewGuid().ToString("N")),
                CaseDecisionFormContext.Default.CaseEscalationForm));
        }

        Preview = await HttpContext.RequestServices.GetRequiredService<SubjectPreviewService>()
            .LoadAsync(Data.GetProperty("moderation").GetProperty("case").GetProperty("subject"), HttpContext.RequestAborted).ConfigureAwait(false);

        AdministrationResponse history = await UiPageHelpers.DispatchAsync(
            this,
            new AdministrationCommand(
                Method: "getCaseActivity",
                Reason: "Xenon case history inspection.",
                Moderation: new(CaseId: Id, HistoryPage: HistoryPage))).ConfigureAwait(false);
        History = history.Moderation?.Activity;
        AdministrationResponse labels = await UiPageHelpers.DispatchAsync(
            this,
            new AdministrationCommand(
                Method: "getCaseLabels",
                Reason: "Xenon case label selection.",
                Moderation: new(CaseId: Id))).ConfigureAwait(false);
        PolicyVersion = labels.Moderation?.Policy?.Definition?.Version;
        CanReplaceLabels = labels.Moderation?.Policy?.Definition is { } definition &&
            HasReplacementLabels(definition.Declaration);
        LabelChoices = labels.Moderation?.LabelChoices ?? [];
        AccountLabels = labels.Moderation?.AccountLabels ?? [];
        AccountLabelChoices = labels.Moderation?.AccountLabelChoices ?? [];
        LabelsAvailable = labels.Succeeded;
        AdministrationResponse reports = await UiPageHelpers.DispatchAsync(
            this,
            new AdministrationCommand(
                Method: "getReports",
                Reason: "Xenon linked reports.",
                Limit: 16,
                Moderation: new(CaseId: Id))).ConfigureAwait(false);
        Reports = reports.Moderation?.Reports ?? [];
        if (ReportId is { } reportId)
        {
            AdministrationResponse selected = await UiPageHelpers.DispatchAsync(
                this,
                new AdministrationCommand(
                    Method: "getReport",
                    Reason: "Xenon current report.",
                    Moderation: new(ReportId: reportId))).ConfigureAwait(false);
            CurrentReport = selected.Moderation?.Reports?.SingleOrDefault();
            if (CurrentReport?.CaseId != Id)
            {
                ViewData["Error"] = "The selected report does not belong to this case.";
                return Page();
            }
            SelectedReviewCompleted = CurrentReport.HandledAt is not null || CurrentReport.DecisionOperationId is not null;
        }

        AdministrationResponse appeals = await UiPageHelpers.DispatchAsync(
            this,
            new AdministrationCommand(
                Method: "getAppeals",
                Reason: "Xenon pending appeal selection.",
                Limit: 16,
                Moderation: new(CaseId: Id, AppealStatus: LabelAppealStatus.Pending))).ConfigureAwait(false);
        Appeals = appeals.Moderation?.Appeals ?? [];
        if (AppealId is { } selectedAppealId && !Appeals.Any(appeal => appeal.Id == selectedAppealId))
        {
            AdministrationResponse selected = await UiPageHelpers.DispatchAsync(
                this,
                new AdministrationCommand(
                    Method: "getAppeal",
                    Reason: "Xenon selected appeal.",
                    Moderation: new(AppealId: selectedAppealId))).ConfigureAwait(false);
            ModerationAppealView? selectedAppeal = selected.Moderation?.Appeals?.SingleOrDefault();
            if (!selected.Succeeded || selectedAppeal?.CaseId != Id)
            {
                ViewData["Error"] = "This appeal is unavailable or belongs to a different case.";
                return Page();
            }

            if (selectedAppeal.Status == nameof(LabelAppealStatus.Pending))
            {
                Appeals = [.. Appeals, selectedAppeal];
            }
            else
            {
                SelectedReviewCompleted = true;
            }
        }
        if (labels.Succeeded)
        {
            ChangedAppealIds = [.. Appeals.Where(appeal => HasChangedChallenges(appeal, LabelChoices)).Select(appeal => appeal.Id)];
        }
        if (labels.Succeeded && reports.Succeeded && appeals.Succeeded &&
            PolicyVersion is not null && response.Moderation?.Case is { } current &&
            labels.Moderation?.Case?.Revision == current.Revision &&
            (CurrentReport is null || CurrentReport.DecisionOperationId is null))
        {
            CaseDecisionForm snapshot = new(
                Id, current.Revision, PolicyVersion, Guid.NewGuid().ToString("N"), ReportId, LabelChoices);
            DecisionState = Protector().Protect(JsonSerializer.Serialize(snapshot, CaseDecisionFormContext.Default.CaseDecisionForm));
            AcknowledgeState = Protector().Protect(JsonSerializer.Serialize(
                snapshot with { OperationId = Guid.NewGuid().ToString("N"), ReportId = null, Choices = [], Acknowledge = true },
                CaseDecisionFormContext.Default.CaseDecisionForm));
        }
        return Page();
    }

    /// <summary>Flags the observed case for further review without changing labels, report handling or assignment.</summary>
    /// <returns>An action result displaying the escalation outcome.</returns>
    public async Task<IActionResult> OnPostEscalateAsync()
    {
        CaseEscalationForm? snapshot;
        try
        {
            string state = await UiPageHelpers.FormValueAsync(this, "escalationState", 262144).ConfigureAwait(false);
            snapshot = JsonSerializer.Deserialize(EscalationProtector().Unprotect(state), CaseDecisionFormContext.Default.CaseEscalationForm);
        }
        catch (Exception exception) when (exception is CryptographicException or JsonException)
        {
            ViewData["Error"] = "This escalation form is invalid or has expired. Refresh the case before submitting.";
            return await OnGetAsync().ConfigureAwait(false);
        }
        if (snapshot is null || snapshot.CaseId != Id)
        {
            ViewData["Error"] = "Refresh this case before escalating it.";
            return await OnGetAsync().ConfigureAwait(false);
        }
        JsonElement? subject = await ReadSubjectAsync().ConfigureAwait(false);
        if (subject is null)
        {
            ViewData["Error"] = "The case subject could not be loaded. Reload the case.";
            return await OnGetAsync().ConfigureAwait(false);
        }
        AdministrationResponse response = await UiPageHelpers.DispatchAsync(
            this,
            new AdministrationCommand(
                Method: "changeCase",
                OperationId: snapshot.OperationId,
                Reason: await ReadOptionalReasonAsync().ConfigureAwait(false),
                Moderation: new(
                    CaseId: Id,
                    ExpectedRevision: snapshot.Revision,
                    Action: LabelCaseAction.Escalate,
                    Subject: subject))).ConfigureAwait(false);
        if (response.Succeeded)
        {
            TempData["Status"] = "Case escalated for further moderator review. Reports and labels are unchanged.";
            return RedirectToPage(new { id = Id });
        }
        return await OnGetAsync().ConfigureAwait(false);
    }

    /// <summary>Changes an assignment after explicit confirmation.</summary>
    /// <returns>An action result that displays the outcome of the assignment change.</returns>
    public async Task<IActionResult> OnPostAssignAsync()
    {
        if (!await UiPageHelpers.ConfirmedAsync(this).ConfigureAwait(false))
        {
            ViewData["Error"] = "Select the confirmation checkbox before changing the assignment.";
            return await OnGetAsync().ConfigureAwait(false);
        }

        string assignee = await UiPageHelpers.FormValueAsync(this, "assignee", 256).ConfigureAwait(false);
        if (assignee.Length > 0 &&
            !(ViewData["AvailableModerators"] is AdministrationMemberView[] moderators && moderators.Any(candidate => candidate.Did == assignee)))
        {
            ViewData["Error"] = "Choose an active moderator or administrator. Refresh the page if the team has changed.";
            return await OnGetAsync().ConfigureAwait(false);
        }
        if (!long.TryParse(await UiPageHelpers.FormValueAsync(this, "revision", 24).ConfigureAwait(false), out long revision) ||
            !long.TryParse(await UiPageHelpers.FormValueAsync(this, "caseId", 24).ConfigureAwait(false), out long caseId))
        {
            ViewData["Error"] = "The case information submitted is invalid. Refresh the case and review it before trying again.";
            return await OnGetAsync().ConfigureAwait(false);
        }

        JsonElement? subject = await ReadSubjectAsync().ConfigureAwait(false);
        if (subject is null)
        {
            ViewData["Error"] = "The case subject could not be loaded. Reload the case.";
            return await OnGetAsync().ConfigureAwait(false);
        }

        string reason = await ReadOptionalReasonAsync().ConfigureAwait(false);
        string operationId = await UiPageHelpers.FormValueAsync(this, "operationId", 256).ConfigureAwait(false);
        AdministrationResponse response = await UiPageHelpers.DispatchAsync(
            this,
            new AdministrationCommand(
                Method: "changeCase",
                OperationId: operationId,
                Reason: reason,
                Moderation: new(
                    CaseId: caseId,
                    ExpectedRevision: revision,
                    Action: LabelCaseAction.Assign,
                    Subject: subject,
                    Assignee: string.IsNullOrEmpty(assignee) ? null : assignee))).ConfigureAwait(false);
        if (response.Succeeded)
        {
            TempData["Status"] = $"Assignment operation {operationId} completed.";
            return RedirectToPage(new { id = caseId });
        }

        return await OnGetAsync().ConfigureAwait(false);
    }

    /// <summary>Commits one atomic case decision using the observed revision and current policy version.</summary>
    /// <returns>An action result that displays the outcome of the case decision.</returns>
    public async Task<IActionResult> OnPostDecideAsync()
    {
        CaseDecisionForm snapshot;
        string protectedState = await UiPageHelpers.FormValueAsync(this, "decisionState", 262144).ConfigureAwait(false);
        try
        {
            CaseDecisionForm? decoded = JsonSerializer.Deserialize(
                Protector().Unprotect(protectedState), CaseDecisionFormContext.Default.CaseDecisionForm);
            if (decoded is null)
            {
                ViewData["Error"] = "The information needed to submit this decision is missing. Refresh the case and review your choices again.";
                return await OnGetAsync().ConfigureAwait(false);
            }

            snapshot = decoded;
        }
        catch (Exception exception) when (exception is CryptographicException or JsonException)
        {
            ViewData["Error"] = "This decision form is invalid or has expired. Refresh the case and review your choices again.";
            return await OnGetAsync().ConfigureAwait(false);
        }

        if (snapshot.CaseId != Id)
        {
            ViewData["Error"] = "This decision form belongs to a different case. Refresh this case before submitting.";
            return await OnGetAsync().ConfigureAwait(false);
        }

        ReportId = snapshot.ReportId;
        Microsoft.AspNetCore.Http.IFormCollection form = await Request.ReadFormAsync(HttpContext.RequestAborted).ConfigureAwait(false);
        int hours = snapshot.DurationHours;
        if (!snapshot.DurationFrozen && !LabelDuration.TryRead(form, out hours))
        {
            ViewData["Error"] = "Choose an available new label duration.";
            return await ReloadDecisionAsync(snapshot, form).ConfigureAwait(false);
        }
        Duration = new(hours, snapshot.DurationFrozen);
        if (snapshot.Acknowledge && (hours != 0 || form["reportHandling"] != "same-target" ||
            form["selectedLabels"].Count != 0 || form["appealId"].ToString().Length != 0 ||
            form["appealOutcome"].ToString().Length != 0))
        {
            ViewData["Error"] = "Acknowledgment only reviews this case's reports. It cannot change labels, content versions or appeal outcomes.";
            return await OnGetAsync().ConfigureAwait(false);
        }
        DateTimeOffset? expiresAt = snapshot.ExpiresAt;
        if (!snapshot.DurationFrozen && hours != 0)
        {
            expiresAt = HttpContext.RequestServices.GetRequiredService<TimeProvider>().GetUtcNow().AddHours(hours);
        }
        string reportHandling = form["reportHandling"].ToString();
        if (reportHandling is not ("selected" or "same-target" or "all-records"))
        {
            ViewData["Error"] = "Choose which reports to mark as reviewed.";
            return await ReloadDecisionAsync(snapshot, form).ConfigureAwait(false);
        }

        HashSet<string> selectedLabels = form["selectedLabels"].Select(value => value ?? string.Empty).ToHashSet(StringComparer.Ordinal);
        if (selectedLabels.Any(value => !snapshot.Choices.Any(choice => choice.Value == value && !choice.OtherVersion)))
        {
            ViewData["Error"] = "Select only the displayed labels for this record version.";
            return await OnGetAsync().ConfigureAwait(false);
        }

        ModerationLabelActionInput[] actions = [.. snapshot.Choices
            .Where(choice => !choice.OtherVersion && choice.Applied != selectedLabels.Contains(choice.Value))
            .Select(choice => new ModerationLabelActionInput(
                Kind: selectedLabels.Contains(choice.Value) ? LabelOperationKind.Issue : LabelOperationKind.Negate,
                Value: choice.Value,
                ExpiresAt: selectedLabels.Contains(choice.Value) ? expiresAt : null,
                ExpectedLabelSequence: choice.Sequence))];
        if (actions.Length > 16)
        {
            ViewData["Error"] = "Change at most sixteen labels in one submission.";
            return await OnGetAsync().ConfigureAwait(false);
        }

        string reason = await ReadOptionalReasonAsync().ConfigureAwait(false);
        string appealIdText = await UiPageHelpers.FormValueAsync(this, "appealId", 24).ConfigureAwait(false);
        string appealOutcomeText = await UiPageHelpers.FormValueAsync(this, "appealOutcome", 32).ConfigureAwait(false);

        long[]? appealIds = null;
        LabelAppealStatus? appealOutcome = null;
        if (appealIdText.Length > 0)
        {
            if (!long.TryParse(appealIdText, out long appealId) ||
                !Enum.TryParse(appealOutcomeText, ignoreCase: false, out LabelAppealStatus outcome) ||
                !Enum.IsDefined(outcome) || outcome == LabelAppealStatus.Pending)
            {
                ViewData["Error"] = "Choose an appeal and how to resolve it.";
                return await ReloadDecisionAsync(snapshot, form).ConfigureAwait(false);
            }

            appealIds = [appealId];
            appealOutcome = outcome;
        }

        AdministrationResponse response = await UiPageHelpers.DispatchAsync(
            this,
            new AdministrationCommand(
                Method: "decideCase",
                OperationId: snapshot.OperationId,
                Reason: reason,
                Moderation: new(
                    CaseId: Id,
                    ExpectedRevision: snapshot.Revision,
                    PolicyVersion: snapshot.PolicyVersion,
                    Actions: actions,
                    AppealIds: appealIds,
                    AppealOutcome: appealOutcome,
                    ReportId: snapshot.ReportId ?? 0,
                    HandleOtherReports: reportHandling == "same-target",
                    HandleReportsAcrossRecordVersions: reportHandling == "all-records"))).ConfigureAwait(false);
        if (response.Succeeded)
        {
            TempData["Status"] = (snapshot.Acknowledge, response.Moderation?.Decision?.Event.Case.Status) switch
            {
                (true, nameof(LabelCaseStatus.Resolved)) => "Case acknowledged and closed. Labels were not changed.",
                (true, _) => "Reports acknowledged without changing labels. The case remains in review for outstanding work.",
                _ => "Moderation decision saved."
            };
            return RedirectToPage(new { id = Id });
        }

        IActionResult page = await ReloadDecisionAsync(snapshot, form).ConfigureAwait(false);
        if (response.Error == "OutcomeUnknown")
        {
            CaseDecisionForm frozen = snapshot with { DurationFrozen = true, DurationHours = hours, ExpiresAt = expiresAt };
            DecisionState = Protector().Protect(JsonSerializer.Serialize(frozen, CaseDecisionFormContext.Default.CaseDecisionForm));
            RetryFields = [.. form.Where(field => field.Key is "reason" or "selectedLabels" or "reportHandling" or "appealId" or "appealOutcome").SelectMany(field => field.Value.Select(value => new KeyValuePair<string, string>(field.Key, value ?? string.Empty)))];
            Duration = new(hours, true);
            ViewData["Error"] = "We could not confirm whether the decision completed. Retry without changing your choices so the service can recover the original result. Do not submit a new decision.";
        }

        return page;
    }

    /// <summary>Records an explicit absence of explanation without weakening shared audit reason requirements.</summary>
    private async Task<string> ReadOptionalReasonAsync()
    {
        Microsoft.AspNetCore.Http.IFormCollection form = await Request.ReadFormAsync(HttpContext.RequestAborted).ConfigureAwait(false);
        string reason = form["reason"].ToString().Trim();
        return reason.Length == 0 ? "No reason provided." : reason;
    }

    /// <summary>Preserves rejected form choices without rebasing their intent onto a newer case, policy or label baseline.</summary>
    private async Task<IActionResult> ReloadDecisionAsync(
        CaseDecisionForm snapshot,
        Microsoft.AspNetCore.Http.IFormCollection form)
    {
        IActionResult page = await OnGetAsync().ConfigureAwait(false);
        if (Data.ValueKind == JsonValueKind.Object &&
            Data.TryGetProperty("moderation", out JsonElement currentModeration) &&
            currentModeration.ValueKind == JsonValueKind.Object &&
            currentModeration.TryGetProperty("case", out JsonElement currentCase) &&
            currentCase.ValueKind == JsonValueKind.Object &&
            currentCase.GetProperty("revision").GetInt64() == snapshot.Revision &&
            LabelChoices.SequenceEqual(snapshot.Choices) && PolicyVersion == snapshot.PolicyVersion)
        {
            SubmittedDecision = form;
            if (LabelDuration.TryRead(form, out int hours))
            {
                Duration = new(hours, snapshot.DurationFrozen);
            }
        }

        return page;
    }

    /// <summary>Protects the observed form baseline against forged labels, revisions and report substitutions.</summary>
    private IDataProtector Protector() =>
        HttpContext.RequestServices.GetRequiredService<IDataProtectionProvider>().CreateProtector("Xenon.CaseDecision.v1");

    /// <summary>Separates escalation intent from label decisions and other protected forms.</summary>
    private IDataProtector EscalationProtector() =>
        HttpContext.RequestServices.GetRequiredService<IDataProtectionProvider>().CreateProtector("Xenon.CaseEscalation.v1");

    private async Task<JsonElement?> ReadSubjectAsync()
    {
        AdministrationResponse response = await UiPageHelpers.DispatchAsync(
            this,
            new AdministrationCommand(
                Method: "getCase",
                Reason: "Xenon case revision check.",
                Moderation: new(CaseId: Id))).ConfigureAwait(false);
        return response.Succeeded && response.Moderation?.Case is { } caseView
            ? JsonSerializer.SerializeToElement(
                caseView.Subject,
                AdministrationJsonContext.Default.ReportSubjectView)
            : null;
    }
}

/// <summary>Retains the exact label selections observed when the moderator opened the form.</summary>
internal sealed record CaseDecisionForm(
    long CaseId,
    long Revision,
    string PolicyVersion,
    string OperationId,
    long? ReportId,
    ModerationLabelChoiceView[] Choices,
    int DurationHours = 0,
    bool DurationFrozen = false,
    DateTimeOffset? ExpiresAt = null,
    bool Acknowledge = false);

/// <summary>Provides reflection-free metadata for the integrity-protected form baseline.</summary>
[JsonSerializable(typeof(CaseDecisionForm))]
[JsonSerializable(typeof(CaseEscalationForm))]
internal sealed partial class CaseDecisionFormContext : JsonSerializerContext;

/// <summary>Retains the case revision and stable retry identity for a non-label escalation.</summary>
internal sealed record CaseEscalationForm(long CaseId, long Revision, string OperationId);
