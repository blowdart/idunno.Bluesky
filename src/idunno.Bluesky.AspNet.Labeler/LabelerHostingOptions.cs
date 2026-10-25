// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.Bluesky.AspNet.Labeler;

/// <summary>Configures bounded public label distribution and durable publication.</summary>
/// <remarks><para>Limits apply per process, not across a cluster. Use trusted reverse-proxy and
/// application rate limits for deployment-wide admission. Storage initialization is explicit.</para></remarks>
public sealed record LabelerHostingOptions
{
    /// <summary>Gets the maximum simultaneous subscriptions per process.</summary>
    /// <value>The connection limit. The default is 100.</value>
    public int MaximumSubscriptions { get; init; } = 100;

    /// <summary>Gets the maximum events read or published in one batch.</summary>
    /// <value>The batch size, from 1 through 250. The default is 100.</value>
    public int BatchSize { get; init; } = 100;

    /// <summary>Gets the interval between durable publication and live-read polls.</summary>
    /// <value>The positive polling interval. The default is one second.</value>
    public TimeSpan PollInterval { get; init; } = TimeSpan.FromSeconds(1);

    /// <summary>Gets the deadline for sending a subscription frame.</summary>
    /// <value>The positive send timeout. The default is 30 seconds.</value>
    public TimeSpan SendTimeout { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>Gets the deadline for reading and encoding a query response.</summary>
    /// <value>The positive query timeout. The default is 10 seconds.</value>
    public TimeSpan QueryTimeout { get; init; } = TimeSpan.FromSeconds(10);

    /// <summary>Gets the maximum encoded query response or subscription message size.</summary>
    /// <value>The maximum byte count. The default is 1 MiB.</value>
    public int MaximumMessageBytes { get; init; } = 1024 * 1024;

    /// <summary>Gets the explicitly configured public HTTPS origin for the labeler service.</summary>
    /// <value>The public origin, or <see langword="null"/> when identity publication is not configured.</value>
    /// <remarks><para>Key-rotation publication verification requires this value. It is never inferred from an incoming request host.</para></remarks>
    public Uri? PublicEndpoint { get; init; }

    /// <summary>Rejects configurations that would disable deadlines or permit unbounded batches.</summary>
    internal void Validate(string parameterName)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(MaximumSubscriptions, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(MaximumSubscriptions, 100_000);
        ArgumentOutOfRangeException.ThrowIfLessThan(BatchSize, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(BatchSize, 250);
        ArgumentOutOfRangeException.ThrowIfLessThan(MaximumMessageBytes, 1024);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(MaximumMessageBytes, 5 * 1024 * 1024);
        ValidateDuration(PollInterval);
        ValidateDuration(SendTimeout);
        ValidateDuration(QueryTimeout);
        if (PublicEndpoint is not null &&
            (!PublicEndpoint.IsAbsoluteUri ||
                PublicEndpoint.Scheme != Uri.UriSchemeHttps ||
                string.IsNullOrEmpty(PublicEndpoint.Host) ||
                !string.IsNullOrEmpty(PublicEndpoint.UserInfo) ||
                PublicEndpoint.AbsolutePath is not ("" or "/") ||
                !string.IsNullOrEmpty(PublicEndpoint.Query) ||
                !string.IsNullOrEmpty(PublicEndpoint.Fragment)))
        {
            throw new ArgumentException(
                "The public labeler endpoint must be an HTTPS origin without credentials, a path, a query, or a fragment.",
                parameterName);
        }
    }

    /// <summary>Requires finite, bounded timer periods supported on every target framework.</summary>
    private static void ValidateDuration(TimeSpan duration)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(duration, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(duration, TimeSpan.FromHours(1));
    }
}
