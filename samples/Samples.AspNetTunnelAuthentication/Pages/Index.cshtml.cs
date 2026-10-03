// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto.Authentication;
using idunno.Bluesky;
using idunno.Bluesky.AspNet.Authentication;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Samples.AspNetTunnelAuthentication.Pages;

/// <summary>
/// Displays the authenticated session and allows its credentials to be refreshed.
/// </summary>
/// <param name="agentFactory">The factory used to create agents for the authenticated user.</param>
[Authorize]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class IndexModel(BlueskyAgentFactory agentFactory) : PageModel
{
    /// <summary>
    /// Gets the authenticated user's DID.
    /// </summary>
    public string? Did { get; private set; }

    /// <summary>
    /// Gets the access token's expiration time.
    /// </summary>
    public DateTimeOffset? ExpiresOn { get; private set; }

    /// <summary>
    /// Gets the client signing key ID recorded for the session.
    /// </summary>
    public string? ClientSigningKeyId { get; private set; }

    /// <summary>
    /// Gets or sets the outcome of a successful credential refresh.
    /// </summary>
    [TempData]
    public string? StatusMessage { get; set; }

    /// <summary>
    /// Displays the current session without refreshing its credentials.
    /// </summary>
    public void OnGet()
    {
        using BlueskyAgent agent = agentFactory.CreateAgent();
        SetSessionDetails(agent);
    }

    /// <summary>
    /// Refreshes and persists the current session's credentials.
    /// </summary>
    /// <returns>A redirect to the updated session, or the page with a refresh failure.</returns>
    public async Task<IActionResult> OnPostRefreshAsync()
    {
        using BlueskyAgent agent = agentFactory.CreateAgent();

        if (!await agent.RefreshCredentials(HttpContext.RequestAborted))
        {
            SetSessionDetails(agent);
            ModelState.AddModelError(string.Empty, "Credential refresh failed. Check the application logs for details; you may need to sign in again.");

            return Page();
        }

        StatusMessage = "Credentials refreshed successfully.";

        return RedirectToPage();
    }

    private void SetSessionDetails(BlueskyAgent agent)
    {
        Did = User.FindFirst(AtProtoClaims.Did)?.Value;
        ExpiresOn = agent.Credentials?.ExpiresOn;
        ClientSigningKeyId = (agent.Credentials as DPoPAccessCredentials)?.ClientSigningKeyId;
    }
}
