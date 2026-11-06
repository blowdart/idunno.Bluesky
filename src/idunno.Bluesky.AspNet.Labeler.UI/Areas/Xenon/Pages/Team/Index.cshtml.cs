// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;

using idunno.AtProto;
using idunno.Bluesky.Labeler;
using idunno.Bluesky.Labeler.Administration;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace idunno.Bluesky.AspNet.Labeler.UI.Areas.Xenon.Pages.Team;

/// <summary>Displays members and submits audited enrollment, role and membership changes.</summary>
public sealed class IndexModel : PageModel
{
    /// <summary>Gets the current team response.</summary>
    internal JsonElement Data { get; private set; }

    /// <summary>Gets counted membership navigation and canonical identity ordering.</summary>
    internal TableNavigation? Table { get; private set; }

    /// <summary>Gets all registered members independently of the table's search and pagination.</summary>
    internal AdministrationMemberView[] MemberChoices { get; private set; } = [];

    /// <summary>Gets the canonical DID selected from the current team list.</summary>
    internal string? SelectedMember { get; private set; }

    /// <summary>Gets the selected member's current persisted role.</summary>
    internal LabelTeamRole SelectedRole { get; private set; } = LabelTeamRole.Viewer;

    /// <summary>Gets whether the current administrator can manage policy-alert email settings.</summary>
    internal bool CanManagePolicyEmail { get; private set; }

    /// <summary>Gets the current administrator's registered policy-alert email.</summary>
    internal string? CurrentNotificationEmail { get; private set; }

    /// <summary>Gets the configured remote-administration control mode.</summary>
    internal RemoteAdministrationMode RemoteAdministrationControlMode { get; private set; }

    /// <summary>Gets the currently effective remote-administration state, when available.</summary>
    internal bool? RemoteAdministrationEnabled { get; private set; }

    /// <summary>Gets whether the current browser identity is the current labeler owner.</summary>
    internal bool IsRemoteAdministrationOwner { get; private set; }

    /// <summary>Gets whether the current owner can control the Xenon-managed switch.</summary>
    internal bool CanControlRemoteAdministration { get; private set; }

    /// <summary>Loads active members and retained membership tombstones.</summary>
    public async Task OnGetAsync()
    {
        if (TempData["NotificationEmailError"] is string emailError)
        {
            ViewData["Error"] = emailError;
        }
        IOptionsMonitor<LabelerAdministrationOptions>? administrationOptions =
            HttpContext.RequestServices.GetService<IOptionsMonitor<LabelerAdministrationOptions>>();
        LabelerAdministrationOptions currentOptions = administrationOptions?.CurrentValue ?? new();
        RemoteAdministrationControlMode = currentOptions.RemoteMode ??
            (currentOptions.RemoteEnabled ? RemoteAdministrationMode.Enabled : RemoteAdministrationMode.Disabled);
        RemoteAdministrationEnabled = RemoteAdministrationControlMode switch
        {
            RemoteAdministrationMode.Disabled => false,
            RemoteAdministrationMode.Enabled => true,
            RemoteAdministrationMode.WebUiControlled => null,
            _ => false
        };
        LabelServiceResult<LabelRemoteAdministrationStatus> remoteStatus = await HttpContext.RequestServices
            .GetRequiredService<LabelerUiAdministration>()
            .GetRemoteAdministrationStatusAsync(User, HttpContext.RequestAborted).ConfigureAwait(false);
        if (remoteStatus.Succeeded)
        {
            IsRemoteAdministrationOwner = remoteStatus.Result.IsOwner;
            if (RemoteAdministrationControlMode == RemoteAdministrationMode.WebUiControlled)
            {
                RemoteAdministrationEnabled = remoteStatus.Result.Enabled;
            }
            CanControlRemoteAdministration = IsRemoteAdministrationOwner &&
                ViewData["CurrentRole"] is LabelTeamRole.Administrator &&
                RemoteAdministrationControlMode == RemoteAdministrationMode.WebUiControlled;
        }
        else
        {
            ViewData["RemoteAdministrationError"] = remoteStatus.Error.Message;
        }

        LabelerModerationOptions? moderationOptions = HttpContext.RequestServices.GetService<LabelerModerationOptions>();
        CanManagePolicyEmail = ViewData["CurrentRole"] is LabelTeamRole.Administrator &&
            moderationOptions?.PolicyEmail is { Enabled: true, Server: { Length: > 0 } };
        await LoadMemberChoicesAsync().ConfigureAwait(false);
        string? actor = User.FindFirst(idunno.AtProto.Authentication.AtProtoClaims.Did)?.Value;
        CurrentNotificationEmail = MemberChoices.FirstOrDefault(member => member.Did == actor)?.NotificationEmail;
        string selected = Request.Query["member"].ToString();
        if (MemberChoices.FirstOrDefault(member => member.Did == selected) is { } selectedMember)
        {
            SelectedMember = selectedMember.Did;
            SelectedRole = selectedMember.Role;
        }

        Table = TableNavigation.Read(Request, new(LabelTableKind.Team, Sort: LabelTableSort.Subject, Ascending: true));
        if (Table is null)
        {
            ViewData["Error"] = "Choose an available sort order and page.";
            return;
        }

        string search = Request.Query["Search"].ToString().Trim();
        if (search.Length > 256)
        {
            ViewData["Error"] = "Enter a DID or handle of at most 256 characters.";
            return;
        }

        if (search.Length > 0)
        {
            AdministrationResponse resolved = await HttpContext.RequestServices.GetRequiredService<LabelerUiAdministration>()
                .ResolveTableIdentityAsync(
                    principal: User,
                    actor: search,
                    kind: LabelTableKind.Team,
                    token: HttpContext.RequestAborted).ConfigureAwait(false);
            if (!resolved.Succeeded)
            {
                ViewData["Error"] = resolved.Message ?? "The team member handle could not be resolved.";
                return;
            }

            Table = new(Table.Query with { TeamMember = resolved.Target }, Request);
        }

        Data = await UiPageHelpers.ReadTableAsync(this, Table).ConfigureAwait(false);
    }

    /// <summary>Enrolls an identity after the shared service verifies its DID document and handle, if supplied.</summary>
    /// <returns>An action result that displays the outcome of the enrollment.</returns>
    public async Task<IActionResult> OnPostEnrollAsync()
    {
        string target = await UiPageHelpers.FormValueAsync(this, "target", 256).ConfigureAwait(false);
        string roleText = await UiPageHelpers.FormValueAsync(this, "role", 32).ConfigureAwait(false);
        string reason = await UiPageHelpers.FormValueAsync(this, "reason", 4096).ConfigureAwait(false);
        string operationId = await UiPageHelpers.FormValueAsync(this, "operationId", 256).ConfigureAwait(false);
        if (!Enum.TryParse(roleText, ignoreCase: false, out LabelTeamRole role) || !Enum.IsDefined(role))
        {
            ViewData["Error"] = "Choose a valid team role.";
            await OnGetAsync().ConfigureAwait(false);
            return Page();
        }

        AdministrationResponse response = await UiPageHelpers.DispatchAsync(
            this,
            new AdministrationCommand(
                Method: "enroll",
                OperationId: operationId,
                Reason: reason,
                Target: target,
                Role: role)).ConfigureAwait(false);
        if (response.Succeeded)
        {
            TempData["Status"] = $"Enrollment operation {operationId} completed.";
            return RedirectToPage();
        }

        await OnGetAsync().ConfigureAwait(false);
        return Page();
    }

    /// <summary>Applies an audited provider-backed membership change on submission.</summary>
    /// <returns>An action result that displays the outcome of the membership change.</returns>
    public async Task<IActionResult> OnPostChangeAsync()
    {
        string target = await UiPageHelpers.FormValueAsync(this, "target", 256).ConfigureAwait(false);
        string actionText = await UiPageHelpers.FormValueAsync(this, "action", 32).ConfigureAwait(false);
        string roleText = await UiPageHelpers.FormValueAsync(this, "role", 32).ConfigureAwait(false);
        string reason = await UiPageHelpers.FormValueAsync(this, "reason", 4096).ConfigureAwait(false);
        string operationId = await UiPageHelpers.FormValueAsync(this, "operationId", 256).ConfigureAwait(false);
        if (!Enum.TryParse(actionText, ignoreCase: false, out LabelTeamAction action) ||
            !Enum.IsDefined(action) || action is LabelTeamAction.Enroll or LabelTeamAction.BindLocal or LabelTeamAction.RevokeLocal ||
            !Enum.TryParse(roleText, ignoreCase: false, out LabelTeamRole role) || !Enum.IsDefined(role))
        {
            ViewData["Error"] = "Choose an available team action and, when changing permissions, a valid role.";
            await OnGetAsync().ConfigureAwait(false);
            return Page();
        }

        await LoadMemberChoicesAsync().ConfigureAwait(false);
        if (action == LabelTeamAction.TransferOwnership &&
            await UiPageHelpers.FormValueAsync(this, "confirmTransfer", 8).ConfigureAwait(false) != "true")
        {
            ViewData["Error"] = "Confirm that you want to transfer labeler ownership.";
            await OnGetAsync().ConfigureAwait(false);
            return Page();
        }

        IDidHandleResolver handles = HttpContext.RequestServices.GetRequiredService<IDidHandleResolver>();
        AdministrationMemberView? member = MemberChoices.FirstOrDefault(candidate =>
            handles.TryGetCachedHandle(new Did(candidate.Did), out Handle? handle) &&
            handle is { IsValid: true } && string.Equals(handle.Value, target.TrimStart('@'), StringComparison.OrdinalIgnoreCase));
        if (member is null)
        {
            ViewData["Error"] = "Choose a registered team member with a verified handle.";
            await OnGetAsync().ConfigureAwait(false);
            return Page();
        }

        AdministrationResponse response = await UiPageHelpers.DispatchAsync(
            this,
            new AdministrationCommand(
                Method: "changeTeam",
                OperationId: operationId,
                Reason: reason,
                Target: member.Did,
                Role: role,
                Action: action)).ConfigureAwait(false);
        if (response.Succeeded)
        {
            TempData["Status"] = $"Membership operation {operationId} completed.";
            return RedirectToPage();
        }

        await OnGetAsync().ConfigureAwait(false);
        return Page();
    }

    /// <summary>Loads the already authorized full team and verifies handles independently of the visible table page.</summary>
    private async Task LoadMemberChoicesAsync()
    {
        MemberChoices = ViewData["TeamMembers"] as AdministrationMemberView[] ?? [];
        IDidHandleResolver handles = HttpContext.RequestServices.GetRequiredService<IDidHandleResolver>();
        await Parallel.ForEachAsync(
            source: MemberChoices,
            parallelOptions: new ParallelOptions { MaxDegreeOfParallelism = 8, CancellationToken = HttpContext.RequestAborted },
            body: async (member, token) => await handles.ResolveHandleAsync(new Did(member.Did), token).ConfigureAwait(false)).ConfigureAwait(false);
        MemberChoices = [.. MemberChoices.OrderBy(
            member => handles.TryGetCachedHandle(new Did(member.Did), out Handle? handle) && handle.IsValid ? handle.Value : null,
            StringComparer.OrdinalIgnoreCase)];
    }

    /// <summary>Registers or clears the current administrator's policy-verification alert address.</summary>
    /// <returns>An action result that displays the outcome of the update.</returns>
    public async Task<IActionResult> OnPostNotificationEmailAsync()
    {
        LabelerModerationOptions? moderationOptions = HttpContext.RequestServices.GetService<LabelerModerationOptions>();
        if (ViewData["CurrentRole"] is not LabelTeamRole.Administrator ||
            moderationOptions?.PolicyEmail is not { Enabled: true, Server: { Length: > 0 } })
        {
            return Forbid();
        }

        string? actor = User.FindFirst(idunno.AtProto.Authentication.AtProtoClaims.Did)?.Value;
        if (actor is null || !idunno.AtProto.Did.TryParse(actor, out idunno.AtProto.Did? did))
        {
            return Forbid();
        }

        string email = await UiPageHelpers.FormValueAsync(this, "notificationEmail", 254).ConfigureAwait(false);
        string operationId = await UiPageHelpers.FormValueAsync(this, "operationId", 256).ConfigureAwait(false);
        AdministrationResponse response = await UiPageHelpers.DispatchAsync(
            this,
            new AdministrationCommand(
                Method: "setTeamNotificationEmail",
                OperationId: operationId,
                Reason: "Update policy alert email.",
                Target: did.Value,
                NotificationEmail: email.Length == 0 ? null : email)).ConfigureAwait(false);
        if (response.Succeeded)
        {
            TempData["Status"] = email.Length == 0
                ? "Administrator notification email cleared."
                : "Administrator notification email updated.";
            if (await UiPageHelpers.ConfirmedFieldAsync(this, "returnToPreferences").ConfigureAwait(false))
            {
                return RedirectToPage("/Preferences");
            }
            return RedirectToPage();
        }

        if (await UiPageHelpers.ConfirmedFieldAsync(this, "returnToPreferences").ConfigureAwait(false))
        {
            TempData["NotificationEmailError"] = response.Message ?? response.Error ?? "The email address could not be updated.";
            return RedirectToPage("/Preferences");
        }
        await OnGetAsync().ConfigureAwait(false);
        return Page();
    }

    /// <summary>Sends a test only to the current active administrator's persisted notification address.</summary>
    /// <returns>The originating settings page with an explicit delivery outcome.</returns>
    public async Task<IActionResult> OnPostTestEmailAsync()
    {
        LabelerModerationOptions? options = HttpContext.RequestServices.GetService<LabelerModerationOptions>();
        if (ViewData["CurrentRole"] is not LabelTeamRole.Administrator ||
            options?.PolicyEmail is not { Enabled: true, Server: { Length: > 0 } })
        {
            return Forbid();
        }
        AdministrationResponse team = await UiPageHelpers.DispatchAsync(
            this,
            new AdministrationCommand(Method: "getTeam", Reason: "Test own notification email.")).ConfigureAwait(false);
        string? actor = User.FindFirst(idunno.AtProto.Authentication.AtProtoClaims.Did)?.Value;
        AdministrationMemberView? member = team.Members?.FirstOrDefault(member => member.Did == actor);
        if (!team.Succeeded || member is not { Active: true, Removed: false, Role: LabelTeamRole.Administrator })
        {
            return Forbid();
        }
        if (member.NotificationEmail is not { } address)
        {
            TempData["NotificationEmailError"] = "Save your notification email address before sending a test.";
        }
        else
        {
            bool sent = await HttpContext.RequestServices.GetRequiredService<LabelerPolicyEmailService>()
                .SendTestAsync(address, HttpContext.RequestAborted).ConfigureAwait(false);
            TempData[sent ? "Status" : "NotificationEmailError"] = sent
                ? "Test email accepted by the SMTP server. Check your inbox and spam folder."
                : "Test email was not sent. Wait 30 seconds before trying again, and check the server email configuration and logs.";
        }
        if (await UiPageHelpers.ConfirmedFieldAsync(this, "returnToPreferences").ConfigureAwait(false))
        {
            return RedirectToPage("/Preferences");
        }
        return RedirectToPage();
    }

    /// <summary>Changes the web-managed remote-administration switch under durable owner authorization.</summary>
    /// <returns>An action result that displays the outcome of the switch change.</returns>
    public async Task<IActionResult> OnPostRemoteAdministrationAsync()
    {
        IOptionsMonitor<LabelerAdministrationOptions>? options =
            HttpContext.RequestServices.GetService<IOptionsMonitor<LabelerAdministrationOptions>>();
        LabelerAdministrationOptions currentOptions = options?.CurrentValue ?? new();
        RemoteAdministrationMode mode = currentOptions.RemoteMode ??
            (currentOptions.RemoteEnabled ? RemoteAdministrationMode.Enabled : RemoteAdministrationMode.Disabled);
        string enabledText = await UiPageHelpers.FormValueAsync(this, "enabled", 5).ConfigureAwait(false);
        string operationId = await UiPageHelpers.FormValueAsync(this, "operationId", 256).ConfigureAwait(false);
        string reason = await UiPageHelpers.FormValueAsync(this, "reason", 4096).ConfigureAwait(false);
        if (mode != RemoteAdministrationMode.WebUiControlled ||
            ViewData["CurrentRole"] is not LabelTeamRole.Administrator ||
            !bool.TryParse(enabledText, out bool enabled))
        {
            ViewData["Error"] = "Only the labeler owner can change the switch while WebUiControlled mode is configured.";
            await OnGetAsync().ConfigureAwait(false);
            return Page();
        }

        LabelServiceResult<LabelAdministrationReceipt> result;
        try
        {
            result = await HttpContext.RequestServices
                .GetRequiredService<LabelerUiAdministration>()
                .SetRemoteAdministrationEnabledAsync(
                    principal: User,
                    operationId: operationId,
                    enabled: enabled,
                    reason: reason,
                    cancellationToken: HttpContext.RequestAborted).ConfigureAwait(false);
        }
        catch (ArgumentException exception)
        {
            ViewData["Error"] = exception.Message;
            await OnGetAsync().ConfigureAwait(false);
            return Page();
        }

        if (result.Succeeded)
        {
            TempData["Status"] = enabled
                ? "Remote CLI administration enabled."
                : "Remote CLI administration disabled.";
            return RedirectToPage();
        }

        ViewData["Error"] = result.Error.Message;
        await OnGetAsync().ConfigureAwait(false);
        return Page();
    }
}
