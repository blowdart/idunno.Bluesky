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
        idunno.Bluesky.Labeler.Administration.LabelTeamMember[] team =
            [.. response.Members.Select(candidate => new idunno.Bluesky.Labeler.Administration.LabelTeamMember(
                Did: new(candidate.Did),
                Role: candidate.Role,
                Active: candidate.Active,
                Removed: candidate.Removed))];
        OzoneTeamPresence presence = context.HttpContext.RequestServices.GetRequiredService<OzoneTeamPresence>();
        presence.Observe(new(member.Did), team);
        OzoneOnlineMember[] online = presence.List(team, int.MaxValue);
        bool showPresence = team.Count(candidate => candidate.Active && !candidate.Removed) > 1;
        context.HttpContext.Items["Xenon.OnlineMembers"] = online;
        context.HttpContext.Items["Xenon.ShowPresence"] = showPresence;
        PageModel page = (PageModel)context.HandlerInstance;
        if (page is Areas.Xenon.Pages.Cases.IndexModel or
            Areas.Xenon.Pages.Reports.IndexModel or Areas.Xenon.Pages.Appeals.IndexModel)
        {
            context.HttpContext.Response.Headers.CacheControl = "private, no-store";
        }
        bool countsRequest = page is Areas.Xenon.Pages.IndexModel &&
            context.HandlerMethod?.MethodInfo.Name is
                nameof(Areas.Xenon.Pages.IndexModel.OnGetCountsAsync) or
                nameof(Areas.Xenon.Pages.IndexModel.OnGetOnlineAsync);
        if (countsRequest)
        {
            context.HttpContext.Response.Headers.CacheControl = "private, no-store";
            await next().ConfigureAwait(false);
            return;
        }
        page.ViewData["CurrentRole"] = member.Role;
        page.ViewData["TeamMembers"] = response.Members;
        page.ViewData["OnlineMembers"] = online;
        page.ViewData["ShowPresence"] = showPresence;
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
            page.ViewData["Error"] = totals.Message ?? "Case, report and appeal counts could not be loaded.";
        }
    }
}
