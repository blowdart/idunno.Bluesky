using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

using Microsoft.Extensions.Logging;

using idunno.Bluesky.AspNet.Authentication;

namespace idunno.Bluesky.AspNet.Authentication.UI.Areas.Bluesky.Pages;

/// <summary>
///   This API supports the Bluesky ASP.NET Core Authentication default UI infrastructure and is not intended to be used directly from your code.
///   This API may change or be removed in future releases.
/// </summary>
/// <param name="blueskySignInManager">An instance of <see cref="BlueskySignInManager"/></param>
/// <param name="logger">An instance of <see cref="ILogger{CallbackModel}"/></param>
[AllowAnonymous]
[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
public class CallbackModel(BlueskySignInManager blueskySignInManager, ILogger<CallbackModel> logger) : PageModel
{

    /// <summary>
    ///   This API supports the Bluesky ASP.NET Core Authentication default UI infrastructure and is not intended to be used directly from your code.
    ///   This API may change or be removed in future releases.
    /// </summary>
    [BindProperty(SupportsGet = true)]
    public InputModel Input { get; set; } = default!;

    /// <summary>
    ///   This API supports the Bluesky ASP.NET Core Authentication default UI infrastructure and is not intended to be used directly from your code.
    ///   This API may change or be removed in future releases.
    /// </summary>
    [SuppressMessage("Design", "CA1034:Nested types should not be visible", Justification = "Standard asp.net razor pages practice")]
    public class InputModel
    {
        /// <summary>
        /// Gets or sets the return URL to redirect the user to after successful authentication.
        /// </summary>
        [SuppressMessage("Design", "CA1056:URI-like properties should not be strings", Justification = "Return URL comes from the query string, as a string.")]
        public string? ReturnUrl { get; set; }

        /// <summary>
        /// Gets or sets the authorization code received from Bluesky after successful authentication.
        /// </summary>
        [Required]
        public string? Code { get; set; }

        /// <summary>
        /// Gets or sets the issuer identifier received from Bluesky after successful authentication.
        /// </summary>
        [Required]
        public string? Iss { get; set; }

        /// <summary>
        /// Gets or sets the state parameter received from Bluesky after successful authentication.
        /// </summary>
        [Required]
        public string? State { get; set; }
    }

    /// <summary>
    /// Processes the OAuth callback from Bluesky after the user has authenticated.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation. The task result contains an <see cref="IActionResult"/>.</returns>
    public async Task<IActionResult> OnGet()
    {
        if (!ModelState.IsValid)
        {
#pragma warning disable CA1848 // Use the LoggerMessage delegates
            logger.LogInformation("Invalid ModelState");
#pragma warning restore CA1848 // Use the LoggerMessage delegates
            return BadRequest();
        }

        SignInResult result = await blueskySignInManager.SignIn().ConfigureAwait(false);

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
