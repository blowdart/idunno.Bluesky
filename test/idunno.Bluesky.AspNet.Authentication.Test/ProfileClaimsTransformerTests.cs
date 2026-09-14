// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Security.Claims;

using idunno.AtProto;
using idunno.AtProto.Authentication;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace idunno.Bluesky.AspNet.Authentication.Test;

public class ProfileClaimsTransformerTests
{
    private static ProfileClaimsTransformer CreateTransformer()
    {
        ServiceCollection services = new();

        services.AddLogging();
        services.AddOptions<ProfileClaimsTransformerOptions>();
        services.AddOptions<BlueskyAgentOptions>();
        services.AddOptions<BlueskyAuthenticationOptions>();

        ServiceProvider provider = services.BuildServiceProvider();

        return new ProfileClaimsTransformer(
            NullLoggerFactory.Instance,
            provider.GetRequiredService<IOptionsMonitor<ProfileClaimsTransformerOptions>>(),
            provider.GetRequiredService<IOptionsMonitor<BlueskyAgentOptions>>(),
            provider.GetRequiredService<IOptionsMonitor<BlueskyAuthenticationOptions>>(),
            null);
    }

    [Fact]
    public async Task TransformThrowsWhenThePrincipalIsNull()
    {
        ProfileClaimsTransformer transformer = CreateTransformer();

        await Assert.ThrowsAsync<ArgumentNullException>(() => transformer.TransformAsync(null!));
    }

    [Fact]
    public async Task APrincipalWhichHasAlreadyBeenTransformedIsReturnedUnchanged()
    {
        // Claims transformation can run more than once for a request. Without the marker claim a principal would be
        // supplemented again on every run, accumulating a duplicate set of profile claims each time.
        ProfileClaimsTransformer transformer = CreateTransformer();

        Did did = TestData.NewDid();
        ClaimsPrincipal principal = new(new ClaimsIdentity(
            [
                new Claim(AtProtoClaims.Did, did),
                new Claim(AtProtoClaims.AccessToken, "access-token"),
                new Claim(AtProtoClaims.RefreshToken, "refresh-token"),
                new Claim(AtProtoClaims.DPoPProof, "proof"),
                new Claim(AtProtoClaims.DPoPNonce, "nonce"),
                new Claim(ProfileClaimsTransformer.ProfileClaimsAppliedClaimType, "true", ClaimValueTypes.Boolean),
            ],
            "Bluesky"));

        ClaimsPrincipal result = await transformer.TransformAsync(principal);

        Assert.Same(principal, result);
    }

    [Fact]
    public async Task APrincipalWithoutAtProtoCredentialsIsReturnedUnchanged()
    {
        ProfileClaimsTransformer transformer = CreateTransformer();

        ClaimsPrincipal principal = new(new ClaimsIdentity(
            [new Claim(System.Security.Claims.ClaimTypes.Name, "someone")],
            "Bluesky"));

        Assert.Same(principal, await transformer.TransformAsync(principal));
    }

    [Fact]
    public async Task AnUnauthenticatedPrincipalIsReturnedUnchanged()
    {
        ProfileClaimsTransformer transformer = CreateTransformer();

        ClaimsPrincipal principal = new();

        Assert.Same(principal, await transformer.TransformAsync(principal));
    }

    [Fact]
    public void TheMarkerClaimTypeIsNamespaced()
    {
        // The marker travels in the principal, so it needs to be something an application will not collide with.
        Assert.Equal("urn:bluesky:aspnet:profileclaimsapplied", ProfileClaimsTransformer.ProfileClaimsAppliedClaimType);
    }
}
