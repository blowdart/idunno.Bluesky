// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using Microsoft.AspNetCore.Authentication;

namespace idunno.Bluesky.AspNet.Authentication;

internal static class AuthenticateResults
{
    internal static readonly AuthenticateResult s_failedUnprotectingTicket = AuthenticateResult.Fail("Unprotect ticket failed");
    internal static readonly AuthenticateResult s_missingDidInCookie = AuthenticateResult.Fail("Did missing in cookie");
    internal static readonly AuthenticateResult s_invalidDidInCookie = AuthenticateResult.Fail("Did in cookie is not a valid DID");
    internal static readonly AuthenticateResult s_missingIdentityInStore = AuthenticateResult.Fail("Identity missing in identity store");
    internal static readonly AuthenticateResult s_expiredTicket = AuthenticateResult.Fail("Ticket expired");
    internal static readonly AuthenticateResult s_noPrincipal = AuthenticateResult.Fail("No principal.");
    internal static readonly AuthenticateResult s_tokenRefreshFailed = AuthenticateResult.Fail("Token refresh failed.");
    internal static readonly AuthenticateResult s_identityStoreRefreshMissing = AuthenticateResult.Fail("Identity missing in identity store after token refresh");
    internal static readonly AuthenticateResult s_awaitTokenRefreshLoopExpired = AuthenticateResult.Fail("Token refresh check loop expired.");
    internal static readonly AuthenticateResult s_cancellationRequested = AuthenticateResult.Fail("Request cancelled.");

    /// <summary>
    /// Returns a short, lower case tag value describing <paramref name="result"/>, for use as the value of the
    /// <see cref="BlueskyAuthenticationMetrics.AuthenticationResultTagName"/> tag.
    /// </summary>
    /// <remarks>
    /// <para>
    ///   Every failure the handler produces is one of the cached instances above, so they are matched by reference
    ///   rather than by their message. Anything else, which can only come from an event handler replacing the result,
    ///   is reported as a single <c>failure</c> value so an application cannot drive the cardinality of the tag.
    /// </para>
    /// </remarks>
    /// <param name="result">The <see cref="AuthenticateResult"/> to describe.</param>
    /// <returns>A short, lower case description of <paramref name="result"/>.</returns>
    internal static string ReasonFor(AuthenticateResult result)
    {
        if (result.Succeeded)
        {
            return "success";
        }

        if (ReferenceEquals(result, s_failedUnprotectingTicket))
        {
            return "unprotect_ticket_failed";
        }

        if (ReferenceEquals(result, s_missingDidInCookie))
        {
            return "did_missing_in_cookie";
        }

        if (ReferenceEquals(result, s_invalidDidInCookie))
        {
            return "invalid_did_in_cookie";
        }

        if (ReferenceEquals(result, s_missingIdentityInStore))
        {
            return "identity_missing_in_store";
        }

        if (ReferenceEquals(result, s_expiredTicket))
        {
            return "ticket_expired";
        }

        if (ReferenceEquals(result, s_noPrincipal))
        {
            return "no_principal";
        }

        if (ReferenceEquals(result, s_tokenRefreshFailed))
        {
            return "token_refresh_failed";
        }

        if (ReferenceEquals(result, s_identityStoreRefreshMissing))
        {
            return "identity_missing_after_refresh";
        }

        if (ReferenceEquals(result, s_awaitTokenRefreshLoopExpired))
        {
            return "token_refresh_wait_expired";
        }

        if (ReferenceEquals(result, s_cancellationRequested))
        {
            return "request_cancelled";
        }

        return "failure";
    }
}
