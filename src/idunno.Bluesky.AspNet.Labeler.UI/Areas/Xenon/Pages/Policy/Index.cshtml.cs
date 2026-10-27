// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace idunno.Bluesky.AspNet.Labeler.UI.Areas.Xenon.Pages.Policy;

/// <summary>Inspects versioned moderation policy and submits explicit saves or publication requests.</summary>
public sealed class IndexModel : PageModel
{
    /// <summary>Gets the policy state response.</summary>
    internal JsonElement Data { get; private set; }

    /// <summary>Gets the selected report reasons, or null when every reason is accepted.</summary>
    internal string[]? ReasonTypes { get; private set; }

    /// <summary>Gets the existing declaration as a starting point for the next immutable draft.</summary>
    internal string DeclarationText { get; private set; } = string.Empty;

    /// <summary>Tracks whether submitted reason choices must be retained after a failed save.</summary>
    private bool ReasonsSubmitted { get; set; }

    /// <summary>Loads the persisted and independently verified policy state.</summary>
    public async Task OnGetAsync()
    {
        AdministrationResponse response = await UiPageHelpers.DispatchAsync(
            this,
            new AdministrationCommand(Method: "getPolicy", Reason: "Xenon policy inspection.")).ConfigureAwait(false);
        Data = UiPageHelpers.ToElement(response);
        if (response.Moderation?.Policy?.Definition is { } definition)
        {
            DeclarationText = definition.Declaration.GetRawText();
            if (!ReasonsSubmitted && definition.Declaration.TryGetProperty("reasonTypes", out JsonElement reasons))
            {
                ReasonTypes = reasons.EnumerateArray().Select(value => value.GetString()!).ToArray();
            }
        }
    }

    /// <summary>Saves an immutable policy version after explicit administrator confirmation.</summary>
    /// <returns>An action result that displays the outcome of the policy save.</returns>
    public async Task<IActionResult> OnPostSaveAsync()
    {
        if (!await UiPageHelpers.ConfirmedAsync(this).ConfigureAwait(false))
        {
            ViewData["Error"] = "Confirm the policy save before submitting.";
            await OnGetAsync().ConfigureAwait(false);
            return Page();
        }

        string version = await UiPageHelpers.FormValueAsync(this, "version", 256).ConfigureAwait(false);
        string declarationText = await UiPageHelpers.FormValueAsync(this, "declaration", 32768).ConfigureAwait(false);
        string rules = await UiPageHelpers.FormValueAsync(this, "rules", 4096).ConfigureAwait(false);
        string collections = await UiPageHelpers.FormValueAsync(this, "collections", 2048).ConfigureAwait(false);
        string reason = await UiPageHelpers.FormValueAsync(this, "reason", 4096).ConfigureAwait(false);
        string operationId = await UiPageHelpers.FormValueAsync(this, "operationId", 256).ConfigureAwait(false);
        Microsoft.AspNetCore.Http.IFormCollection form = await Request.ReadFormAsync(HttpContext.RequestAborted).ConfigureAwait(false);
        ReasonsSubmitted = true;
        if (form["reasonMode"] != "all" && form["reasonMode"] != "selected")
        {
            ViewData["Error"] = "Choose whether to accept all report reasons or only selected reasons.";
            await OnGetAsync().ConfigureAwait(false);
            return Page();
        }
        ReasonTypes = form["reasonMode"] == "all" ? null : form["reasonTypes"]
            .Select(value => value ?? string.Empty)
            .Concat(form["customReasons"].ToString().Split(
                ['\r', '\n'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            .Distinct(StringComparer.Ordinal).ToArray();
        if (ReasonTypes is { } selectedReasons && (selectedReasons.Length > 1000 ||
            selectedReasons.Any(value => value.Length > 1024 || string.IsNullOrWhiteSpace(value))))
        {
            ViewData["Error"] = "Choose at most 1,000 report reasons, with at most 1,024 characters per identifier.";
            await OnGetAsync().ConfigureAwait(false);
            return Page();
        }
        try
        {
            using JsonDocument declaration = JsonDocument.Parse(declarationText, new JsonDocumentOptions { MaxDepth = 16 });
            JsonElement editedDeclaration = WithReportReasons(declaration.RootElement, ReasonTypes);
            string[]? allowed = collections.Length == 0 ? null : collections.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            AdministrationResponse response = await UiPageHelpers.DispatchAsync(
                this,
                new AdministrationCommand(
                    Method: "savePolicy",
                    OperationId: operationId,
                    Reason: reason,
                    Moderation: new(
                        PolicyVersion: version,
                        Declaration: editedDeclaration,
                        AllowAccounts: await UiPageHelpers.ConfirmedFieldAsync(this, "allowAccounts").ConfigureAwait(false),
                        AllowRecords: await UiPageHelpers.ConfirmedFieldAsync(this, "allowRecords").ConfigureAwait(false),
                        AllowedRecordCollections: allowed,
                        Rules: rules))).ConfigureAwait(false);
            if (response.Succeeded)
            {
                TempData["Status"] = $"Policy save operation {operationId} completed.";
                return RedirectToPage();
            }
        }
        catch (JsonException)
        {
            ViewData["Error"] = "The policy declaration must be valid JSON.";
        }

        await OnGetAsync().ConfigureAwait(false);
        return Page();
    }

    /// <summary>Replaces only report reason restrictions while preserving all other declaration fields for shared validation.</summary>
    private static JsonElement WithReportReasons(JsonElement declaration, string[]? reasons)
    {
        if (declaration.ValueKind != JsonValueKind.Object ||
            declaration.EnumerateObject().Select(property => property.Name).Distinct(StringComparer.Ordinal).Count() != declaration.EnumerateObject().Count())
        {
            throw new JsonException("The declaration must be an object with unique property names.");
        }
        using MemoryStream buffer = new();
        using (Utf8JsonWriter writer = new(buffer))
        {
            writer.WriteStartObject();
            foreach (JsonProperty property in declaration.EnumerateObject())
            {
                if (property.Name != "reasonTypes")
                {
                    property.WriteTo(writer);
                }
            }
            if (reasons is not null)
            {
                writer.WriteStartArray("reasonTypes");
                foreach (string reason in reasons)
                {
                    writer.WriteStringValue(reason);
                }
                writer.WriteEndArray();
            }
            writer.WriteEndObject();
        }
        using JsonDocument updated = JsonDocument.Parse(buffer.ToArray());
        return updated.RootElement.Clone();
    }

    /// <summary>Starts conditional publication of a retained policy version.</summary>
    /// <returns>An action result that displays the outcome of policy publication.</returns>
    public async Task<IActionResult> OnPostPublishAsync()
    {
        if (!await UiPageHelpers.ConfirmedAsync(this).ConfigureAwait(false))
        {
            ViewData["Error"] = "Confirm policy publication before submitting.";
            await OnGetAsync().ConfigureAwait(false);
            return Page();
        }

        string version = await UiPageHelpers.FormValueAsync(this, "version", 256).ConfigureAwait(false);
        string previousCid = await UiPageHelpers.FormValueAsync(this, "previousCid", 128).ConfigureAwait(false);
        string reason = await UiPageHelpers.FormValueAsync(this, "reason", 4096).ConfigureAwait(false);
        string operationId = await UiPageHelpers.FormValueAsync(this, "operationId", 256).ConfigureAwait(false);
        if (!long.TryParse(await UiPageHelpers.FormValueAsync(this, "revision", 24).ConfigureAwait(false), out long revision))
        {
            ViewData["Error"] = "Reload the policy state to get its current revision.";
            await OnGetAsync().ConfigureAwait(false);
            return Page();
        }

        AdministrationResponse response = await UiPageHelpers.DispatchAsync(
            this,
            new AdministrationCommand(
                Method: "beginPolicyPublication",
                OperationId: operationId,
                Reason: reason,
                Moderation: new(PolicyVersion: version, ExpectedRevision: revision, PreviousCid: previousCid.Length == 0 ? null : previousCid))).ConfigureAwait(false);
        if (response.Succeeded)
        {
            TempData["Status"] = $"Policy publication operation {operationId} started. Inspect policy state to continue recovery.";
            return RedirectToPage();
        }

        await OnGetAsync().ConfigureAwait(false);
        return Page();
    }

    /// <summary>Forces independent declaration verification using the shared policy service.</summary>
    /// <returns>An action result that displays the verification outcome.</returns>
    public async Task<IActionResult> OnPostVerifyAsync()
    {
        if (!await UiPageHelpers.ConfirmedAsync(this).ConfigureAwait(false))
        {
            ViewData["Error"] = "Confirm policy verification before submitting.";
            await OnGetAsync().ConfigureAwait(false);
            return Page();
        }

        string reason = await UiPageHelpers.FormValueAsync(this, "reason", 4096).ConfigureAwait(false);
        AdministrationResponse response = await UiPageHelpers.DispatchAsync(
            this,
            new AdministrationCommand(Method: "verifyPolicy", Reason: reason)).ConfigureAwait(false);
        if (response.Succeeded)
        {
            TempData["Status"] = "Policy declaration verification completed.";
            return RedirectToPage();
        }

        await OnGetAsync().ConfigureAwait(false);
        return Page();
    }
}
