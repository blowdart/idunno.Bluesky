using System.ComponentModel.DataAnnotations;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

using idunno.AtProto;
using idunno.Bluesky;
using idunno.Bluesky.AspNet.Authentication;

namespace Samples.AspNetAuthentication.Areas.Account.Pages;

[AllowAnonymous]
public class LoginModel(BlueskySignInManager blueskyAuthenticationManager, ILogger<LoginModel> logger) : PageModel
{
    private const string HandleCookieName = "Handle";

    private readonly TimeSpan _rememberMeCookieLifetime = new (365, 0, 0, 0, 0);

    [BindProperty]
    public InputModel Input { get; set; } = default!;
    
    public class InputModel
    {
        [Required]
        [RegularExpression(Handle.ValidationRegex, ErrorMessage = "Not a valid handle")]
        [Display(Name = "Handle", Prompt = "Handle", Description = "Handle")]
        public string UserHandle { get; set; } = default!;

        [Display(Name = "Remember my handle?")]
        public bool RememberHandle { get; set; }
    }

    public string? ReturnUrl { get; set; }

    [TempData]
    public string? ErrorMessage { get; set; }

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

            Response.Cookies.Append(HandleCookieName, parsedHandle.Value, new CookieOptions
            {
                Expires = DateTimeOffset.UtcNow + _rememberMeCookieLifetime
            });
        }

        ReturnUrl = returnUrl;
    }

    public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
    {
        returnUrl ??= Url.Content("~/");

        if (!string.IsNullOrEmpty(Input.UserHandle))
        {
            using var agent = new BlueskyAgent();
            try
            {
                Did? resolvedHandle = await agent.ResolveHandle(Input.UserHandle);
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
            Response.Cookies.Append(HandleCookieName, Input.UserHandle, new CookieOptions
            {
                Expires = DateTimeOffset.UtcNow + _rememberMeCookieLifetime
            });
        }
        else if (Request.Cookies.ContainsKey(HandleCookieName))
        {
            Response.Cookies.Delete(HandleCookieName);
        }

        try
        {
            var redirectUri = await blueskyAuthenticationManager.CreateRedirectUri(
                Input.UserHandle,
                stateExtraProperties: new Dictionary<string, string>()
                {
                    { Constants.ReturnUrlKey, returnUrl }
                });
                
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
}
