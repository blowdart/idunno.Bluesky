// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;

using idunno.Bluesky.Labeler.Moderation;
using idunno.Bluesky.Labeler.Administration;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace idunno.Bluesky.AspNet.Labeler.UI.Areas.Xenon.Pages.Appeals;

/// <summary>Lists appeals and links each one to its case for explicit review.</summary>
public sealed class IndexModel : PageModel
{
    /// <summary>Gets or sets the exclusive appeal identifier for the next page.</summary>
    [BindProperty(SupportsGet = true)]
    public long AfterId { get; set; }

    /// <summary>Gets the optional status filter, defaulting to pending appeals when no filter is supplied.</summary>
    [BindProperty(SupportsGet = true)]
    public LabelAppealStatus? Status { get; set; }

    /// <summary>Gets the appeal response.</summary>
    internal JsonElement Data { get; private set; }

    /// <summary>Gets counted appeal navigation preserving the current status filter.</summary>
    internal TableNavigation? Table { get; private set; }

    /// <summary>Loads a bounded page of appeals.</summary>
    public async Task OnGetAsync()
    {
        if (!Request.Query.ContainsKey(nameof(Status)))
        {
            Status = LabelAppealStatus.Pending;
        }

        if (!ModelState.IsValid || AfterId < 0 || Status is { } status && !Enum.IsDefined(status))
        {
            ViewData["Error"] = "Choose a valid appeal status and continuation identifier.";
            return;
        }

        Table = TableNavigation.Read(Request, new(LabelTableKind.Appeals, AppealStatus: Status));
        Data = await UiPageHelpers.ReadTableAsync(this, Table).ConfigureAwait(false);
    }
}
