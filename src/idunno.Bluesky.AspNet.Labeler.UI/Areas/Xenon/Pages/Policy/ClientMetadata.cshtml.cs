// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.Bluesky.AspNet.Authentication;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.DependencyInjection;

namespace idunno.Bluesky.AspNet.Labeler.UI.Areas.Xenon.Pages.Policy;

/// <summary>Publishes the separately scoped declaration client's metadata from configured HTTPS origins.</summary>
public sealed class ClientMetadataModel : PageModel
{
    /// <summary>Returns public metadata without disclosing credentials or trusting request host headers.</summary>
    /// <returns>The declaration publication client's metadata.</returns>
    public IActionResult OnGet()
    {
        PolicyPublicationService publisher = HttpContext.RequestServices.GetRequiredService<PolicyPublicationService>();
        return Content(new BlueskyOAuthClientMetadataOptions().GenerateJson(publisher.CreateOptions()), "application/json");
    }
}
