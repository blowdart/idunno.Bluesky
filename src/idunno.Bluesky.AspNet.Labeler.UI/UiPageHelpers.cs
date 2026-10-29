// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;

using idunno.AtProto;

using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.DependencyInjection;

namespace idunno.Bluesky.AspNet.Labeler.UI;

/// <summary>Shares bounded dispatch, feedback and source-generated response projection across pages.</summary>
internal static class UiPageHelpers
{
    /// <summary>Invokes the common dispatcher and stores a safe user-facing outcome.</summary>
    internal static async Task<AdministrationResponse> DispatchAsync(
        PageModel page,
        AdministrationCommand command)
    {
        if (!string.IsNullOrEmpty(command.OperationId))
        {
            page.ViewData["OperationId"] = command.OperationId;
        }

        AdministrationResponse response = await page.HttpContext.RequestServices
            .GetRequiredService<LabelerUiAdministration>()
            .DispatchAsync(page.User, command, page.HttpContext.RequestAborted).ConfigureAwait(false);
        if (!response.Succeeded)
        {
            page.ViewData["Error"] = response.Message ?? response.Error ?? "The operation was not completed.";
        }
        else if (response.OperationId is not null)
        {
            page.ViewData["Status"] = $"Operation {response.OperationId} completed.";
        }

        await WarmIdentitiesAsync(page, response).ConfigureAwait(false);
        return response;
    }

    /// <summary>Reads one counted, sorted page through the same browser caller and provider authorization boundaries.</summary>
    internal static async Task<JsonElement> ReadTableAsync(PageModel page, TableNavigation? navigation)
    {
        if (navigation is null || !page.ModelState.IsValid)
        {
            page.ViewData["Error"] = "Choose a valid page, sort column, direction and filter.";
            return default;
        }

        AdministrationResponse response = await page.HttpContext.RequestServices.GetRequiredService<LabelerUiAdministration>()
            .ReadTableAsync(page.User, navigation.Query, page.HttpContext.RequestAborted).ConfigureAwait(false);
        if (!response.Succeeded)
        {
            page.ViewData["Error"] = response.Message ?? response.Error ?? "The table could not be loaded.";
        }
        else
        {
            navigation.Page = response.PageNumber!.Value;
            navigation.Total = response.TotalCount!.Value;
            await WarmIdentitiesAsync(page, response).ConfigureAwait(false);
        }

        return ToElement(response);
    }

    /// <summary>Resolves a bounded page's distinct identities concurrently before rendering its individual cells.</summary>
    private static async Task WarmIdentitiesAsync(PageModel page, AdministrationResponse response)
    {
        if (response.Succeeded)
        {
            HashSet<Did> identities = [];
            CollectIdentities(ToElement(response), identities);
            IDidHandleResolver handles = page.HttpContext.RequestServices.GetRequiredService<IDidHandleResolver>();
            await Task.WhenAll(identities.Select(did =>
                handles.ResolveHandleAsync(did, page.HttpContext.RequestAborted).AsTask())).ConfigureAwait(false);
        }

    }

    /// <summary>Warms only structured identity fields concurrently so a table does not serialize network waits.</summary>
    private static void CollectIdentities(JsonElement element, HashSet<Did> identities)
    {
        if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in element.EnumerateArray())
            {
                CollectIdentities(item, identities);
            }
        }
        else if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (JsonProperty property in element.EnumerateObject())
            {
                if (property.Value.ValueKind == JsonValueKind.String &&
                    property.Name is "did" or "assignee" or "actor" or "initiator" or "issuer" or "src" or "uri")
                {
                    if (new IdentityDisplayModel(property.Value.GetString()).Did is Did did)
                    {
                        identities.Add(did);
                    }
                }
                else if (property.Name != "declaration")
                {
                    CollectIdentities(property.Value, identities);
                }
            }
        }
    }
    /// <summary>Creates a source-generated JSON projection for server-rendered tables and details.</summary>
    internal static JsonElement ToElement(AdministrationResponse response) =>
        JsonSerializer.SerializeToElement(response, AdministrationJsonContext.Default.AdministrationResponse);

    /// <summary>Reads one bounded text form value after Razor Pages antiforgery validation.</summary>
    internal static async Task<string> FormValueAsync(PageModel page, string key, int maximumLength)
    {
        Microsoft.AspNetCore.Http.IFormCollection form = await page.Request.ReadFormAsync(page.HttpContext.RequestAborted).ConfigureAwait(false);
        string value = form[key].ToString().Trim();
        return value.Length <= maximumLength ? value : string.Empty;
    }

    /// <summary>Checks an explicit confirmation field before a sensitive browser mutation.</summary>
    internal static async Task<bool> ConfirmedAsync(PageModel page)
    {
        Microsoft.AspNetCore.Http.IFormCollection form = await page.Request.ReadFormAsync(page.HttpContext.RequestAborted).ConfigureAwait(false);
        return form["confirmed"] == "true";
    }

    /// <summary>Reads a checkbox value from the antiforgery-validated form.</summary>
    internal static async Task<bool> ConfirmedFieldAsync(PageModel page, string key)
    {
        Microsoft.AspNetCore.Http.IFormCollection form = await page.Request.ReadFormAsync(page.HttpContext.RequestAborted).ConfigureAwait(false);
        return form[key] == "true";
    }
}
