using System.Diagnostics.CodeAnalysis;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace idunno.Bluesky.AspNet.Authentication.UI.Areas.Bluesky.Pages;

/// <summary>
///  This API supports the Bluesky ASP.NET Core Authentication default UI infrastructure and is not intended to be used directly from your code.
/// </summary>
/// <param name="blueskySignInManager">An instance of <see cref="BlueskySignInManager"/></param>
[AllowAnonymous]
public class LogoutModel(BlueskySignInManager blueskySignInManager) : PageModel
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
        /// Gets the return URL.
        /// </summary>
        [SuppressMessage("Design", "CA1056:URI-like properties should not be strings", Justification = "Return URL comes from the query string, as a string.")]
        public string? ReturnUrl { get; set; }
    }

    /// <summary>
    ///   This API supports the Bluesky ASP.NET Core Authentication default UI infrastructure and is not intended to be used directly from your code.
    ///   This API may change or be removed in future releases.
    /// </summary>
    /// <param name="returnUrl">The URL to redirect the user to after successful logout.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the action result.</returns>
    [SuppressMessage("Design", "CA1054:URI-like parameters should not be strings", Justification = "Return URL comes from the query string, as a string.")]
    public async Task<IActionResult> OnPost(string? returnUrl = null)
    {
        await blueskySignInManager.SignOut().ConfigureAwait(false);

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
