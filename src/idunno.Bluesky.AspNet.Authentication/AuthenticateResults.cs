// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using Microsoft.AspNetCore.Authentication;

namespace idunno.Bluesky.AspNet.Authentication;

internal static class AuthenticateResults
{
    internal static readonly AuthenticateResult s_failedUnprotectingTicket = AuthenticateResult.Fail("Unprotect ticket failed");
    internal static readonly AuthenticateResult s_missingDidInCookie = AuthenticateResult.Fail("Did missing in cookie");
    internal static readonly AuthenticateResult s_missingIdentityInStore = AuthenticateResult.Fail("Identity missing in identity store");
    internal static readonly AuthenticateResult s_expiredTicket = AuthenticateResult.Fail("Ticket expired");
    internal static readonly AuthenticateResult s_noPrincipal = AuthenticateResult.Fail("No principal.");
    internal static readonly AuthenticateResult s_tokenRefreshFailed = AuthenticateResult.Fail("Token refresh failed.");
    internal static readonly AuthenticateResult s_identityStoreRefreshMissing = AuthenticateResult.Fail("Identity missing in identity store after token refresh");
    internal static readonly AuthenticateResult s_awaitTokenRefreshLoopExpired = AuthenticateResult.Fail("Token refresh check loop expired.");

}
