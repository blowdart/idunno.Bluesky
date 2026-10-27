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

    /// <summary>Gets the label publication and retirement checkpoint.</summary>
    internal JsonElement Publication { get; private set; }

    /// <summary>Loads both durable state views independently of any browser session state.</summary>
    public async Task OnGetAsync()
    {
        AdministrationResponse maintenance = await UiPageHelpers.DispatchAsync(
            this,
            new AdministrationCommand(Method: "getMaintenanceState", Reason: "Xenon lifecycle inspection.")).ConfigureAwait(false);
        Maintenance = UiPageHelpers.ToElement(maintenance);
        AdministrationResponse publication = await UiPageHelpers.DispatchAsync(
            this,
            new AdministrationCommand(Method: "getPublicationState", Reason: "Xenon lifecycle inspection.")).ConfigureAwait(false);
        Publication = UiPageHelpers.ToElement(publication);
    }

    /// <summary>Submits one explicitly confirmed durable lifecycle transition.</summary>
    /// <returns>An action result that displays the outcome of the lifecycle step.</returns>
    public async Task<IActionResult> OnPostAsync()
    {
        if (!await UiPageHelpers.ConfirmedAsync(this).ConfigureAwait(false))
        {
            ViewData["Error"] = "Confirm the lifecycle operation before submitting.";
            await OnGetAsync().ConfigureAwait(false);
            return Page();
        }

        string method = await UiPageHelpers.FormValueAsync(this, "method", 32).ConfigureAwait(false);
        string reason = await UiPageHelpers.FormValueAsync(this, "reason", 4096).ConfigureAwait(false);
        string operationId = await UiPageHelpers.FormValueAsync(this, "operationId", 256).ConfigureAwait(false);
        int graceDays = 0;
        if (method == "beginGracePeriod" &&
            (!int.TryParse(await UiPageHelpers.FormValueAsync(this, "graceDays", 8).ConfigureAwait(false), out graceDays) ||
                graceDays is < 1 or > 365))
        {
            ViewData["Error"] = "Choose a grace period from 1 to 365 days.";
            await OnGetAsync().ConfigureAwait(false);
            return Page();
        }

        int limit = 0;
        bool purge = method == "purgeRetiredData";
        if (purge &&
            (!int.TryParse(await UiPageHelpers.FormValueAsync(this, "limit", 8).ConfigureAwait(false), out limit) ||
                limit is < 1 or > 1000))
        {
            ViewData["Error"] = "Choose a purge batch size from 1 to 1000.";
            await OnGetAsync().ConfigureAwait(false);
            return Page();
        }

        if (method is not ("beginRetirement" or "advanceRetirement" or "beginGracePeriod" or "completeRetirement" or "purgeRetiredData"))
        {
            ViewData["Error"] = "Choose a supported lifecycle operation and a purge batch size from 1 to 1000.";
            await OnGetAsync().ConfigureAwait(false);
            return Page();
        }

        AdministrationResponse response = await UiPageHelpers.DispatchAsync(
            this,
            new AdministrationCommand(
                Method: method,
                OperationId: operationId,
                Reason: reason,
                GracePeriodDays: graceDays,
                Limit: purge ? limit : 16,
                Confirmed: purge)).ConfigureAwait(false);
        if (response.Succeeded)
        {
            TempData["Status"] = $"Lifecycle operation {operationId} completed. Reload to inspect durable progress.";
            return RedirectToPage();
        }

        await OnGetAsync().ConfigureAwait(false);
        return Page();
    }
}
