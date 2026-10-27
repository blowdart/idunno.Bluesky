// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;

using idunno.Bluesky.Labeler.Moderation;
using idunno.Bluesky.Labeler.Administration;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace idunno.Bluesky.AspNet.Labeler.UI.Areas.Xenon.Pages.Cases;

/// <summary>Lists the live moderation work queue.</summary>
public sealed class IndexModel : PageModel
{
    /// <summary>Gets or sets the opaque continuation cursor for the selected queue.</summary>
    [BindProperty(SupportsGet = true)]
    public string? Cursor { get; set; }

    /// <summary>Gets the selected workflow queue.</summary>
    [BindProperty(SupportsGet = true)]
    public LabelCaseWorkflow Queue { get; set; } = LabelCaseWorkflow.Unresolved;

    /// <summary>Gets the serialized queue result.</summary>
    internal JsonElement Data { get; private set; }

    /// <summary>Gets the counted navigation and validated global ordering for this queue.</summary>
    internal TableNavigation? Table { get; private set; }

    /// <summary>Loads one bounded page of the selected moderation queue.</summary>
    public async Task OnGetAsync()
    {
        if (!ModelState.IsValid || !Enum.IsDefined(Queue) || Cursor?.Length > 512)
        {
            ViewData["Error"] = "Choose a valid workflow queue and reload a valid continuation cursor.";
            return;
        }

        Table = TableNavigation.Read(Request, new(LabelTableKind.Cases, Queue: Queue));
        Data = await UiPageHelpers.ReadTableAsync(this, Table).ConfigureAwait(false);
    }
}
