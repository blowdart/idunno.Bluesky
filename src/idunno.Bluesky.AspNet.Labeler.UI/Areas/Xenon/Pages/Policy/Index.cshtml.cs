// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;
using System.Text.Json.Nodes;

using idunno.AtProto;
using idunno.AtProto.Authentication;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

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

    /// <summary>Gets the definition being edited independently of the selected publication state.</summary>
    internal JsonElement Editor { get; private set; }

    /// <summary>Gets the currently selected definition for explicit reloading.</summary>
    internal JsonElement CurrentDefinition { get; private set; }

    /// <summary>Gets the latest saved version that has never been selected for publication.</summary>
    internal JsonElement DraftDefinition => DraftDefinitions.Count == 0 ? default : DraftDefinitions[0];

    /// <summary>Gets unpublished, undeleted drafts in most-recently-saved order.</summary>
    internal IReadOnlyList<JsonElement> DraftDefinitions { get; private set; } = [];

    /// <summary>Gets the draft awaiting an explicit deletion confirmation.</summary>
    internal string? PendingDeletion { get; private set; }

    /// <summary>Gets the indented selected declaration for inspection.</summary>
    internal string CurrentDeclaration { get; private set; } = string.Empty;

    /// <summary>Gets the administrator-entered policy ID, retained across failed saves.</summary>
    internal string Version { get; private set; } = string.Empty;

    /// <summary>Gets the submitted audit reason after a failed save.</summary>
    internal string SaveReason { get; private set; } = string.Empty;

    /// <summary>Gets the active editor source description.</summary>
    internal string EditorSource { get; private set; } = "New policy";

    /// <summary>Tracks whether posted editor values must survive validation failures.</summary>
    private bool EditorSubmitted { get; set; }

    /// <summary>Gets whether the page is redisplaying unsaved submitted draft values.</summary>
    internal bool HasSubmittedDraft => EditorSubmitted;

    /// <summary>Loads the persisted and independently verified policy state.</summary>
    public async Task OnGetAsync()
    {
        if (TempData["PublicationError"] is string publicationError)
        {
            ViewData["Error"] = publicationError;
        }
        AdministrationResponse response = await UiPageHelpers.DispatchAsync(
            this,
            new AdministrationCommand(Method: "getPolicy", Reason: "Xenon policy inspection.")).ConfigureAwait(false);
        Data = UiPageHelpers.ToElement(response);
        if (!response.Succeeded)
        {
            return;
        }

        if (response.Moderation?.Policy?.Definition is { } definition)
        {
            CurrentDefinition = UiPageHelpers.ToElement(new AdministrationResponse(true, Moderation: new(Definition: definition)))
                .GetProperty("moderation").GetProperty("definition").Clone();
            CurrentDeclaration = PrettyJson(definition.Declaration);
        }

        await LoadDraftAsync().ConfigureAwait(false);
        if (EditorSubmitted)
        {
            return;
        }

        Editor = CurrentDefinition;
        EditorSource = CurrentDefinition.ValueKind == JsonValueKind.Object
            ? "Current policy: " + CurrentDefinition.GetProperty("version").GetString() : "New policy";
        if (Request.Query["editor"] == "draft")
        {
            string requested = Request.Query["draft"].ToString();
            JsonElement draft = requested.Length == 0 ? DraftDefinition :
                DraftDefinitions.FirstOrDefault(item => item.GetProperty("version").GetString() == requested);
            if (draft.ValueKind == JsonValueKind.Object)
            {
                Editor = draft;
                EditorSource = "Saved draft: " + draft.GetProperty("version").GetString();
            }
            else
            {
                ViewData["Error"] = "The unpublished draft is no longer available. Reload the draft list.";
            }
        }
        else if (Request.Query["editor"] == "clear")
        {
            Editor = default;
            EditorSource = "New policy";
        }

        if (Editor.ValueKind != JsonValueKind.Object)
        {
            using JsonDocument empty = JsonDocument.Parse(
                $$$"""{"$type":"app.bsky.labeler.service","createdAt":"{{{DateTimeOffset.UtcNow:O}}}","subjectTypes":[],"policies":{"labelValues":[]}}""");
            Editor = UiPageHelpers.ToElement(new AdministrationResponse(true, Moderation: new(Definition: new(
                Version: string.Empty,
                Declaration: empty.RootElement.Clone(),
                AllowAccounts: false,
                AllowRecords: false,
                AllowedRecordCollections: null,
                Rules: string.Empty)))).GetProperty("moderation").GetProperty("definition").Clone();
        }

        DeclarationText = PrettyJson(Editor.GetProperty("declaration"));
        ReasonTypes = Editor.GetProperty("declaration").TryGetProperty("reasonTypes", out JsonElement reasons)
            ? [.. reasons.EnumerateArray().Select(value => value.GetString()!)] : null;
    }

    /// <summary>Finds unpublished drafts in sequence order without treating old published versions as drafts.</summary>
    private async Task LoadDraftAsync()
    {
        Dictionary<string, (long Sequence, ModerationPolicyDefinitionView Definition)> drafts = new(StringComparer.Ordinal);
        HashSet<string> published = new(StringComparer.Ordinal);
        HashSet<string> deleted = new(StringComparer.Ordinal);
        long after = 0;
        while (true)
        {
            AdministrationResponse history = await UiPageHelpers.DispatchAsync(
                this,
                new AdministrationCommand(Method: "getPolicyHistory", Reason: "Inspect saved policy drafts.", AfterSequence: after, Limit: 100)).ConfigureAwait(false);
            if (!history.Succeeded)
            {
                return;
            }

            ModerationPolicyHistoryView[] entries = history.Moderation?.PolicyHistory ?? [];
            foreach (ModerationPolicyHistoryView entry in entries)
            {
                if (entry.Action == "Draft" && entry.Definition is { } draft)
                {
                    drafts[draft.Version] = (entry.Sequence, draft);
                }
                else if (entry.Action == "Publication" && entry.State?.Definition is { } selected)
                {
                    published.Add(selected.Version);
                }
                else if (entry.Action == "DraftDeletion" && entry.Definition is { } removed)
                {
                    deleted.Add(removed.Version);
                }
            }

            if (entries.Length < 100)
            {
                break;
            }
            after = entries[^1].Sequence;
        }

        DraftDefinitions = [.. drafts.Where(pair => !published.Contains(pair.Key) && !deleted.Contains(pair.Key) &&
            (CurrentDefinition.ValueKind != JsonValueKind.Object || pair.Key != CurrentDefinition.GetProperty("version").GetString()))
            .OrderByDescending(pair => pair.Value.Sequence).Select(pair =>
                UiPageHelpers.ToElement(new AdministrationResponse(true, Moderation: new(Definition: pair.Value.Definition)))
                    .GetProperty("moderation").GetProperty("definition").Clone())];
    }

    /// <summary>Formats declaration JSON without requiring reflection-based serialization metadata.</summary>
    private static string PrettyJson(JsonElement value)
    {
        using MemoryStream buffer = new();
        using (Utf8JsonWriter writer = new(buffer, new JsonWriterOptions { Indented = true }))
        {
            value.WriteTo(writer);
        }

        return System.Text.Encoding.UTF8.GetString(buffer.ToArray());
    }

    /// <summary>Separates lower-to-upper-case word boundaries in persisted phase names.</summary>
    internal static string FormatPhase(string value) => string.Concat(value.Select((letter, index) =>
        index > 0 && char.IsUpper(letter) && char.IsLower(value[index - 1]) ? $" {letter}" : letter.ToString()));

    /// <summary>Shows an explicit server-rendered confirmation without deleting or changing policy state.</summary>
    /// <returns>The current page with a confirmation for an available unpublished draft.</returns>
    public async Task<IActionResult> OnPostDeleteRequestAsync()
    {
        string version = await UiPageHelpers.FormValueAsync(this, "version", 256).ConfigureAwait(false);
        await OnGetAsync().ConfigureAwait(false);
        if (DraftDefinitions.Any(item => item.GetProperty("version").GetString() == version))
        {
            PendingDeletion = version;
        }
        else
        {
            ViewData["Error"] = "The unpublished draft is no longer available. Reload the draft list.";
        }

        return Page();
    }

    /// <summary>Deletes an explicitly confirmed unpublished draft through the shared audited service.</summary>
    /// <returns>The refreshed policy page or a classified deletion error.</returns>
    public async Task<IActionResult> OnPostDeleteAsync()
    {
        string version = await UiPageHelpers.FormValueAsync(this, "version", 256).ConfigureAwait(false);
        if (!await UiPageHelpers.ConfirmedAsync(this).ConfigureAwait(false))
        {
            ViewData["Error"] = "Confirm draft deletion before submitting.";
            await OnGetAsync().ConfigureAwait(false);
            return Page();
        }

        AdministrationResponse response = await UiPageHelpers.DispatchAsync(
            this,
            new AdministrationCommand(
                Method: "deletePolicyDraft",
                OperationId: await UiPageHelpers.FormValueAsync(this, "operationId", 256).ConfigureAwait(false),
                Reason: await UiPageHelpers.FormValueAsync(this, "reason", 4096).ConfigureAwait(false),
                Moderation: new(PolicyVersion: version))).ConfigureAwait(false);
        if (response.Succeeded)
        {
            TempData["Status"] = $"Draft {version} deleted. Its audit and policy history are retained.";
            return RedirectToPage();
        }

        await OnGetAsync().ConfigureAwait(false);
        return Page();
    }

    /// <summary>Saves an immutable policy version with submission as the administrator's confirmation.</summary>
    /// <returns>An action result that displays the outcome of the policy save.</returns>
    public async Task<IActionResult> OnPostSaveAsync()
    {
        string version = await UiPageHelpers.FormValueAsync(this, "version", 256).ConfigureAwait(false);
        string declarationText = await UiPageHelpers.FormValueAsync(this, "declaration", 32768).ConfigureAwait(false);
        string rules = await UiPageHelpers.FormValueAsync(this, "rules", 4096).ConfigureAwait(false);
        string collections = await UiPageHelpers.FormValueAsync(this, "collections", 2048).ConfigureAwait(false);
        string reason = await UiPageHelpers.FormValueAsync(this, "reason", 4096).ConfigureAwait(false);
        string operationId = await UiPageHelpers.FormValueAsync(this, "operationId", 256).ConfigureAwait(false);
        Microsoft.AspNetCore.Http.IFormCollection form = await Request.ReadFormAsync(HttpContext.RequestAborted).ConfigureAwait(false);
        EditorSubmitted = true;
        Version = version;
        SaveReason = reason;
        DeclarationText = declarationText;
        EditorSource = "Unsaved edits";
        bool allCollections = form["structuredIssuance"] == "true" ? form["issueAllCollections"] == "true" : collections.Length == 0;
        string[]? allowed = allCollections ? null : collections.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        using JsonDocument emptyDeclaration = JsonDocument.Parse("{}");
        Editor = UiPageHelpers.ToElement(new AdministrationResponse(true, Moderation: new(Definition: new(
            Version: Version,
            Declaration: emptyDeclaration.RootElement.Clone(),
            AllowAccounts: form["allowAccounts"] == "true",
            AllowRecords: form["allowRecords"] == "true",
            AllowedRecordCollections: allowed,
            Rules: rules)))).GetProperty("moderation").GetProperty("definition").Clone();
        if (string.IsNullOrWhiteSpace(Version))
        {
            ViewData["Error"] = "Enter a Policy ID before saving.";
            await OnGetAsync().ConfigureAwait(false);
            return Page();
        }
        if (form["reasonMode"] != "all" && form["reasonMode"] != "selected")
        {
            ViewData["Error"] = "Choose whether to accept all report reasons or only selected reasons.";
            await OnGetAsync().ConfigureAwait(false);
            return Page();
        }
        ReasonTypes = form["reasonMode"] == "all" ? null : [.. form["reasonTypes"]
            .Select(value => value ?? string.Empty)
            .Concat(form["customReasons"].ToString().Split(
                ['\r', '\n'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            .Distinct(StringComparer.Ordinal)];
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
            if (form["structuredScope"] == "true")
            {
                JsonObject updated = JsonNode.Parse(editedDeclaration.GetRawText())!.AsObject();
                updated["subjectTypes"] = new JsonArray([.. form["reportSubjects"].Select(value => (JsonNode?)JsonValue.Create(value))]);
                string reportCollections = await UiPageHelpers.FormValueAsync(this, "reportCollections", 2048).ConfigureAwait(false);
                if (form["reportAllCollections"] == "true")
                {
                    updated.Remove("subjectCollections");
                }
                else
                {
                    updated["subjectCollections"] = new JsonArray([.. reportCollections.Split(
                        ',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Select(value => (JsonNode?)JsonValue.Create(value))]);
                }
                using JsonDocument scoped = JsonDocument.Parse(updated.ToJsonString());
                editedDeclaration = scoped.RootElement.Clone();
            }
            DeclarationText = PrettyJson(editedDeclaration);
            AdministrationResponse response = await UiPageHelpers.DispatchAsync(
                this,
                new AdministrationCommand(
                    Method: "savePolicy",
                    OperationId: operationId,
                    Reason: reason,
                    Moderation: new(
                        PolicyVersion: Version,
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
        if (await UiPageHelpers.ConfirmedFieldAsync(this, "resumeWithdrawal").ConfigureAwait(false))
        {
            AdministrationResponse resumed = await UiPageHelpers.DispatchAsync(
                this,
                new AdministrationCommand(Method: "resumePolicyPublication", OperationId: operationId, Reason: reason)).ConfigureAwait(false);
            if (resumed.Succeeded)
            {
                TempData["Status"] = "Publication cleanup authorization renewed. Durable batches will continue in the background.";
                return RedirectToPage(pageName: "./Index", pageHandler: null, routeValues: null, fragment: "policy-publication");
            }
            await OnGetAsync().ConfigureAwait(false);
            return Page();
        }
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
            TempData["Status"] = $"Policy publication operation {operationId} started. Removed-label cleanup, when required, runs in durable background batches before declaration publication can continue.";
            return RedirectToPage(pageName: "./Index", pageHandler: null, routeValues: null, fragment: "policy-publication");
        }

        await OnGetAsync().ConfigureAwait(false);
        return Page();
    }

    /// <summary>Starts separate labeler-account authorization only after background withdrawal is complete.</summary>
    /// <returns>The external OAuth redirect or the publication tab with a classified error.</returns>
    public async Task<IActionResult> OnPostSignInAndPublishAsync()
    {
        string handle = await UiPageHelpers.FormValueAsync(this, "labelerHandle", 253).ConfigureAwait(false);
        string operation = await UiPageHelpers.FormValueAsync(this, "operationId", 256).ConfigureAwait(false);
        if (!Handle.TryParse(handle, out Handle? labeler))
        {
            ViewData["Error"] = "Enter the labeler account's handle to authorize publication.";
            await OnGetAsync().ConfigureAwait(false);
            return Page();
        }
        try
        {
            Uri redirect = await HttpContext.RequestServices.GetRequiredService<PolicyPublicationService>()
                .StartAsync(HttpContext, operation, labeler).ConfigureAwait(false);
            return Redirect(redirect.AbsoluteUri);
        }
        catch (Exception exception) when (exception is OAuthException or InvalidOperationException or HttpRequestException or
            InvalidDataException or OperationCanceledException)
        {
            PolicyPublicationService.Failed(
                HttpContext.RequestServices.GetRequiredService<ILogger<PolicyPublicationService>>(),
                exception.GetType().Name);
            ViewData["Error"] = exception is InvalidOperationException ? exception.Message :
                "Labeler authorization could not start. No PDS write was attempted. Reload policy state before trying again.";
            await OnGetAsync().ConfigureAwait(false);
            return Page();
        }
    }

    /// <summary>Forces independent declaration verification using the shared policy service.</summary>
    /// <returns>An action result that displays the verification outcome.</returns>
    public async Task<IActionResult> OnPostVerifyAsync()
    {
        AdministrationResponse response = await UiPageHelpers.DispatchAsync(
            this,
            new AdministrationCommand(Method: "verifyPolicy", Reason: "Verify published declaration from Xenon.")).ConfigureAwait(false);
        if (response.Succeeded)
        {
            TempData["Status"] = "Policy declaration verification completed.";
            return RedirectToPage(pageName: "./Index", pageHandler: null, routeValues: null, fragment: "policy-verification");
        }

        await OnGetAsync().ConfigureAwait(false);
        return Page();
    }
}
