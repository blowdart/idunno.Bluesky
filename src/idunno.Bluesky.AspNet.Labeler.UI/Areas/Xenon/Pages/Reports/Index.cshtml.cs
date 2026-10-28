// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;

using idunno.Bluesky.Labeler.Administration;

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

    /// <summary>Gets the report response.</summary>
    internal JsonElement Data { get; private set; }

    /// <summary>Gets the counted report navigation and global ordering.</summary>
    internal TableNavigation? Table { get; private set; }

    /// <summary>Gets the independently paged pending-validation table navigation.</summary>
    internal TableNavigation? PendingTable { get; private set; }

    /// <summary>Gets the confidential pending-validation report response.</summary>
    internal JsonElement PendingData { get; private set; }

    /// <summary>Loads a bounded page of accepted reports.</summary>
    public async Task OnGetAsync()
    {
        if (!Request.Query.ContainsKey(nameof(Status)))
        {
            Status = "Unresolved";
        }

        if (!ModelState.IsValid || AfterId < 0 || Status is not ("Unresolved" or "Handled" or "All"))
        {
            ViewData["Error"] = "Choose a valid report status and non-negative continuation identifier.";
            return;
        }

        Table = TableNavigation.Read(Request, new(
            Kind: LabelTableKind.Reports,
            ReportHandled: Status switch { "Unresolved" => false, "Handled" => true, _ => null }));
        Data = await UiPageHelpers.ReadTableAsync(this, Table).ConfigureAwait(false);
        PendingTable = TableNavigation.Read(
            Request,
            new(Kind: LabelTableKind.PendingReports, Sort: LabelTableSort.Received, Ascending: true),
            parameterPrefix: "Pending");
        PendingData = await UiPageHelpers.ReadTableAsync(this, PendingTable).ConfigureAwait(false);
    }
}
