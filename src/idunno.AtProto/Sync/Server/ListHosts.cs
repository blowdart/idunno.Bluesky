// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;

using idunno.AtProto.Authentication;
using idunno.AtProto.Sync;
using idunno.AtProto.Sync.Model;

using Microsoft.Extensions.Logging;

namespace idunno.AtProto;

public static partial class AtProtoServer
{
    /// <summary>
    /// Enumerates the upstream hosts, such as personal data servers or relays, that the specified service consumes from. Does not require authentication.
    /// </summary>
    /// <param name="limit">The maximum number of hosts to return. Must be between 1 and 1000 if specified. The server default is 200.</param>
    /// <param name="cursor">An optional cursor for pagination.</param>
    /// <param name="service">The relay to query.</param>
    /// <param name="httpClient">An <see cref="HttpClient"/> to use when making a request to the <paramref name="service"/>.</param>
    /// <param name="accessCredentials">Optional access credentials for the specified <paramref name="service"/>.</param>
    /// <param name="onCredentialsUpdated">An <see cref="Func{T1, T2, TResult}" /> to await if the credentials in the request need updating.</param>
    /// <param name="loggerFactory">An instance of <see cref="ILoggerFactory"/> to use to create a logger.</param>
    /// <param name="maximumResponseSize">The maximum number of bytes to read from the response body.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="service"/> or <paramref name="httpClient"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="limit"/> is less than 1 or greater than 1000.</exception>
    /// <exception cref="AccessTokenException">Thrown when <paramref name="accessCredentials"/> are not valid for the specified <paramref name="service"/>.</exception>
    /// <remarks>
    /// <para>
    /// This endpoint is only implemented by relays. Personal data servers do not implement it and will return an error,
    /// so <paramref name="service"/> should be a relay.
    /// </para>
    /// <para>The sort order of the hosts is not formally specified. The recommended order is by the time the host was first seen by the relay, oldest first.</para>
    /// </remarks>
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
        Justification = "All types are preserved in the JsonSerializerOptions call to Get().")]
    [UnconditionalSuppressMessage("AOT",
        "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
        Justification = "All types are preserved in the JsonSerializerOptions call to Get().")]
    public static async Task<AtProtoHttpResult<PagedReadOnlyCollection<HostDescription>>> ListHosts(
        int? limit,
        string? cursor,
        Uri service,
        HttpClient httpClient,
        AccessCredentials? accessCredentials = null,
        Func<AtProtoCredential, CancellationToken, Task>? onCredentialsUpdated = null,
        ILoggerFactory? loggerFactory = default,
        int maximumResponseSize = AtProtoHttpClient.DefaultMaximumResponseSize,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(service);
        ArgumentNullException.ThrowIfNull(httpClient);

        if (accessCredentials is not null && service != accessCredentials.Service)
        {
            throw new AccessTokenException("Credentials not valid for the specified service.");
        }

        if (limit is < 1 or > 1000)
        {
            throw new ArgumentOutOfRangeException(nameof(limit), limit, "limit must be between 1 and 1000.");
        }

        StringBuilder queryBuilder = new("/xrpc/com.atproto.sync.listHosts");
        char separator = '?';

        if (limit is not null)
        {
            queryBuilder.Append(CultureInfo.InvariantCulture, $"{separator}limit={limit}");
            separator = '&';
        }

        if (!string.IsNullOrWhiteSpace(cursor))
        {
            queryBuilder.Append(separator).Append("cursor=").Append(Uri.EscapeDataString(cursor));
        }

        AtProtoHttpClient<ListHostsResponse> request = new(loggerFactory) { MaximumResponseSize = maximumResponseSize };

        AtProtoHttpResult<ListHostsResponse> response = await request.Get(
            service: service,
            endpoint: queryBuilder.ToString(),
            credentials: accessCredentials,
            httpClient: httpClient,
            jsonSerializerOptions: AtProtoJsonSerializerOptions,
            onCredentialsUpdated: onCredentialsUpdated,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return new AtProtoHttpResult<PagedReadOnlyCollection<HostDescription>>(
            response.Succeeded
                ? new PagedReadOnlyCollection<HostDescription>(
                    WithoutNullEntries(response.Result.Hosts, service, nameof(response.Result.Hosts), loggerFactory),
                    response.Result.Cursor)
                : null,
            response.StatusCode,
            response.HttpResponseHeaders,
            response.AtErrorDetail,
            response.RateLimit);
    }
}
