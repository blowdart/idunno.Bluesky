// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using Microsoft.AspNetCore.Mvc.RazorPages;

namespace idunno.Bluesky.AspNet.Labeler.UI.Areas.Xenon.Pages;

/// <summary>Displays the Xenon operator landing page.</summary>
public sealed class IndexModel : PageModel
{
    /// <summary>Gets the active team role used to explain available workflows.</summary>
    internal string Role => HttpContext.Items["Xenon.CurrentRole"]?.ToString() ?? "Viewer";
}
