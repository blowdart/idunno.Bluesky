// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;
using System.Text.Json.Serialization;

using idunno.AtProto;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.DependencyInjection;

namespace idunno.Bluesky.AspNet.Labeler.UI.Areas.Xenon.Pages;

/// <summary>Displays the Xenon operator landing page.</summary>
public sealed class IndexModel : PageModel
{
    /// <summary>Returns recent team activity after live authorization, hiding presence for single-member teams.</summary>
    /// <returns>The uncached online-member list, or no content when the indicator should be hidden.</returns>
    public async Task<IActionResult> OnGetOnlineAsync()
    {
        Response.Headers.CacheControl = "private, no-store";
        Response.Headers.XContentTypeOptions = "nosniff";
        if (HttpContext.Items["Xenon.ShowPresence"] is not true)
        {
            return new NoContentResult();
        }
        OzoneOnlineMember[] members = (OzoneOnlineMember[])HttpContext.Items["Xenon.OnlineMembers"]!;
        IDidHandleResolver resolver = HttpContext.RequestServices.GetRequiredService<IDidHandleResolver>();
        List<OnlineMemberDisplay> display = [];
        foreach (OzoneOnlineMember member in members)
        {
            Handle? handle = await resolver.ResolveHandleAsync(new Did(member.Did), HttpContext.RequestAborted).ConfigureAwait(false);
            display.Add(new(
                Did: member.Did,
                DisplayName: handle is { IsValid: true } ? $"@{handle}" : "Handle unavailable",
                LastActive: member.LastActive));
        }
        return Content(
            JsonSerializer.Serialize(new OnlineMembersDisplay([.. display]), OnlineMembersJsonContext.Default.OnlineMembersDisplay),
            "application/json");
    }

    /// <summary>Returns current workflow totals after live team authorization without permitting cached responses.</summary>
    /// <returns>The workflow counts or an explicit service failure.</returns>
    public async Task<IActionResult> OnGetCountsAsync()
    {
        Response.Headers.CacheControl = "private, no-store";
        Response.Headers.XContentTypeOptions = "nosniff";
        AdministrationResponse response = await HttpContext.RequestServices.GetRequiredService<LabelerUiAdministration>()
            .DispatchAsync(
                principal: User,
                command: new AdministrationCommand(Method: "getModerationOverview", Reason: "Xenon background workflow totals."),
                cancellationToken: HttpContext.RequestAborted).ConfigureAwait(false);
        if (!response.Succeeded || response.Moderation?.OpenCounts is not { } counts)
        {
            return StatusCode(response.Error is "Unauthorized" or "Forbidden"
                ? StatusCodes.Status403Forbidden
                : StatusCodes.Status503ServiceUnavailable);
        }

        return Content(JsonSerializer.Serialize(counts, AdministrationJsonContext.Default.ModerationOpenCountsView), "application/json");
    }

    /// <summary>Gets the active team role used to explain available workflows.</summary>
    internal string Role => HttpContext.Items["Xenon.CurrentRole"]?.ToString() ?? "Viewer";
}

/// <summary>Displays a verified handle while retaining the canonical identity for a tooltip.</summary>
internal sealed record OnlineMemberDisplay(string Did, string DisplayName, DateTimeOffset LastActive);

/// <summary>Contains the currently authorized team's recent activity with readable identities.</summary>
internal sealed record OnlineMembersDisplay(OnlineMemberDisplay[] Members);

/// <summary>Serializes the Xenon-only presence display without reflection.</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(OnlineMembersDisplay))]
internal sealed partial class OnlineMembersJsonContext : JsonSerializerContext;
