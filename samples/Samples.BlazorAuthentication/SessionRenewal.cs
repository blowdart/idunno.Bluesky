// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Globalization;
using System.Security.Claims;

using idunno.AtProto;
using idunno.AtProto.Authentication;
using idunno.Bluesky.AspNet.Authentication;

using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;

namespace Samples.BlazorAuthentication;

internal sealed partial class SessionRenewal(
    IDataProtectionProvider protection,
    IOptionsMonitor<BlueskyAuthenticationOptions> options,
    ILogger<SessionRenewal> logger)
{
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
            result.Properties?.IssuedUtc is not DateTimeOffset issued ||
            result.Properties.ExpiresUtc is not DateTimeOffset expiry)
        {
            return Results.Unauthorized();
        }

        var authentication = options.Get(BlueskyAuthenticationDefaults.AuthenticationScheme);
        var now = (authentication.TimeProvider ?? TimeProvider.System).GetUtcNow();
        if (authentication.SlidingExpiration && result.Properties.AllowRefresh != false &&
            now - issued > expiry - now)
        {
            var store = authentication.IdentityStore ?? throw new InvalidOperationException("An identity store is required.");
            var did = new Did(result.Principal.FindFirstValue(AtProtoClaims.Did)!);
            string? refreshLock = await store.StartRefresh(did, context.RequestAborted);
            if (refreshLock is null)
            {
                // Do not overwrite credentials while another request is rotating them. The active browser retries.
                return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
            }

            try
            {
                var identity = await store.GetIdentity(did, context.RequestAborted);
                if (identity is null ||
                    identity.FindFirst(AtProtoClaims.DPoPProof)?.Value != result.Principal.FindFirstValue(AtProtoClaims.DPoPProof))
                {
                    return Results.Unauthorized();
                }

                // Use the handler's sign-in path with current credentials, not the request's earlier snapshot.
                result.Properties.IssuedUtc = now;
                result.Properties.ExpiresUtc = now.Add(expiry - issued);
                await context.SignInAsync(BlueskyAuthenticationDefaults.AuthenticationScheme,
                    new ClaimsPrincipal(identity), result.Properties);
                expiry = result.Properties.ExpiresUtc.Value;
            }
            finally
            {
                if (!await store.EndRefresh(did, refreshLock, CancellationToken.None))
                {
                    RefreshLockLost(logger, did);
                }
            }
        }

        return Results.Text(ProtectExpiry(result.Principal, expiry));
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Session renewal lost its credential refresh lock for {Did}.")]
    private static partial void RefreshLockLost(ILogger logger, Did did);
}
