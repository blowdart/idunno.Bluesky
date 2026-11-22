// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace idunno.Bluesky.AspNet.Labeler.UI.Areas.Xenon.Pages;

/// <summary>Displays font, icon and third-party software acknowledgements, and serves the full plain-text notices.</summary>
public sealed class AcknowledgementsModel : PageModel
{
    /// <summary>Gets the third-party notices embedded into this assembly at build time.</summary>
    internal NoticeDocument Notices { get; } = ThirdPartyNotices.Document;

    /// <summary>Downloads the unmodified THIRD-PARTY-NOTICES.txt shipped with this build.</summary>
    /// <returns>The notices as UTF-8 plain text, or not found when the build did not embed them.</returns>
    public IActionResult OnGetText()
    {
        string text = ThirdPartyNotices.Text;
        if (text.Length == 0)
        {
            return NotFound();
        }

        return File(Encoding.UTF8.GetBytes(text), "text/plain; charset=utf-8", ThirdPartyNotices.FileName);
    }
}
