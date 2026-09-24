// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.Bluesky.AspNet.Authentication.Events;

using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace idunno.Bluesky.AspNet.Authentication.Test;

/// <summary>
/// Covers how <c>AddBluesky</c> and <see cref="PostConfigureBlueskyAuthenticationOptions"/> build the options for a scheme.
/// </summary>
/// <remarks>
/// <see cref="BlueskyAuthenticationOptions"/> are registered named by authentication scheme, including for the default
/// scheme name, so the unnamed instance behind <see cref="IOptions{TOptions}.Value"/> and
/// <see cref="IOptionsMonitor{TOptions}.CurrentValue"/> never carries an application's configuration. Anything which needs
/// the configured options has to ask for them by scheme.
/// </remarks>
public class BlueskyAuthenticationOptionsConfigurationTests
{
    private static IOptionsMonitor<BlueskyAuthenticationOptions> BuildOptions(
        string scheme,
        Action<BlueskyAuthenticationOptions> configureOptions)
    {
        ServiceCollection services = new();

        services.AddLogging();
        services.AddDataProtection();
        services.AddAuthentication().AddBluesky(scheme, configureOptions);

        return services.BuildServiceProvider().GetRequiredService<IOptionsMonitor<BlueskyAuthenticationOptions>>();
    }

    [Theory]
    [InlineData("Bluesky")]
    [InlineData("CustomScheme")]
    public void OptionsConfiguredForASchemeAreOnlyVisibleThroughTheNamedInstance(string scheme)
    {
        IOptionsMonitor<BlueskyAuthenticationOptions> monitor =
            BuildOptions(scheme, options => options.ExpireTimeSpan = TimeSpan.FromDays(3));

        Assert.Equal(TimeSpan.FromDays(3), monitor.Get(scheme).ExpireTimeSpan);

        // The unnamed instance is a different object which never saw the delegate, even when the scheme is the default
        // one. A constructor taking IOptions<BlueskyAuthenticationOptions> would only ever see this.
        Assert.NotSame(monitor.Get(scheme), monitor.CurrentValue);
        Assert.NotEqual(TimeSpan.FromDays(3), monitor.CurrentValue.ExpireTimeSpan);
    }

    [Fact]
    public void EachSchemeGetsItsOwnOptionsInstance()
    {
        ServiceCollection services = new();

        services.AddLogging();
        services.AddDataProtection();
        services.AddAuthentication()
            .AddBluesky("First", options => options.ExpireTimeSpan = TimeSpan.FromDays(1))
            .AddBluesky("Second", options => options.ExpireTimeSpan = TimeSpan.FromDays(2));

        IOptionsMonitor<BlueskyAuthenticationOptions> monitor =
            services.BuildServiceProvider().GetRequiredService<IOptionsMonitor<BlueskyAuthenticationOptions>>();

        Assert.Equal(TimeSpan.FromDays(1), monitor.Get("First").ExpireTimeSpan);
        Assert.Equal(TimeSpan.FromDays(2), monitor.Get("Second").ExpireTimeSpan);
        Assert.NotSame(monitor.Get("First").IdentityStore, monitor.Get("Second").IdentityStore);
    }

    [Fact]
    public void TheDefaultIdentityStoreAndCorrelationCacheAreGivenTheConfiguredEvents()
    {
        IdentityStoreEvents identityStoreEvents = new();
        CorrelationStateCacheEvents correlationStateCacheEvents = new();

        IOptionsMonitor<BlueskyAuthenticationOptions> monitor = BuildOptions("CustomScheme", options =>
        {
            options.IdentityStoreEvents = identityStoreEvents;
            options.CorrelationStateCacheEvents = correlationStateCacheEvents;
        });

        BlueskyAuthenticationOptions options = monitor.Get("CustomScheme");

        Assert.NotNull(options.IdentityStore);
        Assert.NotNull(options.CorrelationStateCache);
        Assert.Same(identityStoreEvents, options.IdentityStore.Events);
        Assert.Same(correlationStateCacheEvents, options.CorrelationStateCache.Events);
    }

    [Fact]
    public void AnApplicationSuppliedIdentityStoreAndCorrelationCacheAreAlsoGivenTheConfiguredEvents()
    {
        IdentityStoreEvents identityStoreEvents = new();
        CorrelationStateCacheEvents correlationStateCacheEvents = new();

        IIdentityStore identityStore = new EphemeralIdentityStore(NullLoggerFactory.Instance);
        ICorrelationStateCache correlationCache = new DistributedCacheCorrelationStateCache(TestData.DistributedCache());

        IOptionsMonitor<BlueskyAuthenticationOptions> monitor = BuildOptions("CustomScheme", options =>
        {
            options.IdentityStore = identityStore;
            options.CorrelationStateCache = correlationCache;
            options.IdentityStoreEvents = identityStoreEvents;
            options.CorrelationStateCacheEvents = correlationStateCacheEvents;
        });

        BlueskyAuthenticationOptions options = monitor.Get("CustomScheme");

        Assert.Same(identityStore, options.IdentityStore);
        Assert.Same(correlationCache, options.CorrelationStateCache);
        Assert.Same(identityStoreEvents, identityStore.Events);
        Assert.Same(correlationStateCacheEvents, correlationCache.Events);
    }

    [Fact]
    public void IdentityStoreEntryTimeToLiveFollowsTheCookieLifetimeWhenItIsNotConfigured()
    {
        IOptionsMonitor<BlueskyAuthenticationOptions> monitor =
            BuildOptions("CustomScheme", options => options.ExpireTimeSpan = TimeSpan.FromDays(21));

        // An identity store which dropped its entries before the cookie expired would sign users out part way
        // through the lifetime of a cookie the handler is still honouring.
        Assert.Equal(TimeSpan.FromDays(21), monitor.Get("CustomScheme").IdentityStoreEntryTimeToLive);
    }

    [Fact]
    public void AnExplicitIdentityStoreEntryTimeToLiveIsKept()
    {
        IOptionsMonitor<BlueskyAuthenticationOptions> monitor = BuildOptions("CustomScheme", options =>
        {
            options.ExpireTimeSpan = TimeSpan.FromDays(21);
            options.IdentityStoreEntryTimeToLive = TimeSpan.FromDays(30);
        });

        Assert.Equal(TimeSpan.FromDays(30), monitor.Get("CustomScheme").IdentityStoreEntryTimeToLive);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void RefreshCheckWaitMustBeGreaterThanZero(int seconds)
    {
        IOptionsMonitor<BlueskyAuthenticationOptions> monitor =
            BuildOptions("CustomScheme", options => options.RefreshCheckWait = TimeSpan.FromSeconds(seconds));

        // A zero wait spins through every check without giving the refresh holder time to finish, and Task.Delay
        // throws on a negative one, so neither should reach a request.
        Assert.Throws<ArgumentOutOfRangeException>(() => monitor.Get("CustomScheme"));
    }

    [Fact]
    public void RefreshCheckWaitDefaultsToTwoAndAHalfSeconds()
    {
        IOptionsMonitor<BlueskyAuthenticationOptions> monitor = BuildOptions("CustomScheme", _ => { });

        BlueskyAuthenticationOptions options = monitor.Get("CustomScheme");

        Assert.Equal(TimeSpan.FromSeconds(2.5), options.RefreshCheckWait);
        Assert.Equal(5, options.MaxRefreshChecks);
    }

    [Fact]
    public void TheAuthenticationCookieIsNamedAfterTheSchemeAndIsNotReadableByScript()
    {
        IOptionsMonitor<BlueskyAuthenticationOptions> monitor = BuildOptions("Custom Scheme", _ => { });

        BlueskyAuthenticationOptions options = monitor.Get("Custom Scheme");

        Assert.Equal($"{BlueskyAuthenticationDefaults.CookiePrefix}Custom%20Scheme", options.Cookie.Name);
        Assert.True(options.Cookie.HttpOnly);
        Assert.True(options.Cookie.IsEssential);
    }

    [Fact]
    public void AnExplicitAuthenticationCookieNameIsKept()
    {
        IOptionsMonitor<BlueskyAuthenticationOptions> monitor =
            BuildOptions("CustomScheme", options => options.Cookie.Name = "my-cookie");

        Assert.Equal("my-cookie", monitor.Get("CustomScheme").Cookie.Name);
    }

    [Fact]
    public void TheCorrelationCookieIsNotReadableByScriptAndIsSameSiteLax()
    {
        IOptionsMonitor<BlueskyAuthenticationOptions> monitor = BuildOptions("CustomScheme", _ => { });

        BlueskyAuthenticationOptions options = monitor.Get("CustomScheme");

        // The correlation cookie is named per scheme, like the authentication cookie, so two Bluesky schemes in one
        // application cannot overwrite each other's logins.
        Assert.Equal($"{Constants.CorrelationCookieName}.CustomScheme", options.CorrelationCookie.Name);
        Assert.True(options.CorrelationCookie.HttpOnly);
        Assert.True(options.CorrelationCookie.IsEssential);
        Assert.Equal(Microsoft.AspNetCore.Http.SameSiteMode.Lax, options.CorrelationCookie.SameSite);
    }

    [Fact]
    public void AConfiguredCorrelationCookieNameIsLeftAlone()
    {
        IOptionsMonitor<BlueskyAuthenticationOptions> monitor =
            BuildOptions("CustomScheme", options => options.CorrelationCookie.Name = "my-correlation-cookie");

        Assert.Equal("my-correlation-cookie", monitor.Get("CustomScheme").CorrelationCookie.Name);
    }

    [Fact]
    public void DefaultsAreSuppliedForTheCollaboratorsTheHandlerNeeds()
    {
        IOptionsMonitor<BlueskyAuthenticationOptions> monitor = BuildOptions("CustomScheme", _ => { });

        BlueskyAuthenticationOptions options = monitor.Get("CustomScheme");

        Assert.NotNull(options.DataProtectionProvider);
        Assert.NotNull(options.TicketDataFormat);
        Assert.NotNull(options.CookieManager);
        Assert.NotNull(options.IdentityStore);
        Assert.NotNull(options.CorrelationStateCache);
        Assert.Equal(CookieAuthenticationDefaults.LoginPath, options.LoginPath);
        Assert.Equal(CookieAuthenticationDefaults.LogoutPath, options.LogoutPath);
        Assert.Equal(CookieAuthenticationDefaults.AccessDeniedPath, options.AccessDeniedPath);
    }

    [Fact]
    public void CookieExpirationIsRejectedInFavourOfExpireTimeSpan()
    {
        ServiceCollection services = new();

        services.AddLogging();
        services.AddDataProtection();
        services.AddAuthentication().AddBluesky(
            "CustomScheme",
            options => options.Cookie.Expiration = TimeSpan.FromDays(1));

        IOptionsMonitor<BlueskyAuthenticationOptions> monitor =
            services.BuildServiceProvider().GetRequiredService<IOptionsMonitor<BlueskyAuthenticationOptions>>();

        Assert.Throws<OptionsValidationException>(() => monitor.Get("CustomScheme"));
    }

    [Fact]
    public void PostConfigureThrowsWhenTheOptionsAreNull()
    {
        PostConfigureBlueskyAuthenticationOptions postConfigure =
            new(new Microsoft.AspNetCore.DataProtection.EphemeralDataProtectionProvider(), NullLoggerFactory.Instance);

        Assert.Throws<ArgumentNullException>(() => postConfigure.PostConfigure("CustomScheme", null!));
    }
}
