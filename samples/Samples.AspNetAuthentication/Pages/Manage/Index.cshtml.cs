using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

using idunno.AtProto;
using idunno.AtProto.Authentication;
using idunno.Bluesky;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Samples.AspNetAuthentication.Pages.Manage;

[Authorize]
public class IndexModel(BlueskyAgent agent) : PageModel
{
    [Required]
    [StringLength(64)]
    [BindProperty]
    public string DisplayName { get; set; } = string.Empty;

    [Required]
    [StringLength(256)]
    [BindProperty]
    public string Description { get; set; } = string.Empty;

    [Required]
    [StringLength(256)]
    [BindProperty]
    public string Pronouns { get; set; } = string.Empty;

    [ViewData]
    public Cid? Cid { get; set; }

    public async Task<IActionResult> OnGet()
    {
        if (User is not null && User.Identity?.IsAuthenticated == true && User.Identity is ClaimsIdentity && User.Did is not null)
        {
            var getProfileResult = await agent.GetProfile(cancellationToken: HttpContext.RequestAborted);
            getProfileResult.EnsureSucceeded();

            DisplayName = getProfileResult.Result.Value?.DisplayName ?? string.Empty;
            Description = getProfileResult.Result.Value?.Description ?? string.Empty;
            Pronouns = getProfileResult.Result.Value?.Pronouns ?? string.Empty;

            Cid = getProfileResult.Result.Cid.ToString();

            return Page();
        }

        return Forbid();
    }

    public async Task<IActionResult> OnPost(Cid cid)
    {
        if (User is not null && User.Identity?.IsAuthenticated == true && User.Identity is ClaimsIdentity && User.Did is not null)
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            if (cid is null)
            {
                return BadRequest("Missing cid.");
            }

            var getProfileResult = await agent.GetProfile(cancellationToken: HttpContext.RequestAborted);
            getProfileResult.EnsureSucceeded();
            var profile = getProfileResult.Result.Value;

            profile.DisplayName = DisplayName;
            profile.Description = Description;
            profile.Pronouns = Pronouns;

            var updateProfileResult = await agent.UpdateProfile(profile, cid: cid, cancellationToken: HttpContext.RequestAborted);

            updateProfileResult.EnsureSucceeded();
            return RedirectToPage();
        }

        return Forbid();
    }
}
