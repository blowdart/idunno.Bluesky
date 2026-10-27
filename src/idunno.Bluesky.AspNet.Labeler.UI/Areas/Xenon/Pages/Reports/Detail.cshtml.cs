// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.DependencyInjection;

namespace idunno.Bluesky.AspNet.Labeler.UI.Areas.Xenon.Pages.Reports;

/// <summary>Displays an authorized report and the current public view of its subject.</summary>
public sealed class DetailModel : PageModel
{
    /// <summary>Gets or sets the report identifier from the route.</summary>
    [BindProperty(SupportsGet = true)]
    public long Id { get; set; }

    /// <summary>Gets or sets the newest-first shared case history page.</summary>
    [BindProperty(SupportsGet = true)]
    public long HistoryPage { get; set; } = 1;

    /// <summary>Gets the compact subject and author label display, or null when loading failed.</summary>
    internal ReviewLabelDisplay? Labels { get; private set; }

    /// <summary>Gets the bounded shared case and report activity.</summary>
    internal ModerationActivityPageView? History { get; private set; }

    /// <summary>Gets the authorized report response.</summary>
    internal JsonElement Data { get; private set; }

    /// <summary>Gets the current public subject preview, independently of the persisted report evidence.</summary>
    internal SubjectPreview? Preview { get; private set; }

    /// <summary>Loads the report before requesting any public preview content.</summary>
    /// <returns>A task representing report and preview loading.</returns>
    public async Task OnGetAsync()
    {
        if (!ModelState.IsValid || Id < 1 || HistoryPage <= 0)
        {
            ViewData["Error"] = "Choose a valid report number.";
            return;
        }

        AdministrationResponse response = await UiPageHelpers.DispatchAsync(
            this,
            new AdministrationCommand(
                Method: "getReport",
                Reason: "Xenon report inspection.",
                Moderation: new(ReportId: Id))).ConfigureAwait(false);
        Data = UiPageHelpers.ToElement(response);
        if (response.Succeeded && response.Moderation?.Reports is { Length: 1 } reportViews)
        {
            JsonElement subject = Data.GetProperty("moderation").GetProperty("reports")[0].GetProperty("report").GetProperty("subject");
            Preview = await HttpContext.RequestServices.GetRequiredService<SubjectPreviewService>()
                .LoadAsync(subject, HttpContext.RequestAborted).ConfigureAwait(false);
            AdministrationResponse labels = await UiPageHelpers.DispatchAsync(
                this,
                new AdministrationCommand(
                    Method: "getCaseLabels",
                    Reason: "Xenon report label context.",
                    Moderation: new(CaseId: reportViews[0].CaseId))).ConfigureAwait(false);
            if (labels.Succeeded && labels.Moderation is { } observed)
            {
                Labels = new(
                    subject.TryGetProperty("uri", out _),
                    observed.AccountLabels ?? [],
                    observed.LabelChoices?.Where(label => label.Applied).Select(label => label.Value).ToArray() ?? []);
            }

            AdministrationResponse history = await UiPageHelpers.DispatchAsync(
                this,
                new AdministrationCommand(
                    Method: "getCaseActivity",
                    Reason: "Xenon report and shared case history.",
                    Moderation: new(CaseId: reportViews[0].CaseId, HistoryPage: HistoryPage))).ConfigureAwait(false);
            History = history.Moderation?.Activity;
        }
    }
}
