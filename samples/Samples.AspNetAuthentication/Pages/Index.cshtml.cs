// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Samples.AspNetAuthentication.Pages;

public class IndexModel : PageModel
{
    public void OnGet()
    {
        System.Diagnostics.Debug.WriteLine(HttpContext.User?.Identity?.Name);
    }
}
