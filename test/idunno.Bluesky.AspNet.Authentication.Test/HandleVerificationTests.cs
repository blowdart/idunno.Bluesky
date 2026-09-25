// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;

using idunno.AtProto;
using idunno.AtProto.Authentication;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace idunno.Bluesky.AspNet.Authentication.Test;

/// <summary>
/// Covers the verification of the handle a profile carries before it becomes a claim.
/// </summary>
/// <remarks>
/// <para>
///   A profile is served by the user's own personal data server, which can put any handle it likes in it. An
///   application which shows or authorizes on the name claim would be taking that server's word for who the user is,
///   so the handle is checked against the directory and the handle owner before it is presented.
/// </para>
/// </remarks>
[ExcludeFromCodeCoverage]
public class HandleVerificationTests
{
    private const string Handle = "test.invalid";

    private static BlueskyClaimsTransformer CreateTransformer(
        IHttpClientFactory httpClientFactory,
        Action<BlueskyClaimsTransformerOptions>? configureTransformerOptions = null)
    {
        ServiceCollection services = new();

        services.AddLogging();
        services.AddMetrics();
        services.Configure<BlueskyClaimsTransformerOptions>(options =>
        {
            options.Cache = new EmptyProfileCache();
            configureTransformerOptions?.Invoke(options);
        });
        services.AddOptions<BlueskyAgentOptions>();
        services.AddOptions<BlueskyAuthenticationOptions>();

        ServiceProvider provider = services.BuildServiceProvider();

        return new BlueskyClaimsTransformer(
            NullLoggerFactory.Instance,
            provider.GetRequiredService<IOptionsMonitor<BlueskyClaimsTransformerOptions>>(),
            provider.GetRequiredService<IOptionsMonitor<BlueskyAgentOptions>>(),
            provider.GetRequiredService<IOptionsMonitor<BlueskyAuthenticationOptions>>(),
            provider.GetRequiredService<System.Diagnostics.Metrics.IMeterFactory>(),
            httpClientFactory);
    }

    [Fact]
    public async Task AHandleWhichVerifiesBecomesTheNameClaim()
    {
        Did did = TestData.NewDid();

        await using FakePds pds = await FakePds.Create(profile: (Handle, did), directoryHandle: Handle);

        BlueskyClaimsTransformer transformer = CreateTransformer(pds.HttpClientFactory);

        ClaimsPrincipal result = await transformer.TransformAsync(
            new ClaimsPrincipal(TestData.AuthenticatedClaimsIdentity(did, signableProofKey: true)));

        Assert.Equal(Handle, result.FindFirst(idunno.Bluesky.ClaimTypes.Handle)?.Value);
        Assert.Equal(Handle, result.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value);
    }

    [Fact]
    public async Task AHandleTheDirectoryDoesNotAgreeWithIsDropped()
    {
        Did did = TestData.NewDid();

        // The directory says this DID is known as something else, so the personal data server is asserting a handle
        // its owner never pointed at this DID.
        await using FakePds pds = await FakePds.Create(profile: (Handle, did), directoryHandle: "someone.else.invalid");

        BlueskyClaimsTransformer transformer = CreateTransformer(pds.HttpClientFactory);

        ClaimsPrincipal result = await transformer.TransformAsync(
            new ClaimsPrincipal(TestData.AuthenticatedClaimsIdentity(did, signableProofKey: true)));

        Assert.Null(result.FindFirst(idunno.Bluesky.ClaimTypes.Handle));
        Assert.Null(result.FindFirst(System.Security.Claims.ClaimTypes.Name));

        // The rest of the transformation still ran, so the application still gets a supplemented principal.
        Assert.NotNull(result.FindFirst(BlueskyClaimsTransformer.ProfileClaimsAppliedClaimType));
    }

    [Fact]
    public async Task AnUnverifiableHandleIsCounted()
    {
        Did did = TestData.NewDid();

        await using FakePds pds = await FakePds.Create(profile: (Handle, did), directoryHandle: "someone.else.invalid");

        ServiceCollection services = new();

        services.AddLogging();
        services.AddMetrics();
        services.Configure<BlueskyClaimsTransformerOptions>(options => options.Cache = new EmptyProfileCache());
        services.AddOptions<BlueskyAgentOptions>();
        services.AddOptions<BlueskyAuthenticationOptions>();

        using ServiceProvider provider = services.BuildServiceProvider();
        System.Diagnostics.Metrics.IMeterFactory meterFactory =
            provider.GetRequiredService<System.Diagnostics.Metrics.IMeterFactory>();

        using Microsoft.Extensions.Diagnostics.Metrics.Testing.MetricCollector<long> collector = new(
            meterFactory,
            BlueskyAuthenticationMetrics.MeterName,
            "idunno.bluesky.aspnet.authentication.handleverification.failures.total");

        BlueskyClaimsTransformer transformer = new(
            NullLoggerFactory.Instance,
            provider.GetRequiredService<IOptionsMonitor<BlueskyClaimsTransformerOptions>>(),
            provider.GetRequiredService<IOptionsMonitor<BlueskyAgentOptions>>(),
            provider.GetRequiredService<IOptionsMonitor<BlueskyAuthenticationOptions>>(),
            meterFactory,
            pds.HttpClientFactory);

        await transformer.TransformAsync(
            new ClaimsPrincipal(TestData.AuthenticatedClaimsIdentity(did, signableProofKey: true)));

        Assert.Equal(1, collector.GetMeasurementSnapshot().Sum(measurement => measurement.Value));
    }

    [Fact]
    public async Task VerificationCanBeTurnedOff()
    {
        Did did = TestData.NewDid();

        await using FakePds pds = await FakePds.Create(profile: (Handle, did), directoryHandle: "someone.else.invalid");

        BlueskyClaimsTransformer transformer = CreateTransformer(
            pds.HttpClientFactory,
            options => options.VerifyHandle = false);

        ClaimsPrincipal result = await transformer.TransformAsync(
            new ClaimsPrincipal(TestData.AuthenticatedClaimsIdentity(did, signableProofKey: true)));

        Assert.Equal(Handle, result.FindFirst(idunno.Bluesky.ClaimTypes.Handle)?.Value);
    }

    [Fact]
    public void VerificationIsOnByDefault()
    {
        Assert.True(new BlueskyClaimsTransformerOptions().VerifyHandle);
    }

    /// <summary>
    /// A cache which starts empty, so every transformation runs the cache miss path the verification lives on, and
    /// which is per instance, so tests do not see each other's entries.
    /// </summary>
    private sealed class EmptyProfileCache : IProfileCache
    {
        private readonly Dictionary<string, ProfileCacheEntry> _entries = [];

        public Task Add(Did did, ProfileCacheEntry profile)
        {
            ArgumentNullException.ThrowIfNull(did);

            _entries[did.Value] = profile;

            return Task.CompletedTask;
        }

        public Task<ProfileCacheEntry?> GetCachedValue(Did did)
        {
            ArgumentNullException.ThrowIfNull(did);

            return Task.FromResult(_entries.TryGetValue(did.Value, out ProfileCacheEntry? entry) ? entry : null);
        }
    }
}
