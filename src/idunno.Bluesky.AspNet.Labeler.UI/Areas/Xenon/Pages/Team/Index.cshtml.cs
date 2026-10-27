// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;

using idunno.Bluesky.Labeler.Administration;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace idunno.Bluesky.AspNet.Labeler.UI.Areas.Xenon.Pages.Team;

/// <summary>Displays members and submits audited enrollment, role and membership changes.</summary>
public sealed class IndexModel : PageModel
{
    /// <summary>Gets the current team response.</summary>
    internal JsonElement Data { get; private set; }

    /// <summary>Gets counted membership navigation and canonical identity ordering.</summary>
    internal TableNavigation? Table { get; private set; }

    /// <summary>Loads active members and retained membership tombstones.</summary>
    public async Task OnGetAsync()
    {
        Table = TableNavigation.Read(Request, new(LabelTableKind.Team, Sort: LabelTableSort.Subject, Ascending: true));
        Data = await UiPageHelpers.ReadTableAsync(this, Table).ConfigureAwait(false);
    }

    /// <summary>Enrolls an identity after the shared service verifies its DID document and handle, if supplied.</summary>
    /// <returns>An action result that displays the outcome of the enrollment.</returns>
    public async Task<IActionResult> OnPostEnrollAsync()
    {
        if (!await UiPageHelpers.ConfirmedAsync(this).ConfigureAwait(false))
        {
            ViewData["Error"] = "Confirm the team enrollment before submitting.";
            await OnGetAsync().ConfigureAwait(false);
            return Page();
        }

        string target = await UiPageHelpers.FormValueAsync(this, "target", 256).ConfigureAwait(false);
        string roleText = await UiPageHelpers.FormValueAsync(this, "role", 32).ConfigureAwait(false);
        string reason = await UiPageHelpers.FormValueAsync(this, "reason", 4096).ConfigureAwait(false);
        string operationId = await UiPageHelpers.FormValueAsync(this, "operationId", 256).ConfigureAwait(false);
        if (!Enum.TryParse(roleText, ignoreCase: false, out LabelTeamRole role) || !Enum.IsDefined(role))
        {
            ViewData["Error"] = "Choose a valid team role.";
            await OnGetAsync().ConfigureAwait(false);
            return Page();
        }

        AdministrationResponse response = await UiPageHelpers.DispatchAsync(
            this,
            new AdministrationCommand(
                Method: "enroll",
                OperationId: operationId,
                Reason: reason,
                Target: target,
                Role: role)).ConfigureAwait(false);
        if (response.Succeeded)
        {
            TempData["Status"] = $"Enrollment operation {operationId} completed.";
            return RedirectToPage();
        }

        await OnGetAsync().ConfigureAwait(false);
        return Page();
    }

    /// <summary>Applies a provider-backed membership change after explicit confirmation.</summary>
    /// <returns>An action result that displays the outcome of the membership change.</returns>
    public async Task<IActionResult> OnPostChangeAsync()
    {
        if (!await UiPageHelpers.ConfirmedAsync(this).ConfigureAwait(false))
        {
            ViewData["Error"] = "Confirm the membership change before submitting.";
            await OnGetAsync().ConfigureAwait(false);
            return Page();
        }

        string target = await UiPageHelpers.FormValueAsync(this, "target", 256).ConfigureAwait(false);
        string actionText = await UiPageHelpers.FormValueAsync(this, "action", 32).ConfigureAwait(false);
        string roleText = await UiPageHelpers.FormValueAsync(this, "role", 32).ConfigureAwait(false);
        string reason = await UiPageHelpers.FormValueAsync(this, "reason", 4096).ConfigureAwait(false);
        string operationId = await UiPageHelpers.FormValueAsync(this, "operationId", 256).ConfigureAwait(false);
        if (!Enum.TryParse(actionText, ignoreCase: false, out LabelTeamAction action) ||
            !Enum.IsDefined(action) || action is LabelTeamAction.Enroll or LabelTeamAction.BindLocal or LabelTeamAction.RevokeLocal ||
            !Enum.TryParse(roleText, ignoreCase: false, out LabelTeamRole role) || !Enum.IsDefined(role))
        {
            ViewData["Error"] = "Choose a valid membership action and role.";
            await OnGetAsync().ConfigureAwait(false);
            return Page();
        }

        AdministrationResponse response = await UiPageHelpers.DispatchAsync(
            this,
            new AdministrationCommand(
                Method: "changeTeam",
                OperationId: operationId,
                Reason: reason,
                Target: target,
                Role: role,
                Action: action)).ConfigureAwait(false);
        if (response.Succeeded)
        {
            TempData["Status"] = $"Membership operation {operationId} completed.";
            return RedirectToPage();
        }

        await OnGetAsync().ConfigureAwait(false);
        return Page();
    }
}
