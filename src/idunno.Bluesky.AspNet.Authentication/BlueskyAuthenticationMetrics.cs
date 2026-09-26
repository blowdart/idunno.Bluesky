// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Metrics;

namespace idunno.Bluesky.AspNet.Authentication;

/// <summary>
/// Bluesky Authentication metrics.
/// </summary>
[SuppressMessage("Globalization", "CA1308:Normalize strings to uppercase", Justification = "Metric names are typically lower case.")]
[SuppressMessage("Security", "S6418:Hard-coded secrets are security-sensitive", Justification = "The constants are metric tag names and values. Those describing token refresh are flagged for containing \"token\", but hold no secret.")]
public class BlueskyAuthenticationMetrics
{
    // A wait typically ends on the first check, when the refresh which held the lock has already finished, so the
    // distribution needs resolution close to zero. The upper bound is RefreshCheckWait multiplied by MaxRefreshChecks,
    // which defaults to 12.5 seconds, but both are configurable so the boundaries run past the default ceiling.
    private static readonly IReadOnlyList<double> s_refreshWaitBucketBoundaries = [0.01, 0.05, 0.1, 0.25, 0.5, 1, 2.5, 5, 7.5, 10, 12.5, 20, 30, 60];

    // An identity store operation is a cache read or write, which is sub millisecond in memory and a small number of
    // milliseconds against a network cache, so the resolution has to sit well below a second. The upper boundaries
    // exist to make a store which has started timing out visible rather than to give it resolution.
    private static readonly IReadOnlyList<double> s_identityStoreOperationBucketBoundaries =
        [0.0001, 0.00025, 0.0005, 0.001, 0.0025, 0.005, 0.01, 0.025, 0.05, 0.1, 0.25, 0.5, 1, 2.5, 5];

    // For non-DI scenarios, see https://learn.microsoft.com/en-us/dotnet/core/diagnostics/metrics-instrumentation#best-practices
    private static readonly Meter s_meter = new(MeterName, MeterVersion);

    /// <summary>
    /// The name of the tag recording why a sign-in failed.
    /// </summary>
    public const string SignInFailureReasonTagName = "reason";

    /// <summary>
    /// The name of the tag recording how an access token refresh completed.
    /// </summary>
    public const string TokenRefreshOutcomeTagName = "outcome";

    /// <summary>
    /// The name of the tag recording why a request waited for an access token refresh.
    /// </summary>
    public const string TokenRefreshWaitReasonTagName = "reason";

    /// <summary>
    /// The name of the tag recording which protected value could not be unprotected.
    /// </summary>
    public const string DataProtectionSourceTagName = "source";

    /// <summary>
    /// The name of the tag recording how an authentication attempt ended.
    /// </summary>
    public const string AuthenticationResultTagName = "result";

    /// <summary>
    /// The name of the tag recording which part of the request an identity store lookup missed in.
    /// </summary>
    public const string IdentityStoreMissPhaseTagName = "phase";

    /// <summary>
    /// The name of the tag recording which identity store operation was timed.
    /// </summary>
    public const string IdentityStoreOperationTagName = "operation";

    /// <summary>
    /// The name of the tag recording why OAuth correlation state was rejected.
    /// </summary>
    public const string CorrelationStateRejectionReasonTagName = "reason";

    /// <summary>
    /// The value of the <see cref="TokenRefreshOutcomeTagName"/> tag when this request performed the refresh itself.
    /// </summary>
    public const string TokenRefreshOutcomeSelf = "self";

    /// <summary>
    /// The value of the <see cref="TokenRefreshOutcomeTagName"/> tag when this request's refresh failed, but another
    /// request had concurrently refreshed and stored usable credentials.
    /// </summary>
    public const string TokenRefreshOutcomeConcurrent = "concurrent";

    /// <summary>
    /// The value of the <see cref="TokenRefreshWaitReasonTagName"/> tag when a refresh was already running when the
    /// request arrived.
    /// </summary>
    public const string TokenRefreshWaitReasonRefreshInProgress = "refresh_in_progress";

    /// <summary>
    /// The value of the <see cref="TokenRefreshWaitReasonTagName"/> tag when the request tried to start a refresh and
    /// lost the race for the refresh lock to another request.
    /// </summary>
    public const string TokenRefreshWaitReasonLockDenied = "lock_denied";

    /// <summary>
    /// The value of the <see cref="DataProtectionSourceTagName"/> tag when the correlation cookie could not be unprotected.
    /// </summary>
    public const string DataProtectionSourceCorrelationCookie = "correlation_cookie";

    /// <summary>
    /// The value of the <see cref="DataProtectionSourceTagName"/> tag when a stored identity could not be unprotected.
    /// </summary>
    public const string DataProtectionSourceIdentityStore = "identity_store";

    /// <summary>
    /// The value of the <see cref="IdentityStoreMissPhaseTagName"/> tag when the identity backing an authentication
    /// cookie was not in the store.
    /// </summary>
    public const string IdentityStoreMissPhaseAuthentication = "authentication";

    /// <summary>
    /// The value of the <see cref="IdentityStoreMissPhaseTagName"/> tag when the identity was not in the store after
    /// an access token refresh had stored it.
    /// </summary>
    public const string IdentityStoreMissPhaseTokenRefresh = "token_refresh";

    /// <summary>
    /// The value of the <see cref="IdentityStoreOperationTagName"/> tag for storing a new identity.
    /// </summary>
    public const string IdentityStoreOperationAdd = "add";

    /// <summary>
    /// The value of the <see cref="IdentityStoreOperationTagName"/> tag for reading an identity.
    /// </summary>
    public const string IdentityStoreOperationGet = "get";

    /// <summary>
    /// The value of the <see cref="IdentityStoreOperationTagName"/> tag for removing an identity.
    /// </summary>
    public const string IdentityStoreOperationRemove = "remove";

    /// <summary>
    /// The value of the <see cref="IdentityStoreOperationTagName"/> tag for updating a stored identity.
    /// </summary>
    public const string IdentityStoreOperationUpdate = "update";

    /// <summary>
    /// The value of the <see cref="CorrelationStateRejectionReasonTagName"/> tag when the correlation cookie had passed
    /// the expiry it was protected with.
    /// </summary>
    public const string CorrelationStateRejectionExpiredCookie = "expired_cookie";

    /// <summary>
    /// The value of the <see cref="CorrelationStateRejectionReasonTagName"/> tag when the correlation cookie could not
    /// be unprotected.
    /// </summary>
    public const string CorrelationStateRejectionUnprotectFailed = "unprotect_failed";

    /// <summary>
    /// The value of the <see cref="CorrelationStateRejectionReasonTagName"/> tag when the request carried no
    /// correlation cookie at all.
    /// </summary>
    public const string CorrelationStateRejectionMissingCookie = "missing_cookie";

    /// <summary>
    /// The value of the <see cref="CorrelationStateRejectionReasonTagName"/> tag when the callback did not carry
    /// exactly one OAuth state parameter, so the correlation cookie for the login it belongs to could not be named.
    /// </summary>
    public const string CorrelationStateRejectionMissingState = "missing_state";

    /// <summary>
    /// The value of the <see cref="CorrelationStateRejectionReasonTagName"/> tag when the correlation cookie was
    /// unprotected successfully but its contents could not be parsed.
    /// </summary>
    public const string CorrelationStateRejectionMalformedCookie = "malformed_cookie";

    /// <summary>
    /// The value of the <see cref="CorrelationStateRejectionReasonTagName"/> tag when the correlation cookie was
    /// readable but the login state it pointed at was not in the correlation cache.
    /// </summary>
    public const string CorrelationStateRejectionStateNotFound = "state_not_found";

    /// <summary>
    /// Creates a new instance of <see cref="BlueskyAuthenticationMetrics"/> using the provided <see cref="IMeterFactory"/> to create the underlying <see cref="Meter"/>.
    /// </summary>
    /// <param name="meterFactory">An optional <see cref="IMeterFactory"/> to use for creating the underlying <see cref="Meter"/>.</param>
    [SuppressMessage(
        "Reliability",
        "CA2000:Dispose objects before losing scope",
        Justification = "but IMeterFactory automatically manages the lifetime of any Meter objects it creates, disposing them when the DI container is disposed.")]
    public BlueskyAuthenticationMetrics(IMeterFactory? meterFactory)
    {
        if (meterFactory == null)
        {
            Initialize(s_meter);
        }
        else
        {
            Initialize(meterFactory.Create(MeterName, MeterVersion));
        }
    }

    [MemberNotNull(
        nameof(AccessTokensRefreshed),
        nameof(AccessTokensRefreshFailures),
        nameof(AccessTokenRefreshWaits),
        nameof(AccessTokenRefreshWaitDuration),
        nameof(DataProtectionFailures),
        nameof(ProfileCacheMisses),
        nameof(HandleVerificationFailures),
        nameof(ProfileCacheHits),
        nameof(SigninsSucceeded),
        nameof(SigninsFailed),
        nameof(SignOuts),
        nameof(CredentialRevocationFailures),
        nameof(AuthenticationOutcomes),
        nameof(IdentityStoreMisses),
        nameof(IdentityStoreEvictions),
        nameof(IdentityStoreWriteFailures),
        nameof(IdentityStoreOperationDuration),
        nameof(CorrelationStateRejections)
        )]
    private void Initialize(Meter meter)
    {
        AccessTokensRefreshed = meter.CreateCounter<long>(
            name: $"{MeterName.ToLowerInvariant()}.tokenrefreshes.total",
            description: "Total access tokens refreshed",
            unit: "{refreshes}");

        AccessTokensRefreshFailures = meter.CreateCounter<long>(
            name: $"{MeterName.ToLowerInvariant()}.tokenrefreshfailures.total",
            description: "Total access tokens refresh failures",
            unit: "{refreshes}");

        AccessTokenRefreshWaits = meter.CreateCounter<long>(
            name: $"{MeterName.ToLowerInvariant()}.tokenrefreshwaits.total",
            description: "Total waits during token refresh as another refresh is in progress",
            unit: "{waits}");

        AccessTokenRefreshWaitDuration = meter.CreateHistogram<double>(
            name: $"{MeterName.ToLowerInvariant()}.tokenrefreshwaits.duration",
            description: "Duration of waits during token refresh as another refresh is in progress",
            unit: "s",
            advice: new InstrumentAdvice<double> { HistogramBucketBoundaries = s_refreshWaitBucketBoundaries });

        DataProtectionFailures = meter.CreateCounter<long>(
            name: $"{MeterName.ToLowerInvariant()}.dataprotection.failures.total",
            description: "Total data protection failures",
            unit: "{failures}");

        ProfileCacheMisses = meter.CreateCounter<long>(
            name: $"{MeterName.ToLowerInvariant()}.profilecache.misses.total",
            description: "Total profile cache misses",
            unit: "{misses}");

        HandleVerificationFailures = meter.CreateCounter<long>(
            name: $"{MeterName.ToLowerInvariant()}.handleverification.failures.total",
            description: "Total handles returned by a profile which did not resolve back to the DID they were returned for",
            unit: "{failures}");

        ProfileCacheHits = meter.CreateCounter<long>(
            name: $"{MeterName.ToLowerInvariant()}.profilecache.hits.total",
            description: "Total profile cache hits",
            unit: "{hits}");

        SigninsSucceeded = meter.CreateCounter<long>(
            name: $"{MeterName.ToLowerInvariant()}.signins.total.successful",
            description: "Total successful sign-ins",
            unit: "{signins}");

        SigninsFailed = meter.CreateCounter<long>(
            name: $"{MeterName.ToLowerInvariant()}.signins.total.failure",
            description: "Total failed sign-ins",
            unit: "{signins}");

        SignOuts = meter.CreateCounter<long>(
            name: $"{MeterName.ToLowerInvariant()}.signouts.total",
            description: "Total sign-outs",
            unit: "{signouts}");

        CredentialRevocationFailures = meter.CreateCounter<long>(
            name: $"{MeterName.ToLowerInvariant()}.credentialrevocations.failures.total",
            description: "Total failures revoking credentials at the PDS during sign-out",
            unit: "{failures}");

        AuthenticationOutcomes = meter.CreateCounter<long>(
            name: $"{MeterName.ToLowerInvariant()}.authentications.total",
            description: "Total authentication attempts against a request carrying an authentication cookie",
            unit: "{authentications}");

        IdentityStoreMisses = meter.CreateCounter<long>(
            name: $"{MeterName.ToLowerInvariant()}.identitystore.misses.total",
            description: "Total identities which were not in the identity store when a request needed them",
            unit: "{misses}");

        IdentityStoreEvictions = meter.CreateCounter<long>(
            name: $"{MeterName.ToLowerInvariant()}.identitystore.evictions.total",
            description: "Total identities evicted from the identity store because it reached its size limit",
            unit: "{evictions}");

        IdentityStoreWriteFailures = meter.CreateCounter<long>(
            name: $"{MeterName.ToLowerInvariant()}.identitystore.writefailures.total",
            description: "Total identities which could not be written to the identity store",
            unit: "{failures}");

        IdentityStoreOperationDuration = meter.CreateHistogram<double>(
            name: $"{MeterName.ToLowerInvariant()}.identitystore.operations.duration",
            description: "Duration of identity store operations",
            unit: "s",
            advice: new InstrumentAdvice<double> { HistogramBucketBoundaries = s_identityStoreOperationBucketBoundaries });

        CorrelationStateRejections = meter.CreateCounter<long>(
            name: $"{MeterName.ToLowerInvariant()}.correlationstate.rejections.total",
            description: "Total OAuth correlation states rejected when processing a login callback",
            unit: "{rejections}");
    }

    /// <summary>
    /// Gets the meter name publishing metrics.
    /// </summary>
    public static string MeterName => "idunno.Bluesky.AspNet.Authentication";

    /// <summary>
    /// Gets the current version of the meter.
    /// </summary>
    public static string MeterVersion => "1.0.0";

    /// <summary>
    /// Gets the counter of access tokens successfully refreshed.
    /// </summary>
    /// <remarks>
    /// <para>
    ///   Tagged with <see cref="TokenRefreshOutcomeTagName"/>, whose value is <see cref="TokenRefreshOutcomeSelf"/> when
    ///   the request performed the refresh itself, and <see cref="TokenRefreshOutcomeConcurrent"/> when the request's own
    ///   refresh failed but another request had concurrently refreshed and stored usable credentials.
    /// </para>
    /// </remarks>
    public Counter<long> AccessTokensRefreshed { get; private set; }

    /// <summary>
    /// Gets the counter of access token refresh failures.
    /// </summary>
    public Counter<long> AccessTokensRefreshFailures { get; private set; }

    /// <summary>
    /// Gets the counter of waits for a token refresh started by another request.
    /// </summary>
    /// <remarks>
    /// <para>
    ///   Tagged with <see cref="TokenRefreshWaitReasonTagName"/>, whose value is
    ///   <see cref="TokenRefreshWaitReasonRefreshInProgress"/> when a refresh was already running when the request
    ///   arrived, and <see cref="TokenRefreshWaitReasonLockDenied"/> when the request tried to start a refresh itself
    ///   and lost the race for the refresh lock. A persistent count of the latter means requests are arriving together
    ///   closely enough to contend, rather than simply queueing behind a refresh already under way.
    /// </para>
    /// </remarks>
    public Counter<long> AccessTokenRefreshWaits { get; private set; }

    /// <summary>
    /// Gets the histogram of how long requests waited for a token refresh started by another request.
    /// </summary>
    public Histogram<double> AccessTokenRefreshWaitDuration { get; private set; }

    /// <summary>
    /// Gets the counter of values which could not be unprotected.
    /// </summary>
    /// <remarks>
    /// <para>
    ///   Tagged with <see cref="DataProtectionSourceTagName"/>, whose value is <see cref="DataProtectionSourceCorrelationCookie"/>
    ///   or <see cref="DataProtectionSourceIdentityStore"/>.
    /// </para>
    /// </remarks>
    public Counter<long> DataProtectionFailures { get; private set; }

    /// <summary>
    /// Gets the counter of profile cache misses.
    /// </summary>
    public Counter<long> ProfileCacheMisses { get; private set; }

    /// <summary>
    /// Gets the counter of handles which did not resolve back to the DID whose profile returned them.
    /// </summary>
    /// <remarks>
    /// <para>
    ///   A handle is asserted by the user's own personal data server, so it is verified against the directory before it
    ///   becomes a claim. Anything counted here was dropped rather than presented to the application.
    /// </para>
    /// </remarks>
    public Counter<long> HandleVerificationFailures { get; private set; }

    /// <summary>
    /// Gets the counter of profile cache hits.
    /// </summary>
    /// <remarks>
    /// <para>
    ///   Reported alongside <see cref="ProfileCacheMisses"/> so the cache hit rate can be calculated. A falling hit rate
    ///   means a call to the PDS on more requests, and a hit rate of zero means the cache is not working at all.
    /// </para>
    /// </remarks>
    public Counter<long> ProfileCacheHits { get; private set; }

    /// <summary>
    /// Gets the counter of successful sign-ins.
    /// </summary>
    public Counter<long> SigninsSucceeded { get; private set; }

    /// <summary>
    /// Gets the counter of failed sign-ins.
    /// </summary>
    /// <remarks>
    /// <para>
    ///   Tagged with <see cref="SignInFailureReasonTagName"/>, recording why the sign-in failed.
    /// </para>
    /// </remarks>
    public Counter<long> SigninsFailed { get; private set; }

    /// <summary>
    /// Gets the counter of sign-outs.
    /// </summary>
    /// <remarks>
    /// <para>
    ///   Reported alongside <see cref="SigninsSucceeded"/>, so the number of live sessions an application is holding
    ///   identities for can be estimated.
    /// </para>
    /// </remarks>
    public Counter<long> SignOuts { get; private set; }

    /// <summary>
    /// Gets the counter of failures revoking credentials at the PDS during sign-out.
    /// </summary>
    /// <remarks>
    /// <para>
    ///   A sign-out removes the local identity whether or not the PDS accepted the revocation, so a failure here leaves
    ///   a refresh token live at the PDS which the application can no longer revoke. That is a security relevant outcome
    ///   which is otherwise only visible in the logs.
    /// </para>
    /// </remarks>
    public Counter<long> CredentialRevocationFailures { get; private set; }

    /// <summary>
    /// Gets the counter of authentication attempts made against a request carrying an authentication cookie.
    /// </summary>
    /// <remarks>
    /// <para>
    ///   Tagged with <see cref="AuthenticationResultTagName"/>, whose value is <c>success</c> when the request was
    ///   authenticated, or one of a fixed set of lower case reasons describing why it was not, such as
    ///   <c>unprotect_ticket_failed</c>, <c>identity_missing_in_store</c>, <c>ticket_expired</c>,
    ///   <c>token_refresh_failed</c> or <c>request_cancelled</c>. Requests which carry no cookie of this scheme are not
    ///   counted, so the counter measures the health of established sessions rather than overall traffic.
    /// </para>
    /// </remarks>
    public Counter<long> AuthenticationOutcomes { get; private set; }

    /// <summary>
    /// Gets the counter of identities which were not in the identity store when a request needed them.
    /// </summary>
    /// <remarks>
    /// <para>
    ///   Tagged with <see cref="IdentityStoreMissPhaseTagName"/>, whose value is
    ///   <see cref="IdentityStoreMissPhaseAuthentication"/> when the identity behind a valid authentication cookie had
    ///   gone, and <see cref="IdentityStoreMissPhaseTokenRefresh"/> when it had gone immediately after a refresh stored
    ///   it. Both silently sign a user out, and are the symptom of a store which is evicting entries early, expiring
    ///   them before the authentication cookie, or losing them on restart.
    /// </para>
    /// </remarks>
    public Counter<long> IdentityStoreMisses { get; private set; }

    /// <summary>
    /// Gets the counter of identities evicted from the identity store because it reached its size limit.
    /// </summary>
    /// <remarks>
    /// <para>
    ///   Only <see cref="EphemeralIdentityStore"/> has a size limit. Its capacity warning is logged once for the
    ///   lifetime of the process, so this counter is the only way to see how often eviction is signing users out.
    /// </para>
    /// </remarks>
    public Counter<long> IdentityStoreEvictions { get; private set; }

    /// <summary>
    /// Gets the counter of identities which could not be written to the identity store.
    /// </summary>
    /// <remarks>
    /// <para>
    ///   A failed write loses a sign-in, or loses refreshed credentials after the refresh token which produced them has
    ///   already been spent, so any value here means users are being signed out by the store rather than by their own
    ///   actions. <see cref="EphemeralIdentityStore"/> raises this only when it is full and compacting it did not make
    ///   room, which means its size limit is far too small for the number of users signing in.
    /// </para>
    /// </remarks>
    public Counter<long> IdentityStoreWriteFailures { get; private set; }

    /// <summary>
    /// Gets the histogram of how long identity store operations took.
    /// </summary>
    /// <remarks>
    /// <para>
    ///   Tagged with <see cref="IdentityStoreOperationTagName"/>, whose value is <see cref="IdentityStoreOperationAdd"/>,
    ///   <see cref="IdentityStoreOperationGet"/>, <see cref="IdentityStoreOperationRemove"/> or
    ///   <see cref="IdentityStoreOperationUpdate"/>. Every authenticated request reads the store, so its latency is
    ///   added to the latency of the application.
    /// </para>
    /// </remarks>
    public Histogram<double> IdentityStoreOperationDuration { get; private set; }

    /// <summary>
    /// Gets the counter of OAuth correlation states rejected when processing a login callback.
    /// </summary>
    /// <remarks>
    /// <para>
    ///   Tagged with <see cref="CorrelationStateRejectionReasonTagName"/>, whose value is
    ///   <see cref="CorrelationStateRejectionMissingCookie"/>, <see cref="CorrelationStateRejectionMissingState"/>,
    ///   <see cref="CorrelationStateRejectionExpiredCookie"/>,
    ///   <see cref="CorrelationStateRejectionUnprotectFailed"/>, <see cref="CorrelationStateRejectionMalformedCookie"/>
    ///   or <see cref="CorrelationStateRejectionStateNotFound"/>. Correlation state is what ties a login callback to the
    ///   login which started it, so a sustained count is either users taking too long to log in or callbacks arriving
    ///   which no login on this application started.
    /// </para>
    /// </remarks>
    public Counter<long> CorrelationStateRejections { get; private set; }

    /// <summary>
    /// Records the duration of an identity store operation against <see cref="IdentityStoreOperationDuration"/>.
    /// </summary>
    /// <param name="operation">The operation which was timed, tagged as <see cref="IdentityStoreOperationTagName"/>.</param>
    /// <param name="startTimestamp">The <see cref="Stopwatch.GetTimestamp"/> value captured when the operation started.</param>
    /// <remarks>
    /// <para>
    ///   Use one of <see cref="IdentityStoreOperationAdd"/>, <see cref="IdentityStoreOperationGet"/>,
    ///   <see cref="IdentityStoreOperationRemove"/> or <see cref="IdentityStoreOperationUpdate"/> as
    ///   <paramref name="operation"/>, so a custom store's timings sit alongside those from the stores in this package
    ///   rather than in a series of their own.
    /// </para>
    /// </remarks>
    public void RecordIdentityStoreOperation(string operation, long startTimestamp) =>
        IdentityStoreOperationDuration.Record(
            Stopwatch.GetElapsedTime(startTimestamp).TotalSeconds,
            new KeyValuePair<string, object?>(IdentityStoreOperationTagName, operation));
}
