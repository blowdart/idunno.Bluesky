// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.DependencyInjection;

namespace idunno.Bluesky.AspNet.Labeler.UI.Areas.Xenon.Pages;

/// <summary>Displays the Xenon operator landing page.</summary>
public sealed class IndexModel : PageModel
{
    /// <summary>Returns current workflow totals after live team authorization without permitting cached responses.</summary>
    /// <returns>The workflow counts or an explicit service failure.</returns>
    public async Task<IActionResult> OnGetCountsAsync()
    {
        Response.Headers.CacheControl = "private, no-store";
        Response.Headers.XContentTypeOptions = "nosniff";
        AdministrationResponse response = await HttpContext.RequestServices.GetRequiredService<LabelerUiAdministration>()
            .DispatchAsync(
                principal: User,
                command: new AdministrationCommand(Method: "getModerationOverview", Reason: "Xenon background workflow totals."),
                cancellationToken: HttpContext.RequestAborted).ConfigureAwait(false);
        if (!response.Succeeded || response.Moderation?.OpenCounts is not { } counts)
        {
            return StatusCode(response.Error is "Unauthorized" or "Forbidden"
                ? StatusCodes.Status403Forbidden
                : StatusCodes.Status503ServiceUnavailable);
        }

        return Content(JsonSerializer.Serialize(counts, AdministrationJsonContext.Default.ModerationOpenCountsView), "application/json");
    }

    /// <summary>Gets the active team role used to explain available workflows.</summary>
    internal string Role => HttpContext.Items["Xenon.CurrentRole"]?.ToString() ?? "Viewer";
}
