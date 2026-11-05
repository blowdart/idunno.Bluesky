// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;
using System.Text.Json.Nodes;

using idunno.AtProto;

using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.DependencyInjection;

namespace idunno.Bluesky.AspNet.Labeler.UI.Areas.Xenon.Pages.Subjects;

/// <summary>Displays current public records, identity metadata and labels without changing moderation state.</summary>
public sealed class DetailModel : PageModel
{
    /// <summary>Gets the canonical subject retained independently of verified display handles.</summary>
    internal string? Target { get; private set; }

    /// <summary>Gets the account whose profile owns the displayed record.</summary>
    internal string? Author { get; private set; }

    /// <summary>Gets the bounded public preview, including explicit unavailable-content feedback.</summary>
    internal SubjectPreview? Preview { get; private set; }

    /// <summary>Gets the complete current repository record address, including the profile record for an account.</summary>
    internal AtUri? RecordUri { get; private set; }

    /// <summary>Gets active issuer-local labels that apply to the displayed current version.</summary>
    internal ModerationLabelChoiceView[] Labels { get; private set; } = [];

    /// <summary>Gets whether issuer-local labels were successfully checked.</summary>
    internal bool LabelsAvailable { get; private set; }

    /// <summary>Gets whether the submitted subject is an account rather than a repository record.</summary>
    internal bool IsAccount { get; private set; }

    /// <summary>Loads a supported canonical subject after live team authorization.</summary>
    /// <param name="target">The account DID or complete AT URI of a repository record.</param>
    /// <returns>A task that loads current public content and its version metadata.</returns>
    public async Task OnGetAsync(string? target)
    {
        Response.Headers.CacheControl = "private, no-store";
        IdentityDisplayModel identity = new(target);
        if (!ModelState.IsValid || target is null || target.Length > 2048 || !identity.HasDetails)
        {
            Response.StatusCode = 400;
            ViewData["Error"] = "Enter an account DID or a complete record address (AT URI).";
            return;
        }

        Target = target;
        Author = identity.Did!.Value;
        IsAccount = identity.RecordPath.Length == 0;
        RecordUri = new(IsAccount ? $"at://{Author}/app.bsky.actor.profile/self" : target);
        JsonObject subject = new() { [identity.RecordPath.Length == 0 ? "did" : "uri"] = target };
        using JsonDocument document = JsonDocument.Parse(subject.ToJsonString());
        Preview = await HttpContext.RequestServices.GetRequiredService<SubjectPreviewService>()
            .LoadAsync(
                subject: document.RootElement,
                cancellationToken: HttpContext.RequestAborted,
                inspectRecord: true).ConfigureAwait(false);
        Preview = Preview with { Standalone = true };
        AdministrationResponse labels = await UiPageHelpers.DispatchAsync(
            this,
            new AdministrationCommand(
                Method: "getManualLabelChoices",
                Target: target,
                Cid: IsAccount ? null : Preview.Cid)).ConfigureAwait(false);
        LabelsAvailable = labels.Succeeded && (IsAccount || Preview.Cid is not null);
        Labels = labels.Moderation?.LabelChoices?.Where(label => label.Applied && !label.OtherVersion).ToArray() ?? [];
    }
}
