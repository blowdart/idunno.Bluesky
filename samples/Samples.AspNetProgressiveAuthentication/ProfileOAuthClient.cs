// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;
using idunno.AtProto.Authentication;
using idunno.Bluesky;
using idunno.Bluesky.AspNet.Authentication;

namespace Samples.AspNetProgressiveAuthentication;

/// <summary>
/// Connects the profile editor to the existing OAuth and correlation APIs.
/// </summary>
/// <param name="manager">The sign-in manager.</param>
/// <param name="clients">The HTTP client factory.</param>
/// <remarks>
/// <para>
/// This adapter uses the library's OAuth validation and ASP.NET correlation storage without signing in automatically.
/// The callback page decides when validated credentials can replace the existing authenticated session.
/// </para>
/// </remarks>
public class ProfileOAuthClient(BlueskySignInManager manager, IHttpClientFactory clients)
{
    /// <summary>
    /// Prepares progressive consent for profile writes and binds its OAuth state to a server-side draft.
    /// </summary>
    /// <param name="handle">The verified account handle used as the authorization login hint.</param>
    /// <param name="credentials">The current credentials used to retain the sample's effective prior permissions.</param>
    /// <param name="editId">The opaque identifier of the account/session-bound pending edit.</param>
    /// <param name="cancellationToken">A cancellation token for discovery and authorization preparation.</param>
    /// <returns>The authorization server's consent URI.</returns>
    /// <remarks>
    /// <para>
    /// The request adds only the profile create/update scopes required by putRecord. The client ID remains fixed,
    /// while the pushed authorization request supplies the expanded subset advertised in client metadata.
    /// </para>
    /// </remarks>
    internal virtual async Task<Uri> Challenge(Handle handle, DPoPAccessCredentials credentials, string editId, CancellationToken cancellationToken)
    {
        using var agent = new BlueskyAgent(httpClientFactory: clients, options: manager.BlueskyAgentOptions);
        OAuthClient oauth = agent.CreateOAuthClient();
        // Reuse the authentication callback builder so localhost consent retains its configured path and browser port.
        Uri callback = manager.CreateReturnUri();
        Uri redirect = await agent.BuildOAuth2LoginUri(oauth, handle,
            scopes: ProfilePermissions.ExpandedScopes(credentials),
            returnUri: callback,
            stateExtraProperties: new() { [ProfilePermissions.PendingEditKey] = editId },
            cancellationToken: cancellationToken);
        // Persist the PKCE/DPoP state using the existing protected, single-use correlation mechanism.
        // Store only the opaque draft identifier here; editor values never travel through the browser or OAuth server.
        await manager.SaveStateAndCreateCorrelationCookie(
            oauth.State ?? throw new OAuthException("Missing OAuth state."), markCookieAsSecure: callback.Scheme == "https");

        return redirect;
    }

    /// <summary>
    /// Validates an OAuth callback and returns credentials without persisting them to the application's identity store.
    /// </summary>
    /// <param name="state">The saved OAuth state already consumed through browser correlation validation.</param>
    /// <param name="callbackData">The callback query containing the authorization response.</param>
    /// <param name="expectedDid">The original account DID for progressive consent, or <see langword="null"/> for initial login.</param>
    /// <param name="cancellationToken">A cancellation token for the authorization response exchange.</param>
    /// <returns>The validated credentials, or <see langword="null"/> when authorization did not succeed.</returns>
    /// <remarks>
    /// <para>
    /// The expected-DID overload rejects another account before changing even the isolated agent's credentials.
    /// The caller still checks required grants and pending-draft ownership before signing in through ASP.NET.
    /// </para>
    /// </remarks>
    internal virtual async Task<DPoPAccessCredentials?> Authorize(OAuthLoginState state, string callbackData, Did? expectedDid, CancellationToken cancellationToken)
    {
        // Do not use the request's factory agent: its credential-update hook writes to the identity store.
        using var agent = new BlueskyAgent(httpClientFactory: clients, options: manager.BlueskyAgentOptions);
        OAuthClient oauth = agent.CreateOAuthClient(state);
        bool authorized = expectedDid is null
            ? await agent.ProcessOAuth2LoginResponse(oauth, callbackData, cancellationToken)
            : await agent.ProcessOAuth2LoginResponse(oauth, callbackData, expectedDid, cancellationToken);

        return authorized ? agent.Credentials as DPoPAccessCredentials : null;
    }
}
