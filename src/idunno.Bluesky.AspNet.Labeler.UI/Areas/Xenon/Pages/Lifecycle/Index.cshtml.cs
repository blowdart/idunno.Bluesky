// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace idunno.Bluesky.AspNet.Labeler.UI.Areas.Xenon.Pages.Lifecycle;

/// <summary>Inspects durable lifecycle state and submits explicit resumable retirement steps.</summary>
public sealed class IndexModel : PageModel
{
    /// <summary>Gets the durable maintenance state.</summary>
    internal JsonElement Maintenance { get; private set; }

    /// <summary>Gets the durable retirement progress or action-bound preflight projection.</summary>
    internal JsonElement Retirement { get; private set; }

    /// <summary>Gets the immutable intent displayed for a separate confirmation submission.</summary>
    internal AdministrationCommand? Intent { get; private set; }

    /// <summary>Gets lifecycle-specific feedback independently of other page-filter inspection failures.</summary>
    internal string? LifecycleError { get; private set; }

    /// <summary>Loads durable state views independently of any browser session state.</summary>
    public async Task OnGetAsync()
    {
        AdministrationResponse maintenance = await UiPageHelpers.DispatchAsync(
            this,
            new AdministrationCommand(Method: "getMaintenanceState", Reason: "Xenon lifecycle inspection.")).ConfigureAwait(false);
        Maintenance = UiPageHelpers.ToElement(maintenance);
        AdministrationResponse retirement = await UiPageHelpers.DispatchAsync(
            this,
            new AdministrationCommand(Method: "getRetirementState", Reason: "Xenon lifecycle inspection.")).ConfigureAwait(false);
        Retirement = UiPageHelpers.ToElement(retirement);
    }

    /// <summary>Submits one explicitly confirmed durable lifecycle transition.</summary>
    /// <returns>An action result that displays the outcome of the lifecycle step.</returns>
    public async Task<IActionResult> OnPostAsync()
    {
        string method = await UiPageHelpers.FormValueAsync(this, "method", 32).ConfigureAwait(false);
        string stage = await UiPageHelpers.FormValueAsync(this, "stage", 16).ConfigureAwait(false);
        string reason = await UiPageHelpers.FormValueAsync(this, "reason", 4096).ConfigureAwait(false);
        string operationId = await UiPageHelpers.FormValueAsync(this, "operationId", 256).ConfigureAwait(false);
        string confirmation = await UiPageHelpers.FormValueAsync(this, "confirmation", 128).ConfigureAwait(false);
        string confirmationText = await UiPageHelpers.FormValueAsync(this, "confirmationText", 32).ConfigureAwait(false);
        string inventory = await UiPageHelpers.FormValueAsync(this, "inventory", 16384).ConfigureAwait(false);
        string attestation = await UiPageHelpers.FormValueAsync(this, "attestation", 4096).ConfigureAwait(false);
        string hostText = await UiPageHelpers.FormValueAsync(this, "hostInstanceId", 36).ConfigureAwait(false);
        bool preview = stage == "preview";
        bool grace = method is "beginGracePeriod" or "extendGracePeriod";
        bool keys = method is "requestKeyRemoval" or "attestKeyRemoval";
        bool attests = method is "completeRetirement" or "attestKeyRemoval" or "confirmRetirementHostStopped";
        Guid? hostInstanceId = null;
        if (method == "confirmRetirementHostStopped")
        {
            if (!Guid.TryParse(hostText, out Guid host) || host == Guid.Empty)
            {
                LifecycleError = "Choose the exact host registration independently verified as stopped.";
                await OnGetAsync().ConfigureAwait(false);
                return Page();
            }

            hostInstanceId = host;
        }

        ViewData["OperationId"] = operationId;
        int graceDays = 0;
        if (grace &&
            (!int.TryParse(await UiPageHelpers.FormValueAsync(this, "graceDays", 8).ConfigureAwait(false), out graceDays) ||
                graceDays is < 1 or > 365))
        {
            LifecycleError = "Choose a grace period from 1 to 365 days.";
            await OnGetAsync().ConfigureAwait(false);
            return Page();
        }

        if (!IsRetirementAction(method) || stage is not ("preview" or "execute") ||
            string.IsNullOrWhiteSpace(operationId) || string.IsNullOrWhiteSpace(reason) ||
            keys && string.IsNullOrWhiteSpace(inventory) ||
            attests && string.IsNullOrWhiteSpace(attestation))
        {
            LifecycleError = "Choose a supported action, stable operation ID, reason and required inventory/attestation.";
            await OnGetAsync().ConfigureAwait(false);
            return Page();
        }

        if (!preview && (confirmationText != method ||
            RequiresChallenge(method) && string.IsNullOrWhiteSpace(confirmation)))
        {
            LifecycleError = "Preview this exact action first, then type its method name and submit its action-specific challenge. A generic checkbox is not sufficient.";
            await OnGetAsync().ConfigureAwait(false);
            return Page();
        }

        AdministrationCommand intent = new(
            Method: method,
            OperationId: operationId,
            Reason: reason,
            GracePeriodDays: graceDays,
            Confirmation: preview || confirmation.Length == 0 ? null : confirmation,
            Attestation: attests ? attestation : null,
            Inventory: keys ? inventory : null,
            HostInstanceId: hostInstanceId);
        AdministrationResponse response = await UiPageHelpers.DispatchAsync(
            this,
            preview ? intent with { Method = "prepareRetirement", Target = method } : intent).ConfigureAwait(false);
        if (preview && response.Succeeded)
        {
            await OnGetAsync().ConfigureAwait(false);
            Retirement = UiPageHelpers.ToElement(response);
            Intent = intent;
            ViewData["Status"] = "Read-only preflight prepared. No lifecycle action has been executed.";
            return Page();
        }

        if (response.Succeeded)
        {
            TempData["Status"] = $"Command {operationId} accepted. Background progress is durable; reload to inspect checkpoints.";
            return RedirectToPage();
        }

        await OnGetAsync().ConfigureAwait(false);
        LifecycleError = $"{response.Message ?? response.Error} If the preview expired or state/generation changed, retry preflight for the same immutable intent. For an uncertain outcome, retry the same operation ID; do not start a new operation.";
        return Page();
    }

    /// <summary>Identifies supported actions without accepting arbitrary dispatcher methods from a form.</summary>
    private static bool IsRetirementAction(string method) =>
        method is "beginRetirement" or "advanceRetirement" or "pauseRetirement" or "resumeRetirement" or
            "beginGracePeriod" or "extendGracePeriod" or "completeRetirement" or "purgeRetiredData" or
            "requestKeyRemoval" or "attestKeyRemoval" or "confirmRetirementHostStopped";

    /// <summary>Identifies irreversible actions that require their own expiring server proof.</summary>
    private static bool RequiresChallenge(string method) =>
        method is "beginRetirement" or "completeRetirement" or "purgeRetiredData" or "requestKeyRemoval" or "attestKeyRemoval" or "confirmRetirementHostStopped";
}
