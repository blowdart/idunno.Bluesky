// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

using idunno.AtProto;
using idunno.AtProto.Authentication;
using idunno.Bluesky.Actor;
using idunno.Bluesky.AspNet.Authentication;
using idunno.Bluesky;

namespace Samples.AspNetAuthentication.Pages;

[Authorize]
public class IndexModel(BlueskyAgent agent) : PageModel
{
    public async void OnGet()
    {
        System.Diagnostics.Debug.WriteLine(HttpContext.User?.Identity?.Name);


        if (User is not null && User.Identity?.IsAuthenticated == true && User.Identity is ClaimsIdentity && User.Did is not null)
        {
            AtProtoHttpResult<ProfileViewDetailed> profile = await agent.GetProfile(User.Did!).ConfigureAwait(false);
        }
    }
}
