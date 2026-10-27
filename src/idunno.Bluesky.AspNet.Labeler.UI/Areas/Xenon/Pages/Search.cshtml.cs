// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace idunno.Bluesky.AspNet.Labeler.UI.Areas.Xenon.Pages;

/// <summary>Searches reports and appeals through the same live provider-backed inspection boundary as list pages.</summary>
public sealed class SearchModel : PageModel
{
    /// <summary>Gets or sets the literal, case-insensitive substring to find.</summary>
    [BindProperty(SupportsGet = true)]
    public string? Q { get; set; }

    /// <summary>Gets or sets the requested search result page.</summary>
    [BindProperty(SupportsGet = true)]
    public long PageNumber { get; set; } = 1;

    /// <summary>Gets the authorized result page, including handle-index coverage.</summary>
    internal ModerationSearchPageView? Results { get; private set; }

    /// <summary>Loads a bounded mixed result page without treating an empty query or unavailable storage as no matches.</summary>
    /// <returns>A task representing the completed search.</returns>
    public async Task OnGetAsync()
    {
        Q = Q?.Trim();
        ViewData["SearchQuery"] = Q;
        if (!ModelState.IsValid || PageNumber <= 0 || Q?.Length > 256 || Q is not null && Encoding.UTF8.GetByteCount(Q) > 512)
        {
            ViewData["Error"] = "Enter up to 256 characters (512 UTF-8 bytes) and a valid result page.";
            return;
        }

        if (string.IsNullOrEmpty(Q))
        {
            return;
        }

        AdministrationResponse response = await UiPageHelpers.DispatchAsync(
            this,
            new AdministrationCommand(
                Method: "searchModeration",
                Reason: "Xenon global search.",
                Moderation: new(SearchQuery: Q, HistoryPage: PageNumber))).ConfigureAwait(false);
        Results = response.Moderation?.Search;
    }
}
