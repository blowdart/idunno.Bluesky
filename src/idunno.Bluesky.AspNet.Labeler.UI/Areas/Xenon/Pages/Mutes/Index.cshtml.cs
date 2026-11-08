// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Globalization;

using idunno.Bluesky.Labeler.Administration;
using idunno.Bluesky.Labeler.Moderation;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace idunno.Bluesky.AspNet.Labeler.UI.Areas.Xenon.Pages.Mutes;

/// <summary>Displays shared active mutes and submits confirmed, audited mute changes.</summary>
public sealed class IndexModel : PageModel
{
    /// <summary>Gets the active shared mutes on the current bounded page.</summary>
    internal LabelMuteEntry[] Mutes { get; private set; } = [];

    /// <summary>Gets whether the provider successfully loaded the current page.</summary>
    internal bool Loaded { get; private set; }

    /// <summary>Gets whether the current team role permits mute changes.</summary>
    internal bool CanChange => ViewData["CurrentRole"] is LabelTeamRole.Moderator or LabelTeamRole.Administrator;

    /// <summary>Gets the event cursor used to load this page.</summary>
    internal long AfterMuteEventId { get; private set; }

    /// <summary>Gets the target selected for the create form.</summary>
    internal string Subject { get; private set; } = string.Empty;

    /// <summary>Gets the mute kind selected for the create form.</summary>
    internal LabelMuteKind Kind { get; private set; }

    /// <summary>Gets the finite duration selected for the create form.</summary>
    internal int DurationHours { get; private set; } = 24;

    /// <summary>Gets the optional reason retained after an unsuccessful create submission.</summary>
    internal string Reason { get; private set; } = string.Empty;

    /// <summary>Gets the stable operation identifier retained while retrying a create intent.</summary>
    internal string CreateOperationId { get; private set; } = Guid.NewGuid().ToString("N");

    /// <summary>Gets the event whose unmute submission did not complete.</summary>
    internal long? RetryUnmuteEventId { get; private set; }

    /// <summary>Gets the stable operation identifier for an unsuccessful unmute submission.</summary>
    internal string? RetryUnmuteOperationId { get; private set; }

    /// <summary>Gets the optional reason for an unsuccessful unmute submission.</summary>
    internal string RetryUnmuteReason { get; private set; } = string.Empty;

    /// <summary>Loads one page of active mutes under live provider inspection authority.</summary>
    /// <param name="afterMuteEventId">The last event on the preceding page, or zero for the first page.</param>
    /// <param name="subject">The optional subject to prefill without changing any mute.</param>
    /// <param name="kind">One of the mute kinds to preselect.</param>
    /// <returns>A task representing the page load.</returns>
    public async Task OnGetAsync(long afterMuteEventId = 0, string? subject = null, string kind = "Subject")
    {
        if (!ModelState.IsValid || afterMuteEventId < 0 ||
            !Enum.TryParse(kind, ignoreCase: false, out LabelMuteKind muteKind) || !Enum.IsDefined(muteKind) ||
            subject?.Length > 2048)
        {
            ViewData["Error"] = "Choose a valid mute page, kind and subject.";
            return;
        }

        AfterMuteEventId = afterMuteEventId;
        Subject = subject ?? string.Empty;
        Kind = muteKind;
        await LoadAsync().ConfigureAwait(false);
    }

    /// <summary>Creates or replaces a finite mute only after explicit confirmation.</summary>
    /// <returns>An action result displaying the outcome or the unchanged submission for retry.</returns>
    public async Task<IActionResult> OnPostMuteAsync() => await ChangeAsync(unmute: false).ConfigureAwait(false);

    /// <summary>Removes only the exact active mute the moderator confirmed.</summary>
    /// <returns>An action result displaying the outcome or an explicit concurrency error.</returns>
    public async Task<IActionResult> OnPostUnmuteAsync() => await ChangeAsync(unmute: true).ConfigureAwait(false);

    /// <summary>Validates browser intent before dispatching a provider-authorized and concurrency-fenced change.</summary>
    private async Task<IActionResult> ChangeAsync(bool unmute)
    {
        if (!CanChange)
        {
            return StatusCode(StatusCodes.Status403Forbidden);
        }

        IFormCollection form = await Request.ReadFormAsync(HttpContext.RequestAborted).ConfigureAwait(false);
        string subject = form["subject"].ToString().Trim();
        string operationId = form["operationId"].ToString().Trim();
        string reason = form["reason"].ToString().Trim();
        bool validKind = Enum.TryParse(form["kind"].ToString(), ignoreCase: false, out LabelMuteKind kind) && Enum.IsDefined(kind);
        bool validDuration = int.TryParse(form["durationHours"], NumberStyles.None, CultureInfo.InvariantCulture, out int duration) &&
            MuteDurations.Hours.Contains(duration);
        bool validEvent = long.TryParse(form["expectedMuteEventId"], NumberStyles.None, CultureInfo.InvariantCulture, out long eventId) && eventId > 0;
        if (unmute)
        {
            RetryUnmuteEventId = validEvent ? eventId : null;
            RetryUnmuteOperationId = operationId;
            RetryUnmuteReason = reason;
        }
        else
        {
            Subject = subject;
            Kind = kind;
            DurationHours = duration;
            Reason = reason;
            CreateOperationId = operationId;
        }

        if (!ModelState.IsValid || form["confirmed"] != "true" || !validKind ||
            subject.Length is 0 or > 2048 || operationId.Length is 0 or > 256 || reason.Length > 4096 ||
            (unmute ? !validEvent : !validDuration))
        {
            ViewData["Error"] = unmute
                ? "Confirm the unmute with its current event identifier, subject, kind and operation identifier. Reasons must be at most 4096 characters."
                : "Confirm the mute, choose a supported duration and kind, and provide a subject and operation identifier. Reasons must be at most 4096 characters.";
            await LoadAsync().ConfigureAwait(false);
            return Page();
        }

        AdministrationResponse response = await UiPageHelpers.DispatchAsync(
            this,
            new AdministrationCommand(
                Method: "changeMute",
                OperationId: operationId,
                Reason: reason.Length == 0 ? "No reason provided." : reason,
                Moderation: new(
                    MuteSubject: subject,
                    MuteKind: kind,
                    MuteDurationHours: unmute ? null : duration,
                    ExpectedMuteEventId: unmute ? eventId : null))).ConfigureAwait(false);
        if (response.Succeeded)
        {
            TempData["Status"] = $"{(unmute ? "Unmute" : "Mute")} operation {operationId} completed.";
            return RedirectToPage();
        }

        await LoadAsync().ConfigureAwait(false);
        return Page();
    }

    /// <summary>Loads only provider-active entries, preserving any preceding mutation error.</summary>
    private async Task LoadAsync()
    {
        object? error = ViewData["Error"];
        AdministrationResponse response = await UiPageHelpers.DispatchAsync(
            this,
            new AdministrationCommand(
                Method: "getMutes",
                Reason: "Xenon shared mute inspection.",
                Limit: 16,
                Moderation: new(AfterMuteEventId: AfterMuteEventId))).ConfigureAwait(false);
        Loaded = response.Succeeded;
        Mutes = response.Succeeded ? response.Moderation?.Mutes ?? [] : [];
        if (error is not null)
        {
            ViewData["Error"] = error;
        }
    }
}
