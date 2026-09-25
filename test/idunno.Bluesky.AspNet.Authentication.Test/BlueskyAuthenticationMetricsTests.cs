// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Reflection;
using System.Security.Claims;

using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;

namespace idunno.Bluesky.AspNet.Authentication.Test;

public class BlueskyAuthenticationMetricsTests
{
    // https://learn.microsoft.com/en-us/dotnet/core/diagnostics/metrics-instrumentation#test-custom-metrics

    private const string Prefix = "idunno.bluesky.aspnet.authentication";

    [Theory]
    [InlineData(nameof(BlueskyAuthenticationMetrics.AccessTokensRefreshed), $"{Prefix}.tokenrefreshes.total")]
    [InlineData(nameof(BlueskyAuthenticationMetrics.AccessTokensRefreshFailures), $"{Prefix}.tokenrefreshfailures.total")]
    [InlineData(nameof(BlueskyAuthenticationMetrics.AccessTokenRefreshWaits), $"{Prefix}.tokenrefreshwaits.total")]
    [InlineData(nameof(BlueskyAuthenticationMetrics.DataProtectionFailures), $"{Prefix}.dataprotection.failures.total")]
    [InlineData(nameof(BlueskyAuthenticationMetrics.ProfileCacheMisses), $"{Prefix}.profilecache.misses.total")]
    [InlineData(nameof(BlueskyAuthenticationMetrics.ProfileCacheHits), $"{Prefix}.profilecache.hits.total")]
    [InlineData(nameof(BlueskyAuthenticationMetrics.SigninsSucceeded), $"{Prefix}.signins.total.successful")]
    [InlineData(nameof(BlueskyAuthenticationMetrics.SigninsFailed), $"{Prefix}.signins.total.failure")]
    [InlineData(nameof(BlueskyAuthenticationMetrics.SignOuts), $"{Prefix}.signouts.total")]
    [InlineData(nameof(BlueskyAuthenticationMetrics.CredentialRevocationFailures), $"{Prefix}.credentialrevocations.failures.total")]
    [InlineData(nameof(BlueskyAuthenticationMetrics.AuthenticationOutcomes), $"{Prefix}.authentications.total")]
    [InlineData(nameof(BlueskyAuthenticationMetrics.IdentityStoreMisses), $"{Prefix}.identitystore.misses.total")]
    [InlineData(nameof(BlueskyAuthenticationMetrics.IdentityStoreEvictions), $"{Prefix}.identitystore.evictions.total")]
    [InlineData(nameof(BlueskyAuthenticationMetrics.CorrelationStateRejections), $"{Prefix}.correlationstate.rejections.total")]
    public void CounterIsPublishedUnderItsExpectedName(string propertyName, string instrumentName)
    {
        // Instrument names are a contract with whatever is scraping them, so a rename should not pass silently.
        IServiceProvider services = CreateServiceProvider();
        IMeterFactory meterFactory = services.GetRequiredService<IMeterFactory>();
        var collector = new MetricCollector<long>(meterFactory, BlueskyAuthenticationMetrics.MeterName, instrumentName);

        BlueskyAuthenticationMetrics metrics = new(meterFactory);

        Counter<long> counter = (Counter<long>)InstrumentProperty(propertyName).GetValue(metrics)!;
        counter.Add(1);

        Assert.Single(collector.GetMeasurementSnapshot());
    }

    [Fact]
    public void RefreshWaitDurationHistogramIsPublishedUnderItsExpectedName()
    {
        IServiceProvider services = CreateServiceProvider();
        IMeterFactory meterFactory = services.GetRequiredService<IMeterFactory>();
        var collector = new MetricCollector<double>(
            meterFactory,
            BlueskyAuthenticationMetrics.MeterName,
            $"{Prefix}.tokenrefreshwaits.duration");

        BlueskyAuthenticationMetrics metrics = new(meterFactory);

        metrics.AccessTokenRefreshWaitDuration.Record(1.5);

        CollectedMeasurement<double> measurement = Assert.Single(collector.GetMeasurementSnapshot());
        Assert.Equal(1.5, measurement.Value);
    }

    [Fact]
    public void SignInFailuresAreTaggedWithTheirReason()
    {
        IServiceProvider services = CreateServiceProvider();
        IMeterFactory meterFactory = services.GetRequiredService<IMeterFactory>();
        var collector = new MetricCollector<long>(
            meterFactory,
            BlueskyAuthenticationMetrics.MeterName,
            $"{Prefix}.signins.total.failure");

        BlueskyAuthenticationMetrics metrics = new(meterFactory);

        metrics.SigninsFailed.Add(
            1,
            new KeyValuePair<string, object?>(BlueskyAuthenticationMetrics.SignInFailureReasonTagName, "NoQueryString"));

        CollectedMeasurement<long> measurement = Assert.Single(collector.GetMeasurementSnapshot());

        Assert.True(measurement.ContainsTags(BlueskyAuthenticationMetrics.SignInFailureReasonTagName));
        Assert.Equal("NoQueryString", measurement.Tags[BlueskyAuthenticationMetrics.SignInFailureReasonTagName]);
    }

    [Theory]
    [InlineData(nameof(BlueskyAuthenticationMetrics.SignInFailureReasonTagName), "reason")]
    [InlineData(nameof(BlueskyAuthenticationMetrics.TokenRefreshOutcomeTagName), "outcome")]
    [InlineData(nameof(BlueskyAuthenticationMetrics.TokenRefreshWaitReasonTagName), "reason")]
    [InlineData(nameof(BlueskyAuthenticationMetrics.DataProtectionSourceTagName), "source")]
    [InlineData(nameof(BlueskyAuthenticationMetrics.AuthenticationResultTagName), "result")]
    [InlineData(nameof(BlueskyAuthenticationMetrics.IdentityStoreMissPhaseTagName), "phase")]
    [InlineData(nameof(BlueskyAuthenticationMetrics.IdentityStoreOperationTagName), "operation")]
    [InlineData(nameof(BlueskyAuthenticationMetrics.CorrelationStateRejectionReasonTagName), "reason")]
    public void TagNamesFollowOpenTelemetryNamingConventions(string constantName, string expected)
    {
        // OpenTelemetry attribute keys are lower case, so a PascalCase key would be an outlier in any exporter.
        string? actual = (string?)typeof(BlueskyAuthenticationMetrics)
            .GetField(constantName, BindingFlags.Public | BindingFlags.Static)!
            .GetValue(null);

        Assert.Equal(expected, actual);
        Assert.Equal(actual!.ToLowerInvariant(), actual);
    }

    [Theory]
    [InlineData(nameof(BlueskyAuthenticationMetrics.AccessTokensRefreshed))]
    [InlineData(nameof(BlueskyAuthenticationMetrics.AccessTokensRefreshFailures))]
    [InlineData(nameof(BlueskyAuthenticationMetrics.AccessTokenRefreshWaits))]
    [InlineData(nameof(BlueskyAuthenticationMetrics.AccessTokenRefreshWaitDuration))]
    [InlineData(nameof(BlueskyAuthenticationMetrics.DataProtectionFailures))]
    [InlineData(nameof(BlueskyAuthenticationMetrics.ProfileCacheMisses))]
    [InlineData(nameof(BlueskyAuthenticationMetrics.ProfileCacheHits))]
    [InlineData(nameof(BlueskyAuthenticationMetrics.SigninsSucceeded))]
    [InlineData(nameof(BlueskyAuthenticationMetrics.SigninsFailed))]
    [InlineData(nameof(BlueskyAuthenticationMetrics.SignOuts))]
    [InlineData(nameof(BlueskyAuthenticationMetrics.CredentialRevocationFailures))]
    [InlineData(nameof(BlueskyAuthenticationMetrics.AuthenticationOutcomes))]
    [InlineData(nameof(BlueskyAuthenticationMetrics.IdentityStoreMisses))]
    [InlineData(nameof(BlueskyAuthenticationMetrics.IdentityStoreEvictions))]
    [InlineData(nameof(BlueskyAuthenticationMetrics.IdentityStoreOperationDuration))]
    [InlineData(nameof(BlueskyAuthenticationMetrics.CorrelationStateRejections))]
    public void InstrumentsArePubliclyReadable(string propertyName)
    {
        // DistributedCacheIdentityStore hands its Metrics to subclasses, which may live in another assembly and can
        // only record against an instrument which is public. This assembly has InternalsVisibleTo access, so merely
        // calling the property from a test would still compile if the instruments went back to being internal.
        PropertyInfo property = InstrumentProperty(propertyName);

        Assert.NotNull(property.GetMethod);
        Assert.True(property.GetMethod.IsPublic, $"{propertyName} is not publicly readable.");
    }

    [Fact]
    public void RefreshWaitHistogramBucketsSpanTheRangeOfObservableWaits()
    {
        // A wait can be anything from near zero, when the refresh which held the lock has already finished, up to
        // RefreshCheckWait multiplied by MaxRefreshChecks. Boundaries which miss either end produce a histogram
        // where most of the buckets can never fill and the common case has no resolution.
        BlueskyAuthenticationOptions options = new();
        double defaultLongestWait = options.RefreshCheckWait.TotalSeconds * options.MaxRefreshChecks;

        IReadOnlyList<double> boundaries = (IReadOnlyList<double>)typeof(BlueskyAuthenticationMetrics)
            .GetField("s_refreshWaitBucketBoundaries", BindingFlags.NonPublic | BindingFlags.Static)!
            .GetValue(null)!;

        Assert.NotEmpty(boundaries);
        Assert.Equal([.. boundaries.Order()], boundaries);
        Assert.Equal(boundaries.Distinct().Count(), boundaries.Count);

        Assert.True(
            boundaries[0] < 0.1,
            $"The lowest boundary {boundaries[0]} leaves the common near zero wait without resolution.");

        Assert.True(
            boundaries.Contains(defaultLongestWait),
            $"There is no boundary at the default longest possible wait of {defaultLongestWait} seconds.");

        Assert.True(
            boundaries[^1] >= defaultLongestWait,
            $"The highest boundary {boundaries[^1]} is below the default longest possible wait of {defaultLongestWait} seconds.");

        Assert.True(
            boundaries.Count(boundary => boundary <= defaultLongestWait) >= boundaries.Count / 2,
            "More than half the buckets are above the default longest possible wait, so they can never fill.");
    }

    [Fact]
    public void IdentityStoreOperationHistogramBucketsResolveCacheLatencies()
    {
        // An identity store operation is a cache read or write. Boundaries which start at a second, as a duration
        // histogram often does, would put every healthy operation in the first bucket and show nothing at all.
        IReadOnlyList<double> boundaries = (IReadOnlyList<double>)typeof(BlueskyAuthenticationMetrics)
            .GetField("s_identityStoreOperationBucketBoundaries", BindingFlags.NonPublic | BindingFlags.Static)!
            .GetValue(null)!;

        Assert.NotEmpty(boundaries);
        Assert.Equal([.. boundaries.Order()], boundaries);
        Assert.Equal(boundaries.Distinct().Count(), boundaries.Count);

        Assert.True(
            boundaries[0] <= 0.001,
            $"The lowest boundary {boundaries[0]} cannot resolve an in memory store, which answers in well under a millisecond.");

        Assert.True(
            boundaries.Count(boundary => boundary <= 0.1) >= 5,
            "There are too few buckets below 100ms to tell a healthy network cache from a struggling one.");
    }

    [Fact]
    public void RecordIdentityStoreOperationTagsTheDurationItRecords()
    {
        // A custom store records through this helper, so it is what keeps a third party store's timings in the same
        // series as the ones from the stores in this package.
        IServiceProvider services = CreateServiceProvider();
        IMeterFactory meterFactory = services.GetRequiredService<IMeterFactory>();
        var collector = new MetricCollector<double>(
            meterFactory,
            BlueskyAuthenticationMetrics.MeterName,
            $"{Prefix}.identitystore.operations.duration");

        BlueskyAuthenticationMetrics metrics = new(meterFactory);

        metrics.RecordIdentityStoreOperation(
            BlueskyAuthenticationMetrics.IdentityStoreOperationGet,
            Stopwatch.GetTimestamp());

        CollectedMeasurement<double> measurement = Assert.Single(collector.GetMeasurementSnapshot());

        Assert.Equal(
            BlueskyAuthenticationMetrics.IdentityStoreOperationGet,
            measurement.Tags[BlueskyAuthenticationMetrics.IdentityStoreOperationTagName]);

        Assert.InRange(measurement.Value, 0, 60);
    }

    [Fact]
    public void EveryAuthenticationOutcomeHasItsOwnLowerCaseTagValue()
    {
        // The tag is what an operator groups by to see why sessions are failing, so two different failures sharing a
        // value, or a failure falling through to the catch all, would hide one of them.
        FieldInfo[] fields = typeof(AuthenticateResults)
            .GetFields(BindingFlags.NonPublic | BindingFlags.Static)
            .Where(field => field.FieldType == typeof(AuthenticateResult))
            .ToArray();

        Assert.NotEmpty(fields);

        string[] reasons = fields
            .Select(field => AuthenticateResults.ReasonFor((AuthenticateResult)field.GetValue(null)!))
            .ToArray();

        Assert.Equal(fields.Length, reasons.Distinct(StringComparer.Ordinal).Count());
        Assert.DoesNotContain("failure", reasons);
        Assert.All(reasons, reason => Assert.Equal(reason.ToLowerInvariant(), reason));
        Assert.All(reasons, reason => Assert.DoesNotContain(' ', reason));
    }

    [Fact]
    public void AnUnrecognisedAuthenticationResultIsReportedAsASingleCatchAllValue()
    {
        // An application can replace the result from an event handler, so the tag has to be bounded by something
        // other than trust.
        Assert.Equal("failure", AuthenticateResults.ReasonFor(AuthenticateResult.Fail("something an application made up")));
        Assert.Equal("success", AuthenticateResults.ReasonFor(AuthenticateResult.Success(
            new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity("Bluesky")), "Bluesky"))));
    }

    private static PropertyInfo InstrumentProperty(string propertyName)
    {
        PropertyInfo? property = typeof(BlueskyAuthenticationMetrics)
            .GetProperty(propertyName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

        Assert.NotNull(property);

        return property;
    }

    private static ServiceProvider CreateServiceProvider()
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddMetrics();
        return serviceCollection.BuildServiceProvider();
    }
}
