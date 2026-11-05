// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;

using idunno.Bluesky.Labeler.Administration;
using idunno.Bluesky.Labeler.Moderation;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace idunno.Bluesky.AspNet.Labeler.UI.Areas.Xenon.Pages.Appeals;

/// <summary>Lists appeals and links each one to its case for explicit review.</summary>
public sealed class IndexModel : PageModel
{
    /// <summary>Gets or sets the exclusive appeal identifier for the next page.</summary>
    [BindProperty(SupportsGet = true)]
    public long AfterId { get; set; }

    /// <summary>Gets the optional exact appeal outcome filter within the selected workflow queue.</summary>
    [BindProperty(SupportsGet = true)]
    public LabelAppealStatus? Status { get; set; }

    /// <summary>Gets or sets the appeal workflow queue, defaulting to unresolved work.</summary>
    [BindProperty(SupportsGet = true)]
    public LabelCaseWorkflow Queue { get; set; } = LabelCaseWorkflow.Unresolved;

    /// <summary>Gets the appeal response.</summary>
    internal JsonElement Data { get; private set; }

    /// <summary>Gets counted appeal navigation preserving the current status filter.</summary>
    internal TableNavigation? Table { get; private set; }

    /// <summary>Loads a bounded page of appeals.</summary>
    public async Task OnGetAsync()
    {
        if (!Request.Query.ContainsKey(nameof(Queue)) && Request.Query.ContainsKey(nameof(Status)))
        {
            Queue = Status switch
            {
                LabelAppealStatus.Pending => LabelCaseWorkflow.Unresolved,
                null => LabelCaseWorkflow.All,
                _ => LabelCaseWorkflow.Resolved
            };
        }

        if (!ModelState.IsValid || AfterId < 0 || Status is { } status && !Enum.IsDefined(status) ||
            Queue is not (LabelCaseWorkflow.Unresolved or LabelCaseWorkflow.InReview or LabelCaseWorkflow.Escalated or LabelCaseWorkflow.Resolved or LabelCaseWorkflow.All))
        {
            ViewData["Error"] = "Choose an available appeal status and refresh the page before trying again.";
            return;
        }

        Table = TableNavigation.Read(Request, new(LabelTableKind.Appeals, AppealStatus: Status, Workflow: Queue));
        Data = await UiPageHelpers.ReadTableAsync(this, Table).ConfigureAwait(false);
    }
}
