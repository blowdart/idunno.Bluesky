// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.Metrics;
using System.Security.Claims;

using idunno.AtProto;
using idunno.AtProto.Authentication;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;
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

    [Fact]
    public async Task AProfileServedFromTheCacheIsCountedAsAHit()
    {
        // Hits are only meaningful next to misses, which were already counted. Without both, a cache which never
        // serves anything looks exactly like a cache which is never asked.
        ServiceCollection services = new();

        services.AddLogging();
        services.AddMetrics();
        services.Configure<ProfileClaimsTransformerOptions>(options => options.Cache = new StubProfileCache());
        services.AddOptions<BlueskyAgentOptions>();
        services.AddOptions<BlueskyAuthenticationOptions>();

        using ServiceProvider provider = services.BuildServiceProvider();
        IMeterFactory meterFactory = provider.GetRequiredService<IMeterFactory>();

        var hits = new MetricCollector<long>(
            meterFactory,
            BlueskyAuthenticationMetrics.MeterName,
            "idunno.bluesky.aspnet.authentication.profilecache.hits.total");

        var misses = new MetricCollector<long>(
            meterFactory,
            BlueskyAuthenticationMetrics.MeterName,
            "idunno.bluesky.aspnet.authentication.profilecache.misses.total");

        ProfileClaimsTransformer transformer = new(
            NullLoggerFactory.Instance,
            provider.GetRequiredService<IOptionsMonitor<ProfileClaimsTransformerOptions>>(),
            provider.GetRequiredService<IOptionsMonitor<BlueskyAgentOptions>>(),
            provider.GetRequiredService<IOptionsMonitor<BlueskyAuthenticationOptions>>(),
            meterFactory);

        Did did = TestData.NewDid();

        ClaimsPrincipal result = await transformer.TransformAsync(
            new ClaimsPrincipal(TestData.AuthenticatedClaimsIdentity(did)));

        // The cache answered, so the transformer supplemented the principal without going to the PDS.
        Assert.True(result.HasClaim(claim => claim.Type == ProfileClaimsTransformer.ProfileClaimsAppliedClaimType));

        Assert.Equal(1, Assert.Single(hits.GetMeasurementSnapshot()).Value);
        Assert.Empty(misses.GetMeasurementSnapshot());
    }

    private sealed class StubProfileCache : IProfileCache
    {
        public Task Add(Did did, ProfileCacheEntry profile) => Task.CompletedTask;

        public Task<ProfileCacheEntry?> GetCachedValue(Did did) =>
            Task.FromResult<ProfileCacheEntry?>(
                new ProfileCacheEntry(
                    Handle: new Handle("test.bsky.social"),
                    DisplayName: "Test",
                    Description: null,
                    Pronouns: null,
                    Website: null,
                    Avatar: null,
                    Banner: null,
                    Issuer: "https://bsky.social"));
    }
}
