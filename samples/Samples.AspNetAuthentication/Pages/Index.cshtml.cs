// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Security.Claims;
using Microsoft.AspNetCore.Mvc.RazorPages;

using idunno.AtProto;
using idunno.AtProto.Authentication;
using idunno.Bluesky;
using idunno.Bluesky.Actor;

namespace Samples.AspNetAuthentication.Pages;

public class IndexModel(BlueskyAgent agent) : PageModel
{
    public async Task OnGet()
    {
        if (User is not null && User.Identity?.IsAuthenticated == true && User.Identity is ClaimsIdentity && User.Did is not null)
        {
            System.Diagnostics.Debug.WriteLine($"Authenticated user: {User.Identity.Name} ({User.Did})");
            Claim? claim = User.Claims.FirstOrDefault(c => c.Type == AtProtoClaims.DPoPNonce);
            if (claim is not null)
            {
                System.Diagnostics.Debug.WriteLine($"DPoP Nonce: {claim.Value} @ {DateTimeOffset.Now}");
            }

            AtProtoHttpResult<ProfileViewDetailed> profile = await agent.GetProfile(User.Did!).ConfigureAwait(true);

            if (claim is not null)
            {
                System.Diagnostics.Debug.WriteLine($"DPoP Nonce: {claim.Value} @ {DateTimeOffset.Now}");
            }

            profile = await agent.GetProfile(User.Did!).ConfigureAwait(true);

            if (claim is not null)
            {
                System.Diagnostics.Debug.WriteLine($"DPoP Nonce: {claim.Value} @ {DateTimeOffset.Now}");
            }
        }
    }
}
