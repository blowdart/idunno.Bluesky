// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Security.Claims;

using idunno.AtProto;
using idunno.AtProto.Authentication;
using idunno.Bluesky.AspNet.Authentication;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Samples.AspNetProgressiveAuthentication.Areas.Bluesky.Pages;

/// <summary>
/// Processes initial login and session-bound progressive consent.
/// </summary>
/// <param name="manager">The sign-in manager.</param>
/// <param name="oauth">The OAuth client.</param>
/// <param name="edits">The pending edit store.</param>
/// <param name="logger">The diagnostic logger.</param>
/// <remarks>
/// <para>
/// This page overrides the authentication UI package's callback so initial login and progressive consent share
/// the configured callback URI. Unlike a normal sign-in, progressive consent must retain the editing session,
/// validate the original account and recover its pending edit before replacing credentials.
/// </para>
/// </remarks>
[AllowAnonymous]
[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
public partial class CallbackModel(BlueskySignInManager manager, ProfileOAuthClient oauth, ProfileEditStore edits, ILogger<CallbackModel> logger) : PageModel
{
    /// <summary>
    /// Handles the correlated OAuth response without writing the profile.
    /// </summary>
    /// <returns>The fixed profile route or a validated initial login route.</returns>
    /// <remarks>
    /// <para>
    /// A successful progressive callback makes the saved edit eligible for completion, not yet saved.
    /// The profile editor completes it with a separate single-use, antiforgery-protected POST.
    /// </para>
    /// </remarks>
    public async Task<IActionResult> OnGet()
    {
        // LoadState validates the browser correlation cookie and atomically consumes the saved OAuth state.
        // Neither query-string edit identifiers nor return routes can select a pending progressive operation.
        OAuthLoginState? state = await manager.LoadState();
        if (state is null)
        {
            return LoginFailure("The authorization response expired or was already used. No profile was saved.");
        }

        string? pendingId = null;
        state.ExtraProperties?.TryGetValue(ProfilePermissions.PendingEditKey, out pendingId);
        AuthenticateResult session = await HttpContext.AuthenticateAsync(BlueskyAuthenticationDefaults.AuthenticationScheme);
        ProfileEditOwner? owner = GetOwner(session);

        // A progressive request must belong to the same DID and protected-ticket session that submitted the edit.
        // Taking consent moves the draft out of AwaitingConsent so another callback cannot exchange it again.
        if (pendingId is not null && (owner is null || !edits.TakeConsent(owner, pendingId)))
        {
            return LoginFailure("The profile authorization no longer belongs to this session, expired, or was already used. No profile was saved.");
        }

        try
        {
            // Validate the OAuth response and, for progressive consent, the expected DID on an isolated agent.
            // Its credentials must not reach the shared identity store until the checks below succeed.
            DPoPAccessCredentials? credentials = await oauth.Authorize(state, Request.QueryString.Value![1..],
                pendingId is not null ? new Did(owner!.Did) : null, HttpContext.RequestAborted);

            if (credentials is null)
            {
                return ConsentFailure(owner, pendingId, "Authorization was denied or canceled. Your edits have not been saved.");
            }

            // Keep this guard even though the real OAuth adapter rejects mismatches before publishing credentials.
            if (pendingId is not null && credentials.Did.Value != owner!.Did)
            {
                return ConsentFailure(owner, pendingId, "Authorization used a different account. Your edits have not been saved.");
            }

            // Consent must retain the reads as well as granting profile writes; requested scopes alone are not
            // proof of an explicit grant. Opaque reference grants are ultimately enforced by the PDS.
            if (!ProfilePermissions.HasRequiredScopes(credentials, requireWrite: pendingId is not null))
            {
                return ConsentFailure(owner, pendingId, "The required read or profile update permissions were not granted. Your edits have not been saved.");
            }

            // Preserve the ticket's session identifier and lifetime for an upgrade. Only a new login creates
            // a new editing session; ChangeSession invalidates its predecessor before invoking the handler.
            AuthenticationProperties properties = pendingId is not null ? session.Properties! : new()
            {
                AllowRefresh = true,
                IsPersistent = true
            };
            if (pendingId is null)
            {
                properties.Items[ProfilePermissions.SessionKey] = Guid.NewGuid().ToString("N");
            }

            if (pendingId is not null)
            {
                // Hold the session gate across the ownership check and the entire credential/ticket commit.
                // Concurrent logout or new login must invalidate either before this check or after sign-in completes.
                // Mark the draft ready only after credentials have been persisted by the authentication handler.
                // This GET never saves a profile, and progressive consent never uses a supplied return route.
                if (!await edits.CommitConsent(owner!, pendingId,
                    () => HttpContext.SignInAsync(BlueskyAuthenticationDefaults.AuthenticationScheme,
                        new ClaimsPrincipal(IIdentityStore.BuildClaimsIdentity(credentials)), properties),
                    HttpContext.RequestAborted))
                {
                    return LoginFailure("The editing session ended or the pending edit expired or was replaced. No profile was saved.");
                }

                return RedirectToPage("/Manage/Index", new { area = "" });
            }

            await edits.ChangeSession(owner,
                () => HttpContext.SignInAsync(BlueskyAuthenticationDefaults.AuthenticationScheme,
                    new ClaimsPrincipal(IIdentityStore.BuildClaimsIdentity(credentials)), properties),
                HttpContext.RequestAborted);

            // Ordinary login retains the UI's return-route behavior, but only from correlated server-side state
            // and only for local routes. Ignore any returnUrl supplied directly on the callback query string.
            string? returnUrl = null;
            state.ExtraProperties?.TryGetValue(Constants.ReturnUrlKey, out returnUrl);
            return LocalRedirect(Url.IsLocalUrl(returnUrl) ? returnUrl! : Url.Content("~/"));
        }
        catch (Exception exception) when (exception is OAuthException or HttpRequestException or OperationCanceledException)
        {
            AuthorizationFailed(logger, exception);
            return ConsentFailure(owner, pendingId, "Authorization failed. Your edits have not been saved. You can retry Update.");
        }
    }

    /// <summary>
    /// Extracts the account and editing-session identity from an authenticated ticket.
    /// </summary>
    /// <param name="session">The authentication result containing the protected ticket properties.</param>
    /// <returns>The draft owner, or <see langword="null"/> if the ticket lacks an authenticated DID or session identifier.</returns>
    internal static ProfileEditOwner? GetOwner(AuthenticateResult session) =>
        session.Succeeded &&
        session.Principal?.FindFirst(AtProtoClaims.Did)?.Value is string did &&
        session.Properties is not null &&
        session.Properties.Items.TryGetValue(ProfilePermissions.SessionKey, out string? key) && key is not null
            ? new(did, key) : null;

    /// <summary>
    /// Preserves a failed progressive draft for recovery and routes the user to its editor.
    /// </summary>
    /// <param name="owner">The original draft owner, if this is progressive consent.</param>
    /// <param name="id">The correlated draft identifier, if this is progressive consent.</param>
    /// <param name="message">The failure explanation to display without claiming a save succeeded.</param>
    /// <returns>The editor redirect for progressive consent, or the ordinary login failure redirect.</returns>
    private RedirectToPageResult ConsentFailure(ProfileEditOwner? owner, string? id, string message)
    {
        if (owner is not null && id is not null)
        {
            edits.FinishConsent(owner, id, authorized: false, message);
            TempData["ProfileMessage"] = message;
            return RedirectToPage("/Manage/Index", new { area = "" });
        }

        return LoginFailure(message);
    }

    /// <summary>
    /// Reports an invalid or failed callback without signing out an existing authenticated user.
    /// </summary>
    /// <param name="message">The failure explanation to display on the destination page.</param>
    /// <returns>The editor redirect for an authenticated user, or the login redirect for an anonymous user.</returns>
    private RedirectToPageResult LoginFailure(string message)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            TempData["ProfileMessage"] = message;
            return RedirectToPage("/Manage/Index", new { area = "" });
        }

        TempData["ErrorMessage"] = message;
        return RedirectToPage("/Login", new { area = "Bluesky" });
    }

    /// <summary>
    /// Logs an OAuth validation, transport or cancellation exception without exposing it in the user-facing message.
    /// </summary>
    /// <param name="logger">The diagnostic logger.</param>
    /// <param name="exception">The authorization failure.</param>
    [LoggerMessage(Level = LogLevel.Warning, Message = "Profile authorization failed.")]
    private static partial void AuthorizationFailed(ILogger logger, Exception exception);
}
