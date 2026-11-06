// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.Bluesky.Labeler;
using idunno.Bluesky.Labeler.Administration;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.DependencyInjection;

namespace idunno.Bluesky.AspNet.Labeler.UI.Areas.Xenon.Pages;

/// <summary>Manages the authenticated moderator's private media display preferences.</summary>
public sealed class PreferencesModel : PageModel
{
    /// <summary>Gets the fixed set of configurable Bluesky media labels.</summary>
    internal static IReadOnlyList<string> Labels { get; } = ["graphic-media", "porn", "nudity", "sexual", "self-harm"];

    /// <summary>Gets the caller's current display choices, defaulting to blur for a previously unconfigured account.</summary>
    internal LabelMediaPreferences Preferences { get; private set; } = new();

    /// <summary>Gets whether configured email notifications are available to the signed-in administrator.</summary>
    internal bool CanManagePolicyEmail { get; private set; }

    /// <summary>Gets the signed-in administrator's registered notification address.</summary>
    internal string? CurrentNotificationEmail { get; private set; }

    /// <summary>Loads the signed-in member's preferences without accepting another member's identity.</summary>
    public async Task OnGetAsync()
    {
        if (TempData["NotificationEmailError"] is string emailError)
        {
            ViewData["Error"] = emailError;
        }
        Response.Headers.CacheControl = "private, no-store";
        LabelServiceResult<LabelMediaPreferences> result = await HttpContext.RequestServices
            .GetRequiredService<LabelerUiAdministration>()
            .MediaPreferencesAsync(User, null, HttpContext.RequestAborted).ConfigureAwait(false);
        if (result.Succeeded)
        {
            Preferences = result.Result;
        }
        else
        {
            ViewData["Error"] = result.Error.Message;
        }
        LabelerModerationOptions? options = HttpContext.RequestServices.GetService<LabelerModerationOptions>();
        CanManagePolicyEmail = ViewData["CurrentRole"] is LabelTeamRole.Administrator &&
            options?.PolicyEmail is { Enabled: true, Server.Length: > 0 };
        if (CanManagePolicyEmail)
        {
            AdministrationResponse team = await UiPageHelpers.DispatchAsync(
                this,
                new AdministrationCommand(Method: "getTeam", Reason: "Inspect own notification email.")).ConfigureAwait(false);
            if (!team.Succeeded)
            {
                ViewData["Error"] = team.Message;
                return;
            }
            string? actor = User.FindFirst(idunno.AtProto.Authentication.AtProtoClaims.Did)?.Value;
            CurrentNotificationEmail = team.Members?.FirstOrDefault(member => member.Did == actor)?.NotificationEmail;
        }
    }

    /// <summary>Saves personal filters after antiforgery and live team authorization checks.</summary>
    /// <param name="blur">The labels whose media should be blurred.</param>
    /// <param name="grayscale">The labels whose media should be displayed in grayscale.</param>
    /// <param name="translucent">The labels whose media should be translucent.</param>
    /// <returns>The redisplayed validation error or a redirect after saving.</returns>
    public async Task<IActionResult> OnPostAsync(string[]? blur, string[]? grayscale, string[]? translucent)
    {
        Response.Headers.CacheControl = "private, no-store";
        blur ??= [];
        grayscale ??= [];
        translucent ??= [];
        if (!ModelState.IsValid || blur.Concat(grayscale).Concat(translucent).Any(label => !Labels.Contains(label)))
        {
            ModelState.AddModelError(string.Empty, "Choose only the available media labels and filters.");
            await OnGetAsync().ConfigureAwait(false);
            return Page();
        }
        LabelMediaFilter Filter(string label) =>
            (blur.Contains(label) ? LabelMediaFilter.Blur : LabelMediaFilter.None) |
            (grayscale.Contains(label) ? LabelMediaFilter.Grayscale : LabelMediaFilter.None) |
            (translucent.Contains(label) ? LabelMediaFilter.Translucent : LabelMediaFilter.None);
        Preferences = new(
            GraphicMedia: Filter("graphic-media"),
            Porn: Filter("porn"),
            Nudity: Filter("nudity"),
            Sexual: Filter("sexual"),
            SelfHarm: Filter("self-harm"));
        LabelServiceResult<LabelMediaPreferences> result = await HttpContext.RequestServices
            .GetRequiredService<LabelerUiAdministration>()
            .MediaPreferencesAsync(User, Preferences, HttpContext.RequestAborted).ConfigureAwait(false);
        if (!result.Succeeded)
        {
            ViewData["Error"] = result.Error.Message;
            return Page();
        }
        TempData["Status"] = "Your media preferences were saved.";
        return RedirectToPage();
    }
}
