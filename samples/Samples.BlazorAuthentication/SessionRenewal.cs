// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Globalization;
using System.Security.Claims;

using idunno.AtProto.Authentication;
using idunno.Bluesky.AspNet.Authentication;
using idunno.Bluesky.AspNet.Authentication.Events;

using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;

namespace Samples.BlazorAuthentication;

internal sealed class SessionRenewal(
    IDataProtectionProvider protection)
{
    private static readonly object s_expiryKey = new();

    internal static Task CheckSlidingExpiration(BlueskySlidingExpirationContext context)
    {
        if (context.ShouldRenew && context.Request.Path == "/session/renew")
        {
            // RequestRefresh samples time immediately after this event. This conservative expiry
            // never outlives the renewed reference cookie, even when middleware authenticates first.
            var now = (context.Options.TimeProvider ?? TimeProvider.System).GetUtcNow();
            context.HttpContext.Items[s_expiryKey] = now.Add(context.ElapsedTime + context.RemainingTime);
        }

        return Task.CompletedTask;
    }

    internal string ProtectExpiry(ClaimsPrincipal user, DateTimeOffset expiry) =>
        Protector(user).Protect(expiry.UtcTicks.ToString(CultureInfo.InvariantCulture));

    internal DateTimeOffset ReadExpiry(ClaimsPrincipal user, string receipt) =>
        new(long.Parse(Protector(user).Unprotect(receipt), CultureInfo.InvariantCulture), TimeSpan.Zero);

    private IDataProtector Protector(ClaimsPrincipal user) => protection.CreateProtector(
        "Samples.BlazorAuthentication.SessionRenewal.v1",
        user.FindFirstValue(AtProtoClaims.Did) ?? throw new InvalidOperationException("A DID is required."),
        user.FindFirstValue(AtProtoClaims.DPoPProof) ?? throw new InvalidOperationException("A session proof key is required."));

    internal async Task<IResult> RenewAsync(HttpContext context, IAntiforgery antiforgery)
    {
        try
        {
            await antiforgery.ValidateRequestAsync(context);
        }
        catch (AntiforgeryValidationException)
        {
            return Results.BadRequest("Session renewal requires a valid antiforgery token. Reload before retrying.");
        }

        var result = await context.AuthenticateAsync(BlueskyAuthenticationDefaults.AuthenticationScheme);
        if (!result.Succeeded || result.Principal is null ||
            result.Properties?.ExpiresUtc is not DateTimeOffset expiry)
        {
            return Results.Unauthorized();
        }

        // Let the handler renew its DID-reference cookie on response start. SignInAsync would
        // re-add the request's identity, potentially undoing a concurrent logout or replacement.
        if (context.Items[s_expiryKey] is DateTimeOffset renewedExpiry)
        {
            expiry = renewedExpiry;
        }

        return Results.Text(ProtectExpiry(result.Principal, expiry));
    }
}
