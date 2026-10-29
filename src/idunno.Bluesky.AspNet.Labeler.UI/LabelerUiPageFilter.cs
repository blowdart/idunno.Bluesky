// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.DependencyInjection;

namespace idunno.Bluesky.AspNet.Labeler.UI;

/// <summary>Loads live team authority before rendering navigation and rejects nonmembers from the UI.</summary>
internal sealed class LabelerUiPageFilter(LabelerUiAdministration administration) : IAsyncPageFilter
{
    /// <summary>Does not require work before Razor Pages selects a handler.</summary>
    public Task OnPageHandlerSelectionAsync(PageHandlerSelectedContext context) => Task.CompletedTask;

    /// <summary>Checks live membership before invoking any page handler.</summary>
    public async Task OnPageHandlerExecutionAsync(PageHandlerExecutingContext context, PageHandlerExecutionDelegate next)
    {
        idunno.Bluesky.AspNet.Labeler.AdministrationResponse response = await administration.DispatchAsync(
            principal: context.HttpContext.User,
            command: new idunno.Bluesky.AspNet.Labeler.AdministrationCommand(
                Method: "getTeam",
                Reason: "Xenon navigation authorization."),
            cancellationToken: context.HttpContext.RequestAborted).ConfigureAwait(false);
        if (!response.Succeeded || response.Members is null)
        {
            context.Result = new StatusCodeResult(StatusCodes.Status403Forbidden);
            return;
        }

        string? actor = context.HttpContext.User.FindFirst(idunno.AtProto.Authentication.AtProtoClaims.Did)?.Value;
        idunno.Bluesky.AspNet.Labeler.AdministrationMemberView? member = response.Members
            .Where(candidate => candidate.Active && !candidate.Removed)
            .FirstOrDefault(candidate => candidate.Did == actor);
        if (member is null)
        {
            context.Result = new StatusCodeResult(StatusCodes.Status403Forbidden);
            return;
        }

        context.HttpContext.Items["Xenon.CurrentRole"] = member.Role;
        PageModel page = (PageModel)context.HandlerInstance;
        page.ViewData["CurrentRole"] = member.Role;
        page.ViewData["AvailableModerators"] = response.Members
            .Where(candidate => candidate.Active && !candidate.Removed && candidate.Role >= idunno.Bluesky.Labeler.Administration.LabelTeamRole.Moderator)
            .ToArray();
        ITempDataDictionary tempData = context.HttpContext.RequestServices
            .GetRequiredService<ITempDataDictionaryFactory>()
            .GetTempData(context.HttpContext);
        page.ViewData["Status"] = tempData["Status"];
        await next().ConfigureAwait(false);
        idunno.Bluesky.AspNet.Labeler.AdministrationResponse totals = await administration.DispatchAsync(
            principal: context.HttpContext.User,
            command: new idunno.Bluesky.AspNet.Labeler.AdministrationCommand(
                Method: "getModerationOverview",
                Reason: "Xenon open workflow totals."),
            cancellationToken: context.HttpContext.RequestAborted).ConfigureAwait(false);
        if (totals.Succeeded && totals.Moderation?.OpenCounts is { } counts)
        {
            page.ViewData["OpenCounts"] = counts;
        }
        else
        {
            page.ViewData["Error"] = totals.Message ?? "Open workflow counts could not be loaded.";
        }
    }
}
