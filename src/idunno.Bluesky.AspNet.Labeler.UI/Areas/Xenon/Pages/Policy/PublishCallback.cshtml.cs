// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto.Authentication;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace idunno.Bluesky.AspNet.Labeler.UI.Areas.Xenon.Pages.Policy;

/// <summary>Completes one correlated labeler sign-in without changing the administrator's authenticated identity.</summary>
public sealed class PublishCallbackModel : PageModel
{
    /// <summary>Conditionally publishes only the current selected intent and independently verifies its declaration.</summary>
    /// <returns>A redirect to publication status with an explicit success or recoverable failure message.</returns>
    public async Task<IActionResult> OnGetAsync()
    {
        Response.Headers.CacheControl = "private, no-store";
        Response.Headers["Referrer-Policy"] = "no-referrer";
        try
        {
            TempData["Status"] = await HttpContext.RequestServices.GetRequiredService<PolicyPublicationService>()
                .CompleteAsync(HttpContext).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is OAuthException or InvalidOperationException or HttpRequestException or
            InvalidDataException or OperationCanceledException)
        {
            PolicyPublicationService.Failed(
                HttpContext.RequestServices.GetRequiredService<ILogger<PolicyPublicationService>>(),
                exception.GetType().Name);
            TempData["PublicationError"] = exception is InvalidOperationException ? exception.Message :
                "Publication did not confirm completion. Inspect policy state and the PDS declaration before retrying. Completed writes and negations are not rolled back.";
        }
        return RedirectToPage(pageName: "./Index", pageHandler: null, routeValues: null, fragment: "policy-publication");
    }
}
