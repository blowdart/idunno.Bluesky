// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Globalization;

using idunno.AtProto;
using idunno.AtProto.Authentication;
using idunno.Bluesky.Actor;
using idunno.Bluesky.Contact.Model;

using Microsoft.Extensions.Logging;

namespace idunno.Bluesky;

public static partial class BlueskyServer
{
    /// <summary>
    /// Gets mutually imported contact matches, excluding dismissed matches.
    /// </summary>
    /// <param name="limit">The page size, between 1 and 100; the service default is 50.</param>
    /// <param name="cursor">An optional pagination cursor.</param>
    /// <param name="service">The personal data server to proxy the request through.</param>
    /// <param name="accessCredentials">The credentials used to authenticate the request.</param>
    /// <param name="httpClient">The HTTP client used to make the request.</param>
    /// <param name="onCredentialsUpdated">An optional callback to persist updated credentials.</param>
    /// <param name="loggerFactory">An optional factory for request logging.</param>
    /// <param name="maximumResponseSize">The maximum response body size in bytes.</param>
    /// <param name="cancellationToken">A cancellation token for the operation.</param>
    /// <returns>The task representing the authenticated contact request.</returns>
    /// <exception cref="ArgumentNullException">An argument documented as required is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The page size is less than 1 or greater than 100, or the maximum response size is not positive.</exception>
    /// <remarks>
    /// <para>Requires authentication. Named errors: <c>InvalidDid</c>, <c>InvalidLimit</c>, <c>InvalidCursor</c>, <c>InternalError</c>.</para>
    /// </remarks>
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Contact request and response types are source-generated; ContactDiscoveryTests exercises this endpoint with reflection disabled.")]
    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = "Contact request and response types are source-generated; ContactDiscoveryTests exercises this endpoint with reflection disabled.")]
    public static async Task<AtProtoHttpResult<PagedViewReadOnlyCollection<ProfileView>>> GetContactMatches(
        int? limit,
        string? cursor,
        Uri service,
        AccessCredentials accessCredentials,
        HttpClient httpClient,
        Func<AtProtoCredential, CancellationToken, Task>? onCredentialsUpdated = null,
        ILoggerFactory? loggerFactory = null,
        int maximumResponseSize = AtProtoHttpClient.DefaultMaximumResponseSize,
        CancellationToken cancellationToken = default)
    {
        if (limit is not null)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(limit.Value, 1);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(limit.Value, 100);
        }
        ArgumentNullException.ThrowIfNull(service);
        ArgumentNullException.ThrowIfNull(accessCredentials);
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumResponseSize);

        List<string> parameters = [];
        if (limit is not null)
        {
            parameters.Add($"limit={limit.Value.ToString(CultureInfo.InvariantCulture)}");
        }

        if (cursor is not null)
        {
            parameters.Add($"cursor={Uri.EscapeDataString(cursor)}");
        }

        string query = parameters.Count == 0 ? string.Empty : $"?{string.Join("&", parameters)}";

        BlueskyHttpClient<GetContactMatchesResponse> client = new(AppViewProxy, loggerFactory) { MaximumResponseSize = maximumResponseSize };
        AtProtoHttpResult<GetContactMatchesResponse> response = await client.Get(
            service: service,
            endpoint: $"/xrpc/app.bsky.contact.getMatches{query}",
            credentials: accessCredentials,
            httpClient: httpClient,
            jsonSerializerOptions: BlueskyJsonSerializerOptions,
            onCredentialsUpdated: onCredentialsUpdated,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return new AtProtoHttpResult<PagedViewReadOnlyCollection<ProfileView>>(
            response.Succeeded ? new PagedViewReadOnlyCollection<ProfileView>(
                    WithoutNullEntries(response.Result.Matches, service, nameof(response.Result.Matches), loggerFactory), response.Result.Cursor) : null,
            response.StatusCode,
            response.HttpResponseHeaders,
            response.AtErrorDetail,
            response.RateLimit);
    }
}
