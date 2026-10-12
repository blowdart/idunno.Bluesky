// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;

using idunno.AtProto;
using idunno.AtProto.Authentication;
using idunno.Bluesky.Graph;
using idunno.Bluesky.Graph.Model;

using Microsoft.Extensions.Logging;

namespace idunno.Bluesky;

public static partial class BlueskyServer
{
    /// <inheritdoc cref="GetLists(AtIdentifier, int?, string?, string[], Uri, AccessCredentials?, HttpClient, Func{AtProtoCredential, CancellationToken, Task}?, ILoggerFactory?, IEnumerable{Did}?, int, CancellationToken)"/>
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Usage",
        "RS0027",
        Justification = "The shorter overload preserves the existing optional-parameter API; the extended overload adds the lexicon option.")]
    public static Task<AtProtoHttpResult<PagedViewReadOnlyCollection<ListView>>> GetLists(
        AtIdentifier actor,
        int? limit,
        string? cursor,
        Uri service,
        AccessCredentials? accessCredentials,
        HttpClient httpClient,
        Func<AtProtoCredential, CancellationToken, Task>? onCredentialsUpdated = null,
        ILoggerFactory? loggerFactory = default,
        IEnumerable<Did>? subscribedLabelers = null,
        int maximumResponseSize = AtProtoHttpClient.DefaultMaximumResponseSize,
        CancellationToken cancellationToken = default)
    {
        return GetLists(
            actor, limit, cursor, purposes: null, service: service, accessCredentials: accessCredentials, httpClient: httpClient,
            onCredentialsUpdated: onCredentialsUpdated, loggerFactory: loggerFactory, subscribedLabelers: subscribedLabelers,
            maximumResponseSize: maximumResponseSize, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Get a paged list of lists that created by the specified <paramref name="actor"/>.
    /// </summary>
    /// <param name="actor">The actor whose lists to enumerate.</param>
    /// <param name="limit">The maximum number of lists that should be return in a page.</param>
    /// <param name="cursor">An optional cursor for pagination.</param>
    /// <param name="purposes">An optional list-purpose filter, such as <c>modlist</c> or <c>curatelist</c>.</param>
    /// <param name="service">The <see cref="Uri"/> of the service to retrieve the lists from.</param>
    /// <param name="accessCredentials">The <see cref="AccessCredentials"/> used to authenticate to <paramref name="service"/>.</param>
    /// <param name="httpClient">An <see cref="HttpClient"/> to use when making a request to the <paramref name="service"/>.</param>
    /// <param name="onCredentialsUpdated">An <see cref="Func{T1, T2, TResult}" /> to await if the credentials in the request need updating.</param>
    /// <param name="loggerFactory">An instance of <see cref="ILoggerFactory"/> to use to create a logger.</param>
    /// <param name="subscribedLabelers">An optional list of <see cref="Did"/>s of labelers to retrieve labels applied to the post view.</param>
    /// <param name="maximumResponseSize">The maximum number of bytes to read from the response body.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="actor"/>, <paramref name="service"/> or <paramref name="httpClient"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="limit"/> is &lt; 1 or &gt; 100.</exception>
    /// <exception cref="ArgumentException">Thrown when an entry in <paramref name="purposes"/> is <see langword="null"/> or whitespace.</exception>
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
        Justification = "All types are preserved in the JsonSerializerOptions call to Get().")]
    [UnconditionalSuppressMessage("AOT",
        "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
        Justification = "All types are preserved in the JsonSerializerOptions call to Get().")]
    public static async Task<AtProtoHttpResult<PagedViewReadOnlyCollection<ListView>>> GetLists(
        AtIdentifier actor,
        int? limit,
        string? cursor,
        string[]? purposes,
        Uri service,
        AccessCredentials? accessCredentials,
        HttpClient httpClient,
        Func<AtProtoCredential, CancellationToken, Task>? onCredentialsUpdated,
        ILoggerFactory? loggerFactory,
        IEnumerable<Did>? subscribedLabelers,
        int maximumResponseSize,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(service);
        ArgumentNullException.ThrowIfNull(httpClient);

        if (limit is not null)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan((int)limit, 1);
            ArgumentOutOfRangeException.ThrowIfGreaterThan((int)limit, 100);
        }

        StringBuilder queryStringBuilder = new();
        queryStringBuilder.Append(CultureInfo.InvariantCulture, $"actor={Uri.EscapeDataString(actor.ToString())}");

        if (limit is not null)
        {
            queryStringBuilder.Append(CultureInfo.InvariantCulture, $"&limit={limit}");
        }

        if (cursor is not null)
        {
            queryStringBuilder.Append(CultureInfo.InvariantCulture, $"&cursor={Uri.EscapeDataString(cursor)}");
        }
        if (purposes is not null)
        {
            foreach (string purpose in purposes)
            {
                ArgumentException.ThrowIfNullOrWhiteSpace(purpose);
                queryStringBuilder.Append(CultureInfo.InvariantCulture, $"&purposes={Uri.EscapeDataString(purpose)}");
            }
        }

        string queryString = queryStringBuilder.ToString();

        BlueskyHttpClient<GetListsResponse> client = new(AppViewProxy, loggerFactory) { MaximumResponseSize = maximumResponseSize };
        AtProtoHttpResult<GetListsResponse> response = await client.Get(
            service,
            $"/xrpc/app.bsky.graph.getLists?{queryString}",
            credentials: accessCredentials,
            httpClient: httpClient,
            jsonSerializerOptions: BlueskyJsonSerializerOptions,
            onCredentialsUpdated: onCredentialsUpdated,
            subscribedLabelers: subscribedLabelers,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        if (response.Succeeded)
        {
            return new AtProtoHttpResult<PagedViewReadOnlyCollection<ListView>>(
                new PagedViewReadOnlyCollection<ListView>(WithoutNullEntries(response.Result.Lists, service, nameof(response.Result.Lists), loggerFactory), response.Result.Cursor),
                response.StatusCode,
                response.HttpResponseHeaders,
                response.AtErrorDetail,
                response.RateLimit);
        }
        else
        {
            return new AtProtoHttpResult<PagedViewReadOnlyCollection<ListView>>(
                null,
                response.StatusCode,
                response.HttpResponseHeaders,
                response.AtErrorDetail,
                response.RateLimit);
        }
    }
}
