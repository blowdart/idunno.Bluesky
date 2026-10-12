// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Globalization;

using idunno.AtProto.Authentication;
using idunno.AtProto.Repo;
using idunno.AtProto.Repo.Models;

using Microsoft.Extensions.Logging;

namespace idunno.AtProto;

public static partial class AtProtoServer
{
    /// <summary>
    /// Lists blobs referenced by the authenticated account's repository that are not present on its personal data server.
    /// </summary>
    /// <param name="limit">The maximum number of blobs to return. Must be between 1 and 1000 if specified; the server default is 500.</param>
    /// <param name="cursor">An optional cursor for pagination.</param>
    /// <param name="service">The personal data server hosting the authenticated account.</param>
    /// <param name="accessCredentials">The credentials used to authenticate to <paramref name="service"/>.</param>
    /// <param name="httpClient">The HTTP client to use when making the request.</param>
    /// <param name="onCredentialsUpdated">A callback to invoke if the credentials are updated during the request.</param>
    /// <param name="loggerFactory">A logger factory to use when creating a logger.</param>
    /// <param name="maximumResponseSize">The maximum number of bytes to read from the response body.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the request.</param>
    /// <returns>A page of missing blobs and an optional pagination cursor.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="service"/>, <paramref name="accessCredentials"/> or <paramref name="httpClient"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="limit"/> is less than 1 or greater than 1000.</exception>
    /// <exception cref="AccessTokenException">Thrown when <paramref name="accessCredentials"/> are not valid for <paramref name="service"/>.</exception>
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
        Justification = "All types are preserved in the JsonSerializerOptions call to Get().")]
    [UnconditionalSuppressMessage("AOT",
        "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
        Justification = "All types are preserved in the JsonSerializerOptions call to Get().")]
    public static async Task<AtProtoHttpResult<PagedReadOnlyCollection<MissingBlob>>> ListMissingBlobs(
        int? limit,
        string? cursor,
        Uri service,
        AccessCredentials accessCredentials,
        HttpClient httpClient,
        Func<AtProtoCredential, CancellationToken, Task>? onCredentialsUpdated = null,
        ILoggerFactory? loggerFactory = default,
        int maximumResponseSize = AtProtoHttpClient.DefaultMaximumResponseSize,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(service);
        ArgumentNullException.ThrowIfNull(accessCredentials);
        ArgumentNullException.ThrowIfNull(httpClient);

        if (accessCredentials.Service != service)
        {
            throw new AccessTokenException("Credentials not valid for the specified service.");
        }

        if (limit is < 1 or > 1000)
        {
            throw new ArgumentOutOfRangeException(nameof(limit), limit, "limit must be between 1 and 1000.");
        }

        string queryString = string.Empty;

        if (limit is not null)
        {
            queryString = string.Create(CultureInfo.InvariantCulture, $"?limit={limit}");
        }

        if (cursor is not null)
        {
            queryString += $"{(queryString.Length is 0 ? "?" : "&")}cursor={Uri.EscapeDataString(cursor)}";
        }

        AtProtoHttpClient<ListMissingBlobsResponse> client = new(loggerFactory) { MaximumResponseSize = maximumResponseSize };

        AtProtoHttpResult<ListMissingBlobsResponse> response = await client.Get(
            service,
            $"/xrpc/com.atproto.repo.listMissingBlobs{queryString}",
            accessCredentials,
            httpClient,
            onCredentialsUpdated: onCredentialsUpdated,
            jsonSerializerOptions: AtProtoJsonSerializerOptions,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        if (response.Succeeded)
        {
            return new AtProtoHttpResult<PagedReadOnlyCollection<MissingBlob>>(
                new PagedReadOnlyCollection<MissingBlob>(response.Result.Blobs, response.Result.Cursor),
                response.StatusCode,
                response.HttpResponseHeaders,
                response.AtErrorDetail,
                response.RateLimit);
        }

        return new AtProtoHttpResult<PagedReadOnlyCollection<MissingBlob>>(
            null,
            response.StatusCode,
            response.HttpResponseHeaders,
            response.AtErrorDetail,
            response.RateLimit);
    }
}
