// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;

using idunno.Bluesky.Labeler.Administration;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.DependencyInjection;

namespace idunno.Bluesky.AspNet.Labeler.UI.Areas.Xenon.Pages.Audit;

/// <summary>Displays a bounded page of provider-backed administrative audit events.</summary>
public sealed class IndexModel : PageModel
{
    /// <summary>Gets the exclusive sequence cursor.</summary>
    [BindProperty(SupportsGet = true)]
    public long AfterSequence { get; set; }

    /// <summary>Gets the audit response.</summary>
    internal JsonElement Data { get; private set; }

    /// <summary>Gets counted audit navigation with time ordering and a stable internal sequence tie-breaker.</summary>
    internal TableNavigation? Table { get; private set; }

    /// <summary>Loads the next bounded audit page.</summary>
    public async Task OnGetAsync()
    {
        Table = TableNavigation.Read(Request, new(LabelTableKind.Audit, Sort: LabelTableSort.Received));
        if (Table is null)
        {
            ViewData["Error"] = "Check the filters. The start time must not be after the end time. Local times must occur exactly once in the selected time zone; use UTC for times skipped or repeated when the clocks change.";
            return;
        }

        if (Table.Query.AuditActor is { } actor)
        {
            AdministrationResponse resolved = await HttpContext.RequestServices.GetRequiredService<LabelerUiAdministration>()
                .ResolveTableIdentityAsync(
                    principal: User,
                    actor: actor,
                    kind: LabelTableKind.Audit,
                    token: HttpContext.RequestAborted).ConfigureAwait(false);
            if (!resolved.Succeeded)
            {
                ViewData["Error"] = resolved.Message ?? "The account for that handle could not be found.";
                return;
            }

            Table = new(Table.Query with { AuditActor = resolved.Target }, Request);
        }

        Data = await UiPageHelpers.ReadTableAsync(this, Table).ConfigureAwait(false);
    }

    /// <summary>Exports all matching events with a fixed sequence cutoff and repeated live authorization.</summary>
    /// <returns>A streamed JSON download, or the audit page displaying invalid filter feedback.</returns>
    public async Task<IActionResult> OnPostDownloadAsync()
    {
        await OnGetAsync().ConfigureAwait(false);
        if (Table is null || ViewData["Error"] is not null)
        {
            return Page();
        }

        return new AuditDownloadResult(Table.Query);
    }
}
