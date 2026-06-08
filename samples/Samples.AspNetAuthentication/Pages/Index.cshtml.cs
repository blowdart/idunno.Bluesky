// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Security.Claims;
using Microsoft.AspNetCore.Mvc.RazorPages;

using idunno.AtProto;
using idunno.AtProto.Authentication;
using idunno.Bluesky;
using idunno.Bluesky.Actor;
using idunno.Bluesky.AspNet.Authentication;

namespace Samples.AspNetAuthentication.Pages;

public class IndexModel(BlueskyAgent agent) : PageModel
{
    public async void OnGet()
    {
        System.Diagnostics.Debug.WriteLine(HttpContext.User?.Identity?.Name);

        // non-injected agent
        BlueskyAgent localAgent = new BlueskyAgent(principal: HttpContext?.User);

        if (User is not null && User.Identity?.IsAuthenticated == true && User.Identity is ClaimsIdentity && User.Did is not null)
        {
            System.Diagnostics.Debug.WriteLine($"Authenticated user: {User.Identity.Name} ({User.Did})");
            Claim? claim = User.Claims.FirstOrDefault(c => c.Type == AtProtoClaims.DPoPNonce);
            if (claim is not null)
            {
                System.Diagnostics.Debug.WriteLine($"DPoP Nonce: {claim.Value} @ {DateTimeOffset.Now}");
            }

            AtProtoHttpResult<ProfileViewDetailed> profile = await agent.GetProfile(User.Did!).ConfigureAwait(false);

            if (claim is not null)
            {
                System.Diagnostics.Debug.WriteLine($"DPoP Nonce: {claim.Value} @ {DateTimeOffset.Now}");
            }

            profile = await agent.GetProfile(User.Did!).ConfigureAwait(false);

            if (claim is not null)
            {
                System.Diagnostics.Debug.WriteLine($"DPoP Nonce: {claim.Value} @ {DateTimeOffset.Now}");
            }
        }
    }
}
