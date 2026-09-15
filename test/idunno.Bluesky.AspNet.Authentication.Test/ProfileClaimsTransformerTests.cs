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
    private static BlueskyClaimsTransformer CreateTransformer(
        Action<BlueskyClaimsTransformerOptions>? configureTransformerOptions = null,
        IHttpClientFactory? httpClientFactory = null)
    {
        ServiceCollection services = new();

        services.AddLogging();

        if (configureTransformerOptions is not null)
        {
            services.Configure(configureTransformerOptions);
        }
        else
        {
            services.AddOptions<BlueskyClaimsTransformerOptions>();
        }

        services.AddOptions<BlueskyAgentOptions>();
        services.AddOptions<BlueskyAuthenticationOptions>();

        ServiceProvider provider = services.BuildServiceProvider();

        return new BlueskyClaimsTransformer(
            NullLoggerFactory.Instance,
            provider.GetRequiredService<IOptionsMonitor<BlueskyClaimsTransformerOptions>>(),
            provider.GetRequiredService<IOptionsMonitor<BlueskyAgentOptions>>(),
            provider.GetRequiredService<IOptionsMonitor<BlueskyAuthenticationOptions>>(),
            null,
            httpClientFactory);
    }

    [Fact]
    public async Task TransformThrowsWhenThePrincipalIsNull()
    {
        BlueskyClaimsTransformer transformer = CreateTransformer();

        await Assert.ThrowsAsync<ArgumentNullException>(() => transformer.TransformAsync(null!));
    }

    [Fact]
    public async Task APrincipalWhichHasAlreadyBeenTransformedIsReturnedUnchanged()
    {
        // Claims transformation can run more than once for a request. Without the marker claim a principal would be
        // supplemented again on every run, accumulating a duplicate set of profile claims each time.
        BlueskyClaimsTransformer transformer = CreateTransformer();

        Did did = TestData.NewDid();
        ClaimsPrincipal principal = new(new ClaimsIdentity(
            [
                new Claim(AtProtoClaims.Did, did),
                new Claim(AtProtoClaims.AccessToken, "access-token"),
                new Claim(AtProtoClaims.RefreshToken, "refresh-token"),
                new Claim(AtProtoClaims.DPoPProof, "proof"),
                new Claim(AtProtoClaims.DPoPNonce, "nonce"),
                new Claim(BlueskyClaimsTransformer.ProfileClaimsAppliedClaimType, "true", ClaimValueTypes.Boolean),
            ],
            "Bluesky"));

        ClaimsPrincipal result = await transformer.TransformAsync(principal);

        Assert.Same(principal, result);
    }

    [Fact]
    public async Task APrincipalWithoutAtProtoCredentialsIsReturnedUnchanged()
    {
        BlueskyClaimsTransformer transformer = CreateTransformer();

        ClaimsPrincipal principal = new(new ClaimsIdentity(
            [new Claim(System.Security.Claims.ClaimTypes.Name, "someone")],
            "Bluesky"));

        Assert.Same(principal, await transformer.TransformAsync(principal));
    }

    [Fact]
    public async Task AnUnauthenticatedPrincipalIsReturnedUnchanged()
    {
        BlueskyClaimsTransformer transformer = CreateTransformer();

        ClaimsPrincipal principal = new();

        Assert.Same(principal, await transformer.TransformAsync(principal));
    }

    [Fact]
    public void TheMarkerClaimTypeIsNamespaced()
    {
        // The marker travels in the principal, so it needs to be something an application will not collide with.
        Assert.Equal("urn:bluesky:aspnet:profileclaimsapplied", BlueskyClaimsTransformer.ProfileClaimsAppliedClaimType);
    }

    [Fact]
    public async Task AProfileServedFromTheCacheIsCountedAsAHit()
    {
        // Hits are only meaningful next to misses, which were already counted. Without both, a cache which never
        // serves anything looks exactly like a cache which is never asked.
        ServiceCollection services = new();

        services.AddLogging();
        services.AddMetrics();
        services.Configure<BlueskyClaimsTransformerOptions>(options => options.Cache = new StubProfileCache());
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

        BlueskyClaimsTransformer transformer = new(
            NullLoggerFactory.Instance,
            provider.GetRequiredService<IOptionsMonitor<BlueskyClaimsTransformerOptions>>(),
            provider.GetRequiredService<IOptionsMonitor<BlueskyAgentOptions>>(),
            provider.GetRequiredService<IOptionsMonitor<BlueskyAuthenticationOptions>>(),
            meterFactory);

        Did did = TestData.NewDid();

        ClaimsPrincipal result = await transformer.TransformAsync(
            new ClaimsPrincipal(TestData.AuthenticatedClaimsIdentity(did)));

        // The cache answered, so the transformer supplemented the principal without going to the PDS.
        Assert.True(result.HasClaim(claim => claim.Type == BlueskyClaimsTransformer.ProfileClaimsAppliedClaimType));

        Assert.Equal(1, Assert.Single(hits.GetMeasurementSnapshot()).Value);
        Assert.Empty(misses.GetMeasurementSnapshot());
    }

    [Fact]
    public async Task APrincipalWhoseCredentialsHaveExpiredIsReturnedUnchangedWithoutReachingThePds()
    {
        // The expiry check is the agent's own IsAuthenticated condition, made before an agent exists so that a
        // principal which cannot be used costs nothing.
        CountingHttpClientFactory httpClientFactory = new();

        Did did = TestData.NewDid();

        ClaimsPrincipal principal = new(new ClaimsIdentity(
            [
                new Claim(AtProtoClaims.Did, did, ClaimValueTypes.String, "https://bsky.social"),
                new Claim(AtProtoClaims.AccessToken, TestData.Jwt(did, TimeSpan.FromHours(-1)), ClaimValueTypes.String, "https://bsky.social"),
                new Claim(AtProtoClaims.RefreshToken, TestData.Jwt(did), ClaimValueTypes.String, "https://bsky.social"),
                new Claim(AtProtoClaims.DPoPProof, "proof-key", ClaimValueTypes.String, "https://bsky.social"),
                new Claim(AtProtoClaims.DPoPNonce, "nonce", ClaimValueTypes.String, "https://bsky.social"),
            ],
            "Bluesky"));

        Assert.Same(principal, await CreateTransformer(httpClientFactory: httpClientFactory).TransformAsync(principal));
        Assert.Equal(0, httpClientFactory.ClientsCreated);
    }

    [Fact]
    public async Task AProfileServedFromTheCacheIsReturnedWithoutReachingThePds()
    {
        // Transformation runs on every request, so a cache hit has to answer without a profile lookup. The cache is
        // also consulted before an agent is built, which this cannot see directly because an agent does not ask for a
        // client until it makes a request.
        CountingHttpClientFactory httpClientFactory = new();

        BlueskyClaimsTransformer transformer = CreateTransformer(
            configureTransformerOptions: options => options.Cache = new StubProfileCache(),
            httpClientFactory: httpClientFactory);

        ClaimsPrincipal result = await transformer.TransformAsync(
            new ClaimsPrincipal(TestData.AuthenticatedClaimsIdentity(TestData.NewDid())));

        Assert.True(result.HasClaim(claim => claim.Type == BlueskyClaimsTransformer.ProfileClaimsAppliedClaimType));
        Assert.Equal(0, httpClientFactory.ClientsCreated);
    }

    [Fact]
    public async Task TheSuppliedHttpClientFactoryIsUsedWhenTheProfileHasToBeFetched()
    {
        // Without a factory the agent builds a service provider, and so a connection pool, of its own, once per
        // request, because the transformer is registered as transient.
        CountingHttpClientFactory httpClientFactory = new();

        BlueskyClaimsTransformer transformer = CreateTransformer(
            configureTransformerOptions: options => options.Cache = new EmptyProfileCache(),
            httpClientFactory: httpClientFactory);

        ClaimsPrincipal principal = new(TestData.AuthenticatedClaimsIdentity(TestData.NewDid(), signableProofKey: true));

        // The factory answers everything with a 404, so the profile lookup fails and the principal comes back
        // unchanged. What matters here is which client the attempt was made with.
        Assert.Same(principal, await transformer.TransformAsync(principal));
        Assert.True(httpClientFactory.ClientsCreated > 0);
    }

    private sealed class CountingHttpClientFactory : IHttpClientFactory
    {
        private int _clientsCreated;

        internal int ClientsCreated => Volatile.Read(ref _clientsCreated);

        public HttpClient CreateClient(string name)
        {
            Interlocked.Increment(ref _clientsCreated);

            return new HttpClient(new NotFoundHandler());
        }

        private sealed class NotFoundHandler : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
                Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.NotFound)
                {
                    RequestMessage = request,
                    Content = new StringContent(string.Empty),
                });
        }
    }

    private sealed class EmptyProfileCache : IProfileCache
    {
        public Task Add(Did did, ProfileCacheEntry profile) => Task.CompletedTask;

        public Task<ProfileCacheEntry?> GetCachedValue(Did did) => Task.FromResult<ProfileCacheEntry?>(null);
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
