// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.Metrics;

using Microsoft.Extensions.Logging;

namespace idunno.AtProto;

/// <summary>
/// Configures an instance of <see cref="DidHandleCache"/>.
/// </summary>
public sealed record DidHandleCacheOptions
{
    /// <summary>
    /// Gets the maximum number of handles held in the cache.
    /// </summary>
    /// <value>The maximum number of cached handles. The default is 100,000.</value>
    /// <exception cref="ArgumentOutOfRangeException">The value is less than or equal to zero.</exception>
    /// <remarks>
    /// <para>When the cache is full a new handle is not cached until the cache has been compacted, which happens in the background.
    /// The same limit bounds the number of lookups which can be pending at once; once that many are pending a lookup for a DID which
    /// is not already being resolved returns <see cref="Handle.Invalid"/>, which is not cached.</para>
    /// <para>The size bounds the cache's memory, not the number of resolutions. A source of DIDs which sends more distinct DIDs
    /// than this can keep evicting handles, so most lookups need a resolution.</para>
    /// </remarks>
    public int Size
    {
        get;
        init
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
            field = value;
        }
    } = 100_000;

    /// <summary>
    /// Gets the maximum number of resolutions which can run at once.
    /// </summary>
    /// <value>The maximum number of concurrent resolutions. The default is 32.</value>
    /// <exception cref="ArgumentOutOfRangeException">The value is less than or equal to zero.</exception>
    /// <remarks>
    /// <para>Lookups beyond this limit wait for a resolution to finish. Time spent waiting counts against <see cref="ResolutionTimeout"/>,
    /// and a lookup which times out waiting returns <see cref="Handle.Invalid"/>, which is not cached.</para>
    /// <para>This bounds the outbound requests a source of DIDs can cause, such as a firehose relay sending many distinct DIDs.</para>
    /// </remarks>
    public int MaximumConcurrentResolutions
    {
        get;
        init
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
            field = value;
        }
    } = 32;

    /// <summary>
    /// Gets how long a verified handle is cached for.
    /// </summary>
    /// <value>The cache duration for verified handles. The default is one hour.</value>
    /// <exception cref="ArgumentOutOfRangeException">The value is less than or equal to zero, or exceeds the timer's maximum duration.</exception>
    /// <remarks>
    /// <para>A longer duration means fewer resolutions, but a handle which changes without an <c>#identity</c> event being seen is
    /// reported for longer.</para>
    /// </remarks>
    public TimeSpan Duration
    {
        get;
        init
        {
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(value, TimeSpan.Zero);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(value, TimeSpan.FromMilliseconds(uint.MaxValue - 1));
            field = value;
        }
    } = TimeSpan.FromHours(1);

    /// <summary>
    /// Gets how long a failure to resolve or verify a handle is cached for.
    /// </summary>
    /// <value>The cache duration for failures. The default is one minute.</value>
    /// <exception cref="ArgumentOutOfRangeException">The value is less than or equal to zero, or exceeds the timer's maximum duration.</exception>
    /// <remarks>
    /// <para>A failure is cached as <see cref="Handle.Invalid"/>, so a DID which cannot be resolved is not resolved again for every lookup.
    /// Failures are cached for no longer than <see cref="Duration"/>.</para>
    /// </remarks>
    public TimeSpan FailedResolutionDuration
    {
        get;
        init
        {
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(value, TimeSpan.Zero);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(value, TimeSpan.FromMilliseconds(uint.MaxValue - 1));
            field = value;
        }
    } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Gets how long a single resolution may take before it is abandoned and treated as a failure.
    /// </summary>
    /// <value>The resolution timeout. The default is 30 seconds.</value>
    /// <exception cref="ArgumentOutOfRangeException">The value is less than or equal to zero, or exceeds the timer's maximum duration.</exception>
    /// <remarks>
    /// <para>A resolution is shared by every caller waiting for the same DID and is not cancelled when they stop waiting, so this
    /// bounds how long it can run. The timeout includes any time spent waiting for one of the <see cref="MaximumConcurrentResolutions"/>
    /// resolution slots.</para>
    /// </remarks>
    public TimeSpan ResolutionTimeout
    {
        get;
        init
        {
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(value, TimeSpan.Zero);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(value, TimeSpan.FromMilliseconds(uint.MaxValue - 1));
            field = value;
        }
    } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets the <see cref="Uri"/> of the PLC directory used to resolve <c>did:plc</c> DIDs.
    /// </summary>
    /// <value>The PLC directory, or <see langword="null"/> to use <c>https://plc.directory</c>.</value>
    public Uri? PlcDirectory { get; init; }

    /// <summary>
    /// Gets the <see cref="System.Net.Http.HttpClient"/> used to resolve DID documents and handles.
    /// </summary>
    /// <value>The HTTP client, or <see langword="null"/> to create one for each resolution.</value>
    /// <remarks>
    /// <para>The client is not disposed by the cache.</para>
    /// <para><b>Warning:</b> DID documents and handles name hosts chosen by whoever controls the DID, so resolving them makes requests to
    /// attacker-chosen hosts. When no client is supplied the cache uses a handler which refuses to connect to loopback, private and other
    /// non-public addresses. A supplied client does not get that protection; it is the caller's responsibility to configure it to block
    /// requests to internal networks.</para>
    /// </remarks>
    public HttpClient? HttpClient { get; init; }

    /// <summary>
    /// Gets the <see cref="ILoggerFactory"/>, if any, to use when creating loggers.
    /// </summary>
    public ILoggerFactory? LoggerFactory { get; init; }

    /// <summary>
    /// Gets the <see cref="IMeterFactory"/>, if any, to use when creating meters.
    /// </summary>
    public IMeterFactory? MeterFactory { get; init; }

    /// <summary>
    /// Gets the <see cref="System.TimeProvider"/> used to expire cached handles and time out resolutions.
    /// </summary>
    /// <value>The time provider. The default is <see cref="TimeProvider.System"/>.</value>
    /// <exception cref="ArgumentNullException">The value is <see langword="null"/>.</exception>
    public TimeProvider TimeProvider
    {
        get;
        init
        {
            ArgumentNullException.ThrowIfNull(value);
            field = value;
        }
    } = TimeProvider.System;
}
