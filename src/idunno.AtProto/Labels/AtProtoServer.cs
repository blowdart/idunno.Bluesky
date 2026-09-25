// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;

using idunno.AtProto.Authentication;
using idunno.AtProto.Labels;
using idunno.AtProto.Labels.Models;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace idunno.AtProto;

public static partial class AtProtoServer
{
    // https://docs.bsky.app/docs/api/com-atproto-label-query-labels
    internal const string QueryLabelsEndpoint = "/xrpc/com.atproto.label.queryLabels";

    /// <summary>
    /// Find labels relevant to the provided AT-URI patterns. Public endpoint for moderation services, though it may return different or additional results with authentication.
    /// </summary>
    /// <param name="uriPatterns">List of AT URI patterns to match (boolean 'OR'). Each may be a prefix (ending with ''; will match inclusive of the string leading to ''), or a full URI.</param>
    /// <param name="sources">Optional list of label sources (DIDs) to filter on.</param>
    /// <param name="limit">Number of results to return. Should be between 1 and 250.</param>
    /// <param name="cursor">An optional cursor for pagination.</param>
    /// <param name="service">The service to create fine the labels on.</param>
    /// <param name="accessCredentials">Optional access credentials to use to authenticate against the <paramref name="service"/>.</param>
    /// <param name="httpClient">An <see cref="HttpClient"/> to use when making a request to the <paramref name="service"/>.</param>
    /// <param name="onCredentialsUpdated">An <see cref="Func{T1, T2, TResult}" /> to await if the credentials in the request need updating.</param>
    /// <param name="loggerFactory">An instance of <see cref="ILoggerFactory"/> to use to create a logger.</param>
    /// <param name="maximumResponseSize">The maximum number of bytes to read from the response body.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="uriPatterns" />, <paramref name="service"/> or <paramref name="httpClient"/> is <see langword="null"/></exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown <paramref name="uriPatterns"/> is empty or if <paramref name="limit"/> is &lt;1 or &gt;250.</exception>
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
        Justification = "All types are preserved in the JsonSerializerOptions call to Get().")]
    [UnconditionalSuppressMessage("AOT",
        "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
        Justification = "All types are preserved in the JsonSerializerOptions call to Post().")]
    public static async Task<AtProtoHttpResult<PagedReadOnlyCollection<Label>>> QueryLabels(
        IEnumerable<string> uriPatterns,
        IEnumerable<Did>? sources,
        int? limit,
        string? cursor,
        Uri service,
        AccessCredentials? accessCredentials,
        HttpClient httpClient,
        Func<AtProtoCredential, CancellationToken, Task>? onCredentialsUpdated = null,
        ILoggerFactory? loggerFactory = default,
        int maximumResponseSize = AtProtoHttpClient.DefaultMaximumResponseSize,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(uriPatterns);
        ArgumentNullException.ThrowIfNull(service);
        ArgumentNullException.ThrowIfNull(httpClient);

        var uriPatternList = new List<string>(uriPatterns);
        if (uriPatternList.Count == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(uriPatterns), $"{nameof(uriPatterns)} must contain 1 or more patterns.");
        }
        if (limit is not null &&
           (limit < 1 || limit > 250))
        {
            throw new ArgumentOutOfRangeException(nameof(limit), string.Create(CultureInfo.InvariantCulture, $"{limit} must be between 1 and 250."));
        }

        List<Did> sourcesList = [];
        if (sources is not null)
        {
            sourcesList.AddRange(sources);
        }

        StringBuilder queryStringBuilder = new(@"?");
        queryStringBuilder.Append(string.Join("&", uriPatternList.Select(pattern => $"uriPatterns={Uri.EscapeDataString(pattern)}")));
        if (sourcesList.Count > 0)
        {
            queryStringBuilder.Append(string.Join("&", sourcesList.Select(source => $"sources={Uri.EscapeDataString(source)}")));
        }
        if (limit is not null)
        {
            queryStringBuilder.Append(CultureInfo.InvariantCulture, $"&limit={limit}");
        }
        if (cursor is not null && !string.IsNullOrWhiteSpace(cursor))
        {
            queryStringBuilder.Append(CultureInfo.InvariantCulture, $"&cursor={Uri.EscapeDataString(cursor)}");
        }

        AtProtoHttpClient<QueryLabelsResponse> request = new(loggerFactory) { MaximumResponseSize = maximumResponseSize };

        AtProtoHttpResult<QueryLabelsResponse> response = await request.Get(
            service,
            $"{QueryLabelsEndpoint}{queryStringBuilder}",
            accessCredentials,
            httpClient,
            onCredentialsUpdated: onCredentialsUpdated,
            jsonSerializerOptions: AtProtoJsonSerializerOptions,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        if (response.Succeeded)
        {
            return new AtProtoHttpResult<PagedReadOnlyCollection<Label>>(
                new PagedReadOnlyCollection<Label>(WithoutNullEntries(response.Result.Labels, service, nameof(response.Result.Labels), loggerFactory), cursor),
                response.StatusCode,
                response.HttpResponseHeaders,
                response.AtErrorDetail,
                response.RateLimit);
        }
        else
        {
            return new AtProtoHttpResult<PagedReadOnlyCollection<Label>>(
                null,
                response.StatusCode,
                response.HttpResponseHeaders,
                response.AtErrorDetail,
                response.RateLimit);
        }
    }

    /// <summary>
    /// Returns the non <see langword="null"/> entries in <paramref name="source"/>, logging a warning if any were skipped.
    /// </summary>
    /// <remarks>
    /// <para>Neither <see cref="System.Text.Json.Serialization.JsonRequiredAttribute"/> nor
    /// <see cref="System.Text.Json.JsonSerializerOptions.RespectNullableAnnotations"/> applies to a collection's element
    /// type, so a service can return a <see langword="null"/> entry inside an otherwise well formed collection.</para>
    /// </remarks>
    private static List<T> WithoutNullEntries<T>(
        IEnumerable<T> source,
        Uri service,
        string collection,
        ILoggerFactory? loggerFactory,
        [CallerMemberName] string caller = "") where T : class
    {
        List<T> entries = [];
        int skipped = 0;

        foreach (T? entry in source)
        {
            if (entry is null)
            {
                skipped++;
            }
            else
            {
                entries.Add(entry);
            }
        }

        if (skipped != 0)
        {
            ILogger logger = loggerFactory?.CreateLogger(nameof(AtProtoServer)) ?? NullLogger.Instance;
            Logger.SkippedNullCollectionEntries(logger, caller, skipped, collection, service);
        }

        return entries;
    }
}