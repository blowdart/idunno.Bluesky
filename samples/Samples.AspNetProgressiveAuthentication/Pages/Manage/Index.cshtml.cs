// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.ComponentModel.DataAnnotations;
using System.Net;

using idunno.AtProto;
using idunno.AtProto.Authentication;
using idunno.Bluesky;
using idunno.Bluesky.AspNet.Authentication;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

using Samples.AspNetProgressiveAuthentication.Areas.Bluesky.Pages;

namespace Samples.AspNetProgressiveAuthentication.Pages.Manage;

/// <summary>
/// Edits the current account's profile, obtaining the required profile-write permissions only when saving.
/// </summary>
/// <param name="agent">The authenticated agent.</param>
/// <param name="edits">The pending edit store.</param>
/// <param name="oauth">The OAuth client.</param>
/// <param name="logger">The diagnostic logger.</param>
/// <remarks>
/// <para>
/// The existing editor keeps its read-only login and CID-based concurrency check. Submitted values are retained
/// server-side while consent is in progress, then saved by a separate single-use POST after the callback validates
/// the account and installs upgraded credentials. Razor Pages validates antiforgery tokens for every POST handler.
/// </para>
/// </remarks>
[Authorize]
[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
public partial class IndexModel(BlueskyAgent agent, ProfileEditStore edits, ProfileOAuthClient oauth, ILogger<IndexModel> logger) : PageModel
{
    /// <summary>
    /// Gets or sets the edited display name.
    /// </summary>
    [Required]
    [StringLength(64)]
    [BindProperty]
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the edited description.
    /// </summary>
    [Required]
    [StringLength(256)]
    [BindProperty]
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the edited pronouns.
    /// </summary>
    [Required]
    [StringLength(256)]
    [BindProperty]
    public string Pronouns { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the profile version being edited.
    /// </summary>
    [ViewData]
    public Cid? Cid { get; set; }

    /// <summary>
    /// Gets the single-use completion identifier.
    /// </summary>
    /// <remarks>
    /// <para>Set only for an owner-bound draft made ready by the OAuth callback; the form never carries edited values.</para>
    /// </remarks>
    public string? CompletionId { get; private set; }

    /// <summary>
    /// Gets or sets the outcome displayed to the user.
    /// </summary>
    [TempData]
    public string? ProfileMessage { get; set; }

    /// <summary>
    /// Gets or sets the opaque identifier used to recover or discard a pending edit.
    /// </summary>
    /// <remarks>
    /// <para>
    /// TempData carries only the identifier, not the draft. It also lets the editor explain when an earlier draft
    /// has expired or been evicted instead of silently replacing it with the current profile.
    /// </para>
    /// </remarks>
    [TempData]
    public string? PendingEditId { get; set; }

    /// <summary>
    /// Loads the editor or recovers the session's pending edits.
    /// </summary>
    /// <returns>The profile editor.</returns>
    /// <remarks>
    /// <para>
    /// A ready draft displays the completion form, which JavaScript submits automatically or the user submits manually.
    /// This GET never writes the profile or consumes completion authorization.
    /// </para>
    /// </remarks>
    public async Task<IActionResult> OnGet()
    {
        ProfileEditOwner? owner = await Owner();
        if (owner is null)
        {
            return SessionFailure();
        }

        // Prefer the owner's draft over a fresh profile read so denial, cancellation and failed saves do not erase edits.
        PendingProfileEdit? pending = edits.Get(owner);
        if (pending is not null)
        {
            PendingEditId = pending.Id;
            Apply(pending.Edit);
            if (pending.Status == ProfileEditStatus.Ready)
            {
                // The callback installed credentials, but the actual save must still use an antiforgery-protected POST.
                CompletionId = pending.Id;
                ProfileMessage = "Profile permission granted. Completing your save...";
            }
            else
            {
                ModelState.AddModelError(string.Empty, pending.Message ??
                    "Your edits have not been saved. Consent is pending or was canceled. Click Update to retry.");
            }

            return Page();
        }

        // A remembered identifier with no server-side payload means expiry, eviction or restart, not a successful save.
        if (PendingEditId is not null)
        {
            ProfileMessage ??= "Your pending edits expired or are no longer available. No pending save was attempted. Please enter your changes again.";
            PendingEditId = null;
        }

        try
        {
            var result = await agent.GetProfile(cancellationToken: HttpContext.RequestAborted);
            if (!result.Succeeded)
            {
                ModelState.AddModelError(string.Empty, $"Could not read your profile (HTTP {(int)result.StatusCode}).");
                return Page();
            }

            Apply(new(result.Result.Value.DisplayName ?? string.Empty, result.Result.Value.Description ?? string.Empty,
                result.Result.Value.Pronouns ?? string.Empty, result.Result.Cid.Value));
        }
        catch (Exception exception) when (exception is HttpRequestException or OperationCanceledException)
        {
            ProfileOperationFailed(logger, exception);
            ModelState.AddModelError(string.Empty, "Could not read your profile. Please reload the page.");
        }

        return Page();
    }

    /// <summary>
    /// Saves an edit or starts a progressive permission request.
    /// </summary>
    /// <param name="cid">The profile version from the editor.</param>
    /// <returns>The consent redirect or save outcome.</returns>
    /// <remarks>
    /// <para>
    /// Validation happens before retaining an edit or requesting consent. The submitted CID is kept for the eventual
    /// conditional write, including across the browser's trip to the authorization server.
    /// </para>
    /// </remarks>
    public async Task<IActionResult> OnPost(Cid? cid)
    {
        // Keep the original version in the rendered form even when field validation fails.
        Cid = cid;
        ProfileEditOwner? owner = await Owner();
        if (owner is null)
        {
            return SessionFailure();
        }

        if (cid is null)
        {
            ModelState.AddModelError(string.Empty, "Missing profile version. Reload the profile before saving.");
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        ProfileEdit edit = new(DisplayName, Description, Pronouns, cid!.Value);
        if (agent.Credentials is not DPoPAccessCredentials credentials)
        {
            return SessionFailure();
        }

        // Check both create and update grants: the PDS requires both for putRecord, even for an existing profile.
        if (!ProfilePermissions.CanUpdate(credentials))
        {
            return await Challenge(owner, edit, credentials);
        }

        // Retain a recovery draft before attempting the write, including when a transport failure leaves its outcome uncertain.
        string id = edits.Add(owner, edit, ProfileEditStatus.Failed,
            "The save was started but its outcome has not been confirmed. Check your profile before retrying.");
        PendingEditId = id;
        return await Save(owner, edit, id, allowChallenge: true);
    }

    /// <summary>
    /// Completes an authorized pending edit once, using an antiforgery-protected POST.
    /// </summary>
    /// <param name="completionId">The single-use completion identifier.</param>
    /// <returns>The save outcome.</returns>
    /// <remarks>
    /// <para>
    /// Edited fields submitted alongside the identifier are ignored. The values come only from the owner's saved draft,
    /// and taking it consumes completion authorization before any network request can begin.
    /// </para>
    /// </remarks>
    public async Task<IActionResult> OnPostComplete(string completionId)
    {
        // The form contains only an opaque identifier, not edited values. BindProperty validation does not apply.
        ModelState.Clear();
        ProfileEditOwner? owner = await Owner();
        // TakeReady atomically removes the Ready state while retaining the draft for recovery, not automatic retry.
        ProfileEdit? edit = owner is not null ? edits.TakeReady(owner, completionId) : null;
        if (owner is null || edit is null)
        {
            ProfileMessage = "The pending save expired, was replaced, or was already used. No save was attempted.";
            return RedirectToPage();
        }

        Apply(edit);
        // A failed upgraded save must not launch another consent loop or replay the already consumed completion.
        return await Save(owner, edit, completionId, allowChallenge: false);
    }

    /// <summary>
    /// Discards this session's pending edit so the latest profile can be loaded.
    /// </summary>
    /// <param name="pendingId">The pending edit identifier.</param>
    /// <returns>The profile editor redirect.</returns>
    /// <remarks>
    /// <para>The owner and identifier must both match, so an older discard form cannot remove a newer draft.</para>
    /// </remarks>
    public async Task<IActionResult> OnPostDiscard(string pendingId)
    {
        ProfileEditOwner? owner = await Owner();
        if (owner is not null)
        {
            edits.Remove(owner, pendingId);
        }

        PendingEditId = null;
        return RedirectToPage();
    }

    /// <summary>
    /// Resolves the authenticated editing session and verifies the injected agent belongs to the same account.
    /// </summary>
    /// <returns>The account/session owner, or <see langword="null"/> when the ticket or agent is unavailable or mismatched.</returns>
    private async Task<ProfileEditOwner?> Owner()
    {
        var session = await HttpContext.AuthenticateAsync(BlueskyAuthenticationDefaults.AuthenticationScheme);
        ProfileEditOwner? owner = CallbackModel.GetOwner(session);

        return owner is not null && agent.IsAuthenticated && agent.Did.Value == owner.Did ? owner : null;
    }

    /// <summary>
    /// Displays a session-binding failure without saving or requesting consent for an unverified owner.
    /// </summary>
    /// <returns>The editor with instructions to establish a new login session.</returns>
    private PageResult SessionFailure()
    {
        ModelState.AddModelError(string.Empty, "This editing session is unavailable. Log out and log in again before saving.");
        return Page();
    }

    /// <summary>
    /// Retains the validated edit and starts a progressive permission request for its verified account.
    /// </summary>
    /// <param name="owner">The authenticated account and editing session.</param>
    /// <param name="edit">The submitted editor values and original profile CID.</param>
    /// <param name="credentials">The current credentials whose effective sample permissions should be retained.</param>
    /// <returns>The consent redirect, or the editor with a recoverable preparation error.</returns>
    private async Task<IActionResult> Challenge(ProfileEditOwner owner, ProfileEdit edit, DPoPAccessCredentials credentials)
    {
        // Persist before discovery or redirect preparation can fail. A new Update intentionally supersedes an older attempt.
        string id = edits.Add(owner, edit);
        PendingEditId = id;
        if (User.GetHandle() is not Handle handle)
        {
            ModelState.AddModelError(string.Empty, "Your account handle could not be verified. No profile was saved. Your edits remain here.");
            return Page();
        }

        try
        {
            Uri redirect = await oauth.Challenge(handle, credentials, id, HttpContext.RequestAborted);
            return Redirect(redirect.ToString());
        }
        catch (Exception exception) when (exception is OAuthException or HttpRequestException or OperationCanceledException)
        {
            ProfileOperationFailed(logger, exception);
            ModelState.AddModelError(string.Empty, "Could not request profile permission. No profile was saved. Your edits remain here; click Update to retry.");
            return Page();
        }
    }

    /// <summary>
    /// Conditionally saves the edited fields while preserving the remaining current profile data.
    /// </summary>
    /// <param name="owner">The authenticated account and editing session.</param>
    /// <param name="edit">The retained editor values and original profile CID.</param>
    /// <param name="id">The draft identifier removed only after a confirmed successful save.</param>
    /// <param name="allowChallenge"><see langword="true"/> to allow an initial missing-scope response to start consent; otherwise, <see langword="false"/>.</param>
    /// <returns>The successful-save redirect, a consent redirect, or the editor with a recovery explanation.</returns>
    /// <remarks>
    /// <para>
    /// The original CID is checked against the latest read and passed as swapRecord to close the race between reading
    /// and writing. Transport errors are not retried because the server might already have committed the write.
    /// </para>
    /// </remarks>
    private async Task<IActionResult> Save(ProfileEditOwner owner, ProfileEdit edit, string id, bool allowChallenge)
    {
        try
        {
            var current = await agent.GetProfile(cancellationToken: HttpContext.RequestAborted);
            if (!current.Succeeded)
            {
                return SaveFailure($"Could not read the profile for saving (HTTP {(int)current.StatusCode}).");
            }

            if (current.Result.Cid.Value != edit.Cid)
            {
                // Preserve the submitted version. Do not turn retry into an overwrite of intervening edits.
                return SaveFailure("The profile changed while you were editing. Copy your edits and reload the profile before saving.");
            }

            // Apply only this editor's fields to the full current record, preserving avatar, banner and other profile data.
            var profile = current.Result.Value;
            profile.DisplayName = edit.DisplayName;
            profile.Description = edit.Description;
            profile.Pronouns = edit.Pronouns;
            var result = await agent.UpdateProfile(profile, cid: new Cid(edit.Cid), cancellationToken: HttpContext.RequestAborted);
            if (!result.Succeeded)
            {
                if (allowChallenge && result.StatusCode == HttpStatusCode.Forbidden &&
                    result.AtErrorDetail is ScopeMissingError or InsufficientScope &&
                    agent.Credentials is DPoPAccessCredentials credentials)
                {
                    // Opaque scope references cannot be fully inspected locally. Only a recognized missing-scope
                    // response permits this fallback, not a generic forbidden response or unrelated server failure.
                    return await Challenge(owner, edit, credentials);
                }

                return SaveFailure($"The profile was not saved (HTTP {(int)result.StatusCode}). Check your profile before retrying.");
            }

            // Identifier matching prevents an older in-flight save from removing a newer draft submitted in another tab.
            edits.Remove(owner, id);
            PendingEditId = null;
            ProfileMessage = "Your profile was saved.";
            return RedirectToPage();
        }
        catch (Exception exception) when (exception is HttpRequestException or OperationCanceledException)
        {
            ProfileOperationFailed(logger, exception);
            return SaveFailure("The save could not be confirmed. Check your profile before retrying; your edits remain here.");
        }
    }

    /// <summary>
    /// Renders a save failure with the edited values still present and the recovery draft retained.
    /// </summary>
    /// <param name="message">The explanation of the rejected or uncertain write.</param>
    /// <returns>The editor containing a model-level error.</returns>
    private PageResult SaveFailure(string message)
    {
        ModelState.AddModelError(string.Empty, message);
        return Page();
    }

    /// <summary>
    /// Restores the editor fields and original concurrency version from a profile read or retained draft.
    /// </summary>
    /// <param name="edit">The values and CID to display or save.</param>
    private void Apply(ProfileEdit edit)
    {
        DisplayName = edit.DisplayName;
        Description = edit.Description;
        Pronouns = edit.Pronouns;
        Cid = new Cid(edit.Cid);
    }

    /// <summary>
    /// Logs a profile or consent transport failure without exposing exception details in the editor.
    /// </summary>
    /// <param name="logger">The diagnostic logger.</param>
    /// <param name="exception">The operation failure.</param>
    [LoggerMessage(Level = LogLevel.Warning, Message = "Profile operation failed.")]
    private static partial void ProfileOperationFailed(ILogger logger, Exception exception);
}
