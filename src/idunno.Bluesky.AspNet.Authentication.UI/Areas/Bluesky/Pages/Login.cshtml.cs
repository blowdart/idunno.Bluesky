using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;

using idunno.AtProto;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;

namespace idunno.Bluesky.AspNet.Authentication.UI.Areas.Bluesky.Pages;

/// <summary>
///  This API supports the Bluesky ASP.NET Core Authentication default UI infrastructure and is not intended to be used directly from your code.
/// </summary>
/// <param name="blueskySignInManager">An instance of <see cref="BlueskySignInManager"/></param>
/// <param name="logger">An instance of <see cref="ILogger{LoginModel}"/></param>
[AllowAnonymous]
public class LoginModel(BlueskySignInManager blueskySignInManager, ILogger<LoginModel> logger) : PageModel
{
    private const string HandleCookieName = "Handle";

    private readonly TimeSpan _rememberMeCookieLifetime = new (days: 30, hours: 0, minutes: 0, seconds: 0);

    /// <summary>
    ///   This API supports the Bluesky ASP.NET Core Authentication default UI infrastructure and is not intended to be used directly from your code.
    ///   This API may change or be removed in future releases.
    /// </summary>
    [BindProperty]
    public InputModel Input { get; set; } = default!;

    /// <summary>
    ///   This API supports the Bluesky ASP.NET Core Authentication default UI infrastructure and is not intended to be used directly from your code.
    ///   This API may change or be removed in future releases.
    /// </summary>
    [SuppressMessage("Design", "CA1034:Nested types should not be visible", Justification = "Standard asp.net razor pages practice")]
    public class InputModel
    {
        /// <summary>
        ///  Gets or sets the user handle to be used as a hint for authentication.
        /// </summary>
        [Required]
        [RegularExpression(Handle.ValidationRegex, ErrorMessage = "Not a valid handle")]
        [Display(Name = "Handle", Prompt = "Handle", Description = "Handle")]
        public string UserHandle { get; set; } = default!;

        /// <summary>
        /// Gets or sets a value indicating whether the user wants to remember their handle for future logins.
        /// </summary>
        [Display(Name = "Remember my handle?")]
        public bool RememberHandle { get; set; }
    }
    
    /// <summary>
    ///   This API supports the Bluesky ASP.NET Core Authentication default UI infrastructure and is not intended to be used directly from your code.
    ///   This API may change or be removed in future releases.
    /// </summary>
    [SuppressMessage("Design", "CA1056:URI-like properties should not be strings", Justification = "Return URL comes from the query string, as a string.")]
    public string? ReturnUrl { get; set; }

    /// <summary>
    ///   This API supports the Bluesky ASP.NET Core Authentication default UI infrastructure and is not intended to be used directly from your code.
    ///   This API may change or be removed in future releases.
    /// </summary>
    [TempData]
    public string? ErrorMessage { get; set; }

    /// <summary>
    ///   This API supports the Bluesky ASP.NET Core Authentication default UI infrastructure and is not intended to be used directly from your code.
    ///   This API may change or be removed in future releases.
    /// </summary>
    /// <param name="returnUrl">The URL to redirect the user to after successful authentication.</param>
    [SuppressMessage("Design", "CA1054:URI-like parameters should not be strings", Justification = "Return URL comes from the query string, as a string.")]
    public void OnGet(string? returnUrl = null)
    {
        returnUrl ??= Url.Content("~/");

        Response.Cookies.Delete(Constants.CorrelationCookieName);

        if (!string.IsNullOrEmpty(ErrorMessage))
        {
            ModelState.AddModelError(string.Empty, ErrorMessage);
        }

        if (Input is null && 
            Request.Cookies[HandleCookieName] is string cookieValue &&
            Handle.TryParse(cookieValue, out Handle? parsedHandle))
        {
            Input = new InputModel()
            {
                UserHandle = parsedHandle.Value,
                RememberHandle = true
            };

            // Bluesky handles are public information, so we can store them in a cookie without needing to encrypt them.
            // We will set the cookie to expire in 30 days if the user has selected "Remember my handle".
            Response.Cookies.Append(HandleCookieName, parsedHandle.Value, BuildCookieOptions(isSecure: Request.IsHttps, expires: _rememberMeCookieLifetime));
        }

        ReturnUrl = returnUrl;
    }

    /// <summary>
    ///   This API supports the Bluesky ASP.NET Core Authentication default UI infrastructure and is not intended to be used directly from your code.
    ///   This API may change or be removed in future releases.
    /// </summary>
    /// <param name="returnUrl">The URL to redirect the user to after successful authentication.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the action result.</returns>
    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "We want to catch all exceptions to display a generic error message to the user.")]
    [SuppressMessage("Design", "CA1054:URI-like parameters should not be strings", Justification = "Return URL comes from the query string, as a string.")]
    public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
    {
        returnUrl ??= Url.Content("~/");

        if (!string.IsNullOrEmpty(Input.UserHandle))
        {
            using var agent = new BlueskyAgent();
            try
            {
                Did? resolvedHandle = await agent.ResolveHandle(Input.UserHandle).ConfigureAwait(false);
                if (resolvedHandle is null)
                {
                    ModelState.AddModelError(nameof(Input.UserHandle), $"The handle {Input.UserHandle} cannot be found.");
                }
            }
            catch (HttpRequestException)
            {
                ModelState.AddModelError(nameof(Input.UserHandle), $"The handle {Input.UserHandle} cannot be found.");
            }
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        if (Input.RememberHandle)
        {
            Response.Cookies.Append(HandleCookieName, Input.UserHandle, BuildCookieOptions(isSecure: Request.IsHttps, expires: _rememberMeCookieLifetime));
        }
        else if (Request.Cookies.ContainsKey(HandleCookieName))
        {
            Response.Cookies.Delete(HandleCookieName);
        }

        try
        {
            Uri redirectUri = await blueskySignInManager.CreateRedirectUri(
                Input.UserHandle,
                stateExtraProperties: new Dictionary<string, string>()
                {
                    { Constants.ReturnUrlKey, returnUrl }
                }).ConfigureAwait(false);
                
            return Redirect(redirectUri.ToString());
        }
        catch (Exception ex)
        {
            ErrorMessage = "Could not build the login URI";
#pragma warning disable CA1848 // Use the LoggerMessage delegates
            logger.LogError(0, ex, "Something went wrong building the login uri");
#pragma warning restore CA1848 // Use the LoggerMessage delegates
            return Page();
        }
    }


    [SuppressMessage("Minor Vulnerability", "S2092:Cookies should have the \"secure\" flag", Justification = "The secure flag is set based on the request's HTTPS status, or developer choice.")]
    private static CookieOptions BuildCookieOptions(bool isSecure, TimeSpan? expires = null)
    {
        return new CookieOptions
        {
            HttpOnly = true,
            IsEssential = true,
            SameSite = SameSiteMode.Strict,
            Secure = isSecure,
            Expires = expires.HasValue ? DateTimeOffset.UtcNow + expires.Value : null
        };
    }
}
