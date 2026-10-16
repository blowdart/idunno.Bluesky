// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.Bluesky.AspNet.Authentication;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Samples.AspNetProgressiveAuthentication.Areas.Bluesky.Pages;

/// <summary>
/// Serializes the entire logout operation with progressive credential installation.
/// </summary>
/// <param name="manager">The sign-in manager.</param>
/// <param name="edits">The account/session-bound pending edit store.</param>
/// <remarks>
/// <para>
/// The default handler revokes and removes credentials before raising its sign-out event. This application-owned
/// page acquires the shared gate first and retains it through revocation, identity removal and cookie deletion.
/// Razor Pages validates antiforgery on the logout POST; GET does not end the session.
/// </para>
/// </remarks>
[AllowAnonymous]
public class LogoutModel(BlueskySignInManager manager, ProfileEditStore edits) : PageModel
{
    /// <summary>
    /// Invalidates pending edits and signs out without allowing an overlapping consent commit.
    /// </summary>
    /// <param name="returnUrl">The optional local destination after logout.</param>
    /// <returns>The local destination or home page redirect.</returns>
    public async Task<IActionResult> OnPost(string? returnUrl = null)
    {
        var session = await HttpContext.AuthenticateAsync(manager.AuthenticationScheme);
        // OnSigningOut is too late: the library handler has already revoked and removed the identity by then.
        // Acquire the consent gate before SignOut starts and hold it through cookie deletion, so an overlapping
        // callback cannot reinstall credentials between identity removal and the sign-out event.
        await edits.ChangeSession(CallbackModel.GetOwner(session), () => manager.SignOut(), HttpContext.RequestAborted);

        return LocalRedirect(Url.IsLocalUrl(returnUrl) ? returnUrl! : Url.Content("~/"));
    }
}
