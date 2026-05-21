// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Security.Claims;
using Microsoft.AspNetCore.Mvc.RazorPages;

using idunno.AtProto;
using idunno.AtProto.Authentication;
using idunno.Bluesky.Actor;
using idunno.Bluesky;

namespace Samples.AspNetAuthentication.Pages;

public class IndexModel(BlueskyAgent agent) : PageModel
{
    public async void OnGet()
    {
        System.Diagnostics.Debug.WriteLine(HttpContext.User?.Identity?.Name);

        agent.CredentialsUpdated += async (s, e) =>
        {
            System.Diagnostics.Debug.WriteLine($"Credentials updated for DID: {e.Did}, Service: {e.Service}");
        };

        if (User is not null && User.Identity?.IsAuthenticated == true && User.Identity is ClaimsIdentity && User.Did is not null)
        {
            AtProtoHttpResult<ProfileViewDetailed> profile = await agent.GetProfile(User.Did!).ConfigureAwait(false);
        }
    }
}
