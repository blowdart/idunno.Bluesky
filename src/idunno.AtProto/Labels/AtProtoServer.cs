// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Globalization;
using System.Text;
using idunno.AtProto.Labels;
using idunno.AtProto.Models;

namespace idunno.AtProto
{
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
        /// <param name="accessToken">An optional access token to use to authenticate against the <paramref name="service"/>.</param>
        /// <param name="httpClient">An <see cref="HttpClient"/> to use when making a request to the <paramref name="service"/>.</param>
        /// <param name="subscribedLabelers">An optional list of <see cref="Did"/>s of labelers to retrieve labels applied to the account.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="uriPatterns" /> is null</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown <paramref name="uriPatterns"/> is empty or if <paramref name="limit"/> is &lt;1 or &gt;250.</exception>
        public static async Task<AtProtoHttpResult<PagedReadOnlyCollection<Label>>> QueryLabels(
            IEnumerable<string> uriPatterns,
            IEnumerable<Did>? sources,
            int? limit,
            string? cursor,
            Uri service,
            string? accessToken,
            HttpClient httpClient,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(uriPatterns);

            var uriPatternList = new List<string>(uriPatterns);
            if (uriPatternList.Count == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(uriPatterns), $"{nameof(uriPatterns)} must contain 1 or more patterns.");
            }
            if (limit is not null &&
               (limit < 1 || limit > 250))
            {
                throw new ArgumentOutOfRangeException(nameof(limit), "{limit} must be between 1 and 250.");
            }

            List<Did> sourcesList = new();
            if (sources is not null)
            {
                sourcesList.AddRange(sources);
            }

            StringBuilder queryStringBuilder = new (@"?");
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

            AtProtoHttpClient<QueryLabelsResponse> request = new();

            AtProtoHttpResult<QueryLabelsResponse> response = await request.Get(
                service,
                $"{QueryLabelsEndpoint}{queryStringBuilder}",
                accessToken,
                httpClient,
                cancellationToken: cancellationToken
                ).ConfigureAwait(false);

            if (response.Succeeded)
            {
                return new AtProtoHttpResult<PagedReadOnlyCollection<Label>>(
                    new PagedReadOnlyCollection<Label>(response.Result.Labels, cursor), response.StatusCode, response.AtErrorDetail, response.RateLimit);
            }
            else
            {
                return new AtProtoHttpResult<PagedReadOnlyCollection<Label>>(null, response.StatusCode, response.AtErrorDetail, response.RateLimit);
            }
        }
    }
}
