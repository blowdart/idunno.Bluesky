using System.Security.Claims;

using idunno.AtProto.Authentication;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Samples.AspNetAuthentication.Pages;

[Authorize]
public class ClaimsModel : PageModel
{
    public IList<Claim> Claims { get; set; } = [];

    public DateTimeOffset? ExpiresOn { get; private set; }

    public async Task OnGet()
    {
        foreach (var claim in User.Claims)
        {
            if (claim.Type != AtProtoClaims.AccessToken &&
                claim.Type != AtProtoClaims.RefreshToken &&
                claim.Type != AtProtoClaims.DPoPProof &&
                claim.Type != AtProtoClaims.DPoPNonce)
            {
                Claims.Add(claim);
            }
        }

        if (User.HasClaim(c => c.Type == AtProtoClaims.AccessToken))
        {
            var accessToken = User.FindFirst(c => c.Type == AtProtoClaims.AccessToken);
            if (accessToken is not null)
            {
                JsonWebToken token = new(accessToken.Value);
                ExpiresOn = DateTime.SpecifyKind(token.ValidTo, DateTimeKind.Utc);
            }
        }
    }
}
