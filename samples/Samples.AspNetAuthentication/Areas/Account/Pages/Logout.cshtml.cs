using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

using idunno.Bluesky.AspNet.Authentication;

namespace Samples.AspNetAuthentication.Areas.Account.Pages;

[AllowAnonymous]
public class LogoutModel() : PageModel
{
    [BindProperty(SupportsGet = true)]
    public InputModel Input { get; set; } = default!;

    public class InputModel
    {
        public string? ReturnUrl { get; set; }
    }

    public async Task OnPost()
    {
        await HttpContext.SignOutAsync(BlueskyAuthenticationDefaults.AuthenticationScheme);
    }
}
