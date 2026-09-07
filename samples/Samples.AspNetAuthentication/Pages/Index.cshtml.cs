// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Security.Claims;

using idunno.AtProto.Authentication;
using idunno.Bluesky;
using idunno.Bluesky.Actor;
using idunno.Bluesky.Feed;

using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Samples.AspNetAuthentication.Pages;

public class IndexModel(BlueskyAgent agent) : PageModel
{
    public ICollection<FeedViewPost>? Timeline { get; set; }

    public async Task OnGet()
    {
        if (User is not null && User.Identity?.IsAuthenticated == true && User.Identity is ClaimsIdentity && User.Did is not null)
        {
            if (agent.HasCredentials && agent.Credentials.ExpiresOn < DateTimeOffset.UtcNow)
            {
                bool refreshResult = await agent.RefreshCredentials(cancellationToken: HttpContext.RequestAborted);
                if (!refreshResult || !agent.IsAuthenticated)
                {
                    throw new InvalidOperationException("Failed to refresh token.");
                }
            }

            Preferences preferences = new();
            var preferencesResult = await agent.GetPreferences(cancellationToken: HttpContext.RequestAborted);
            if (preferencesResult.Succeeded)
            {
                preferences = preferencesResult.Result;
            }

            var timelineResult = await agent.GetTimeline(
                subscribedLabelers: preferences.SubscribedLabelers,
                cancellationToken: HttpContext.RequestAborted);

            if (timelineResult.Succeeded)
            {
                Timeline = timelineResult.Result;
            }
        }
    }
}
