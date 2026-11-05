// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

using idunno.AtProto.Authentication;

using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.DependencyInjection;

namespace idunno.Bluesky.AspNet.Labeler.UI.Areas.Xenon.Pages.Labels;

/// <summary>Issues or negates labels for accounts and exact AT Protocol records independently of cases.</summary>
public sealed class IndexModel : PageModel
{
    internal string TargetKind { get; private set; } = "Account";
    internal string Target { get; private set; } = string.Empty;
    internal string Cid { get; private set; } = string.Empty;
    internal string Reason { get; private set; } = string.Empty;
    internal ManualLabelFormDisplay Labels { get; private set; } = new([], null);

    /// <summary>Gets the new-label duration, frozen together with retry selections.</summary>
    internal LabelDurationDisplay Duration { get; private set; } = new();

    /// <summary>Displays the available policy values before a target has been loaded.</summary>
    /// <returns>A task that loads the available label choices.</returns>
    public async Task OnGetAsync()
    {
        AdministrationResponse response = await UiPageHelpers.DispatchAsync(
            this, new AdministrationCommand(Method: "getManualLabelChoices")).ConfigureAwait(false);
        Labels = new(response.Moderation?.LabelChoices ?? [], null);
    }

    /// <summary>Loads issuer-local target labels for a background or explicit antiforgery-protected lookup.</summary>
    /// <returns>The refreshed label controls or the complete page for browsers without scripting.</returns>
    public async Task<IActionResult> OnPostLookupAsync()
    {
        await ReadTargetAsync().ConfigureAwait(false);
        await LoadLabelsAsync().ConfigureAwait(false);
        return Request.Headers.XRequestedWith == "XMLHttpRequest"
            ? Partial("_ManualLabelChoices", Labels with { Error = ViewData["Error"] as string })
            : Page();
    }

    /// <summary>Fills the optional content version only after an explicit authorized request, without issuing labels.</summary>
    /// <returns>The refreshed label controls or complete page, with the current CID or an explicit lookup error.</returns>
    public async Task<IActionResult> OnPostCurrentCidAsync()
    {
        await ReadTargetAsync().ConfigureAwait(false);
        string previousCid = Cid;
        Cid = string.Empty;
        await LoadLabelsAsync().ConfigureAwait(false);
        if (Labels.State is not null && TargetKind == "Record")
        {
            (string? cid, string? error) = await HttpContext.RequestServices.GetRequiredService<SubjectPreviewService>()
                .LookupRecordCidAsync(Target, HttpContext.RequestAborted).ConfigureAwait(false);
            if (cid is not null)
            {
                Cid = cid;
                await LoadLabelsAsync().ConfigureAwait(false);
            }
            else
            {
                ViewData["Error"] = error;
                Labels = Labels with { State = null };
            }
        }
        else if (TargetKind != "Record")
        {
            ViewData["Error"] = "Enter a content address to look up its current version.";
            Labels = Labels with { State = null };
        }
        if (Labels.State is null && TargetKind == "Record")
        {
            Cid = previousCid;
            Labels = Labels with { Cid = Cid };
        }
        return Request.Headers.XRequestedWith == "XMLHttpRequest"
            ? Partial("_ManualLabelChoices", Labels with { Error = ViewData["Error"] as string })
            : Page();
    }

    /// <summary>Submits only staged changes through shared issuance, preserving immutable per-label retries on partial failure.</summary>
    /// <returns>A page showing the completed changes or an explicit recoverable failure.</returns>
    public async Task<IActionResult> OnPostAsync()
    {
        await ReadTargetAsync().ConfigureAwait(false);
        string state = await UiPageHelpers.FormValueAsync(this, "labelState", 1_048_576).ConfigureAwait(false);
        ManualLabelForm? snapshot;
        try
        {
            snapshot = state.Length == 0 ? null : JsonSerializer.Deserialize(
                Protector().Unprotect(state), ManualLabelFormContext.Default.ManualLabelForm);
        }
        catch (Exception exception) when (exception is CryptographicException or JsonException)
        {
            snapshot = null;
        }

        if (snapshot is null || snapshot.Actor != User.FindFirst(AtProtoClaims.Did)?.Value ||
            snapshot.InputTarget != Target || snapshot.TargetKind != TargetKind || snapshot.Cid != Cid)
        {
            ViewData["Error"] = "Load the target's labels before submitting changes.";
            return Page();
        }

        Microsoft.AspNetCore.Http.IFormCollection form = await Request.ReadFormAsync(HttpContext.RequestAborted).ConfigureAwait(false);
        int hours = snapshot.DurationHours;
        if (snapshot.Selected is null && !LabelDuration.TryRead(form, out hours))
        {
            ViewData["Error"] = "Choose an available new label duration.";
            Labels = new(snapshot.Choices, state);
            return Page();
        }
        Duration = new(hours, snapshot.Selected is not null);
        DateTimeOffset? expiresAt = snapshot.ExpiresAt;
        if (snapshot.Selected is null && hours != 0)
        {
            expiresAt = HttpContext.RequestServices.GetRequiredService<TimeProvider>().GetUtcNow().AddHours(hours);
        }
        string[] selected = snapshot.Selected ?? [.. form["selectedLabels"].Select(value => value ?? string.Empty).Distinct(StringComparer.Ordinal)];
        Reason = snapshot.Reason ?? await UiPageHelpers.FormValueAsync(this, "reason", 4096).ConfigureAwait(false);
        Labels = new(
            Choices: snapshot.Choices,
            State: state,
            Selected: snapshot.Selected,
            Retrying: snapshot.Selected is not null);
        if (selected.Any(value => !snapshot.Choices.Any(choice => choice.Value == value && !choice.OtherVersion)) ||
            string.IsNullOrWhiteSpace(Reason))
        {
            ViewData["Error"] = "Choose from the available labels and enter a reason for your changes.";
            Labels = Labels with { Selected = selected };
            return Page();
        }

        ModerationLabelChoiceView[] changes = [.. snapshot.Choices.Where(choice => !choice.OtherVersion && choice.Applied != selected.Contains(choice.Value, StringComparer.Ordinal))];
        if (changes.Length == 0)
        {
            ViewData["Error"] = "No label changes have been selected.";
            return Page();
        }

        snapshot = snapshot with { Selected = selected, Reason = Reason, DurationHours = hours, ExpiresAt = expiresAt };
        Duration = new(hours, true);
        Labels = new(
            Choices: snapshot.Choices,
            State: Protect(snapshot),
            Selected: selected,
            Retrying: true);
        int completed = 0;
        foreach (ModerationLabelChoiceView choice in changes)
        {
            AdministrationResponse response = await UiPageHelpers.DispatchAsync(
                this,
                new AdministrationCommand(
                    Method: choice.Applied ? "negateLabel" : "issueLabel",
                    OperationId: snapshot.OperationId + "-" + completed.ToString(CultureInfo.InvariantCulture),
                    Reason: Reason,
                    Target: snapshot.Target,
                    Value: choice.Value,
                    Cid: choice.UriWide || Cid.Length == 0 ? null : Cid,
                    ExpiresAt: choice.Applied ? null : snapshot.ExpiresAt,
                    ExpectedLabelSequence: choice.Sequence)).ConfigureAwait(false);
            if (!response.Succeeded)
            {
                ViewData["Status"] = null;
                ViewData["Error"] = $"{completed} of {changes.Length} label changes are confirmed. {response.Message ?? response.Error} Retry without changing your selections to recover the original results, or load the labels again to review their current state.";
                return Page();
            }

            completed++;
        }

        ViewData["Status"] = $"{completed} label {(completed == 1 ? "change" : "changes")} completed.";
        Reason = string.Empty;
        Duration = new();
        await LoadLabelsAsync().ConfigureAwait(false);
        return Page();
    }

    /// <summary>Retains bounded target fields on validation failures and explicit no-script lookups.</summary>
    private async Task ReadTargetAsync()
    {
        Target = ManualLabelTarget.Normalize(await UiPageHelpers.FormValueAsync(this, "target", 2048).ConfigureAwait(false));
        TargetKind = Target.StartsWith("at://", StringComparison.Ordinal) ? "Record" : "Account";
        Cid = TargetKind == "Record"
            ? await UiPageHelpers.FormValueAsync(this, "cid", 128).ConfigureAwait(false)
            : string.Empty;
        Reason = await UiPageHelpers.FormValueAsync(this, "reason", 4096).ConfigureAwait(false);
        Microsoft.AspNetCore.Http.IFormCollection form = await Request.ReadFormAsync(HttpContext.RequestAborted).ConfigureAwait(false);
        if (LabelDuration.TryRead(form, out int hours))
        {
            Duration = new(hours);
        }
    }

    /// <summary>Loads a canonical DID-based target and protects its observed label sequences against substitution.</summary>
    private async Task LoadLabelsAsync()
    {
        if (Target.Length == 0)
        {
            ViewData["Error"] = "Enter an account DID/handle or a complete record AT URI.";
            Labels = new([], null);
            return;
        }

        AdministrationResponse response = await UiPageHelpers.DispatchAsync(
            this,
            new AdministrationCommand(
                Method: "getManualLabelChoices",
                Target: Target,
                Cid: Cid.Length == 0 ? null : Cid)).ConfigureAwait(false);
        ModerationLabelChoiceView[] choices = response.Moderation?.LabelChoices ?? [];
        if (response.Succeeded && response.Target is { } resolved)
        {
            Target = resolved;
        }

        Labels = new(
            Choices: choices,
            State: response.Succeeded && response.Target is { } canonical
                ? Protect(new(
                    Actor: User.FindFirst(AtProtoClaims.Did)!.Value,
                    TargetKind: TargetKind,
                    InputTarget: Target,
                    Target: canonical,
                    Cid: Cid,
                    OperationId: Guid.NewGuid().ToString("N"),
                    Choices: choices))
                : null,
            Target: Target,
            Cid: Cid);
    }

    /// <summary>Authenticates the actor, canonical target, observed baseline and any frozen retry intent.</summary>
    private string Protect(ManualLabelForm snapshot) =>
        Protector().Protect(JsonSerializer.Serialize(snapshot, ManualLabelFormContext.Default.ManualLabelForm));

    /// <summary>Separates manual label state from other protected browser workflows.</summary>
    private IDataProtector Protector() =>
        HttpContext.RequestServices.GetRequiredService<IDataProtectionProvider>().CreateProtector("Xenon.ManualLabels.v1");
}

/// <summary>Retains the exact loaded target, per-label concurrency fence and immutable retry intent.</summary>
internal sealed record ManualLabelForm(
    string Actor,
    string TargetKind,
    string InputTarget,
    string Target,
    string Cid,
    string OperationId,
    ModerationLabelChoiceView[] Choices,
    string[]? Selected = null,
    string? Reason = null,
    int DurationHours = 0,
    DateTimeOffset? ExpiresAt = null);

/// <summary>Renders the shared background and no-script label controls without exposing the protected baseline.</summary>
internal sealed record ManualLabelFormDisplay(
    ModerationLabelChoiceView[] Choices,
    string? State,
    string[]? Selected = null,
    bool Retrying = false,
    string? Error = null,
    string? Target = null,
    string? Cid = null);

/// <summary>Supplies trimming-safe serialization for protected manual label form state.</summary>
[JsonSerializable(typeof(ManualLabelForm))]
internal sealed partial class ManualLabelFormContext : JsonSerializerContext;
