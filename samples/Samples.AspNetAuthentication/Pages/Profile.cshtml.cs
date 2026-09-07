using System.Security.Claims;

using idunno.AtProto.Authentication;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Samples.AspNetAuthentication.Pages;

[Authorize]
public class ProfileModel : PageModel
{
    public IList<Claim> Claims { get; set; } = [];

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
    }
}
