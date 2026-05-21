using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

using idunno.Bluesky.AspNet.Authentication;

namespace Samples.AspNetAuthentication.Areas.Identity.Pages.Account;

[AllowAnonymous]
public class LogoutModel(BlueskySignInManager blueskySignInManager) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public InputModel Input { get; set; } = default!;

    public class InputModel
    {
        public string? ReturnUrl { get; set; }
    }

    public async Task<IActionResult> OnPost(string? returnUrl = null)
    {
        await blueskySignInManager.SignOut();

        if (returnUrl != null)
        {
            return LocalRedirect(returnUrl);
        }
        else
        {
            // This needs to be a redirect so that the browser performs a new
            // request and the identity for the user gets updated.
            return RedirectToPage();
        }
    }
}
