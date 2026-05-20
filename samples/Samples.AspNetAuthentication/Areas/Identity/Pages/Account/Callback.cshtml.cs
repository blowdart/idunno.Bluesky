using System.ComponentModel.DataAnnotations;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

using idunno.Bluesky.AspNet.Authentication;

namespace Samples.AspNetAuthentication.Areas.Identity.Pages.Account;

[AllowAnonymous]
public class CallbackModel(BlueskySignInManager blueskySignInManager, ILogger<LoginModel> logger) : PageModel
{

    [BindProperty(SupportsGet = true)]
    public InputModel Input { get; set; } = default!;

    public class InputModel
    {
        public string? ReturnUrl { get; set; }

        [Required]
        public string? Code { get; set; }

        [Required]
        public string? Iss { get; set; }

        [Required]
        public string? State { get; set; }
    }

    public async Task<IActionResult> OnGet()
    {
        if (!ModelState.IsValid)
        {
#pragma warning disable CA1848 // Use the LoggerMessage delegates
            logger.LogInformation("Invalid ModelState");
#pragma warning restore CA1848 // Use the LoggerMessage delegates
            return BadRequest();
        }

        var result = await blueskySignInManager.SignIn();

        if (!result.Succeeded)
        {
#pragma warning disable CA1848 // Use the LoggerMessage delegates
            logger.LogInformation("SignIn failed");
#pragma warning restore CA1848 // Use the LoggerMessage delegates
            return BadRequest();
        }
        else
        {
            string? returnUrl = null;

            if (result.OAuthLoginState is not null &&
                result.OAuthLoginState.ExtraProperties is not null)
            {
                result.OAuthLoginState.ExtraProperties.TryGetValue(Constants.ReturnUrlKey, out returnUrl);
            }

            returnUrl ??= Input.ReturnUrl;
            returnUrl ??= Url.Content("~/");

            return LocalRedirect(returnUrl);
        }
    }
}
