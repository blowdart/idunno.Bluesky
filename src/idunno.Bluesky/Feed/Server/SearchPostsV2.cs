// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;

using idunno.AtProto;
using idunno.AtProto.Authentication;
using idunno.Bluesky.Feed;
using idunno.Bluesky.Feed.Model;

using Microsoft.Extensions.Logging;

namespace idunno.Bluesky;

public static partial class BlueskyServer
{
    /// <summary>
    /// Find posts matching a search query or filters, returning search hits for matching post records.
    /// </summary>
    /// <param name="cursor">Optional pagination cursor.</param>
    /// <param name="limit">Optional maximum number of results to return.</param>
    /// <param name="query">Search query string. A query or at least one filter is required.</param>
    /// <param name="sort">Ranking order for results. 'recent' sorts by recency; 'top' uses search ranking.</param>
    /// <param name="authors">If provided, include posts by any of these authors. <see cref="Handle"/>s are resolved to DIDs before searching.</param>
    /// <param name="mentions">If provided, include posts that mention any of these accounts. <see cref="Handle"/>s are resolved to DIDs before searching.</param>
    /// <param name="domains">If provided, include posts that link to any of these domains.</param>
    /// <param name="urls">If provided, include posts with links pointing to any of these <see cref="Uri"/>.</param>
    /// <param name="embeddedAtUris">If provided, include posts with embed any of the provided <see cref="AtUri"/>.</param>
    /// <param name="hashTags">If provided, include posts with tagged with any of these hashtags. Do not include the hash (#) prefix.</param>
    /// <param name="excludeAuthors">If provided, exclude posts by any of these authors. <see cref="Handle"/>s are resolved to DIDs before searching.</param>
    /// <param name="excludeMentions">If provided, exclude posts that mention any of these accounts. <see cref="Handle"/>s are resolved to DIDs before searching.</param>
    /// <param name="excludeDomains">If provided, exclude posts that link to any of these domains.</param>
    /// <param name="excludeUrls">If provided, exclude posts with links pointing to any of these <see cref="Uri"/>.</param>
    /// <param name="excludeEmbeddedAtUris">If provided, exclude posts with embed any of the provided <see cref="AtUri"/>.</param>
    /// <param name="excludeHashTags">If provided, exclude posts with tagged with any of these hashtags. Do not include the hash (#) prefix.</param>
    /// <param name="since">If provided, include posts indexed at or after this timestamp. </param>
    /// <param name="until">If provided, include posts indexed before this timestamp.</param>
    /// <param name="allTime">Optional flag indicating whether to search the full index instead of the recent-post window.</param>
    /// <param name="languages">If provided, include posts whose language matches any of these language codes.</param>
    /// <param name="excludeLanguages">If provided, exclude posts whose language matches any of these language codes.</param>
    /// <param name="hasMedia">Optional flag indicating whether to only include posts with media.</param>
    /// <param name="hasVideo">Optional flag indicating whether to only include posts with video.</param>
    /// <param name="replyParentUri">If provided, Include only direct replies to this parent post <see cref="AtUri"/>.</param>
    /// <param name="threadRootUri">If provided, include only posts in the thread rooted at this post <see cref="AtUri"/>.</param>
    /// <param name="excludeReplies">Optional flag indicating whether to exclude replies from results. Mutually exclusive with <paramref name="repliesOnly"/>.</param>
    /// <param name="repliesOnly">Optional flag indicating whether to include only replies in results. Mutually exclusive with <paramref name="excludeReplies"/>.</param>
    /// <param name="following">Optional flag indicating whether to include only posts from accounts followed by the viewer.</param>
    /// <param name="queryLanguage">Optional Language analyzer hint for the query text. If unset, the server auto-detects when possible.</param>
    /// <param name="service">The <see cref="Uri"/> of the service to retrieve search information from.</param>
    /// <param name="accessCredentials">The <see cref="AccessCredentials"/> used to authenticate to <paramref name="service"/>.</param>
    /// <param name="httpClient">An <see cref="HttpClient"/> to use when making a request to the <paramref name="service"/>.</param>
    /// <param name="onCredentialsUpdated">An <see cref="Action{T}" /> to call if the credentials in the request need updating.</param>
    /// <param name="loggerFactory">An instance of <see cref="ILoggerFactory"/> to use to create a logger.</param>
    /// <param name="subscribedLabelers">An optional list of <see cref="Did"/>s of labelers to retrieve labels applied to the post view.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="service"/>, <paramref name="accessCredentials"/>, or <paramref name="httpClient"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown if any of the provided arguments are invalid.</exception>
    [SuppressMessage("Documentation", "CSENSE020:Potential ghost parameter reference in documentation", Justification = "Not a ghost reference in summary.")]
    [SuppressMessage("Globalization", "CA1308:Normalize strings to uppercase", Justification = "The api demands lowercase.")]
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
        Justification = "All types are preserved in the JsonSerializerOptions call to Get().")]
    [UnconditionalSuppressMessage("AOT",
        "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
        Justification = "All types are preserved in the JsonSerializerOptions call to Get().")]
    public static async Task<AtProtoHttpResult<SearchV2Results>> SearchPostsV2(
        string? cursor,
        int? limit,
        string? query,
        SortOrder? sort,
        ICollection<AtIdentifier>? authors,
        ICollection<AtIdentifier>? mentions,
        ICollection<string>? domains,
        ICollection<Uri>? urls,
        ICollection<AtUri>? embeddedAtUris,
        ICollection<string>? hashTags,
        ICollection<AtIdentifier>? excludeAuthors,
        ICollection<AtIdentifier>? excludeMentions,
        ICollection<string>? excludeDomains,
        ICollection<Uri>? excludeUrls,
        ICollection<AtUri>? excludeEmbeddedAtUris,
        ICollection<string>? excludeHashTags,
        DateTimeOffset? since,
        DateTimeOffset? until,
        bool? allTime,
        ICollection<string>? languages,
        ICollection<string>? excludeLanguages,
        bool? hasMedia,
        bool? hasVideo,
        AtUri? replyParentUri,
        AtUri? threadRootUri,
        bool? excludeReplies,
        bool? repliesOnly,
        bool? following,
        string? queryLanguage,
        Uri service,
        AccessCredentials accessCredentials,
        HttpClient httpClient,
        Action<AtProtoCredential>? onCredentialsUpdated = null,
        ILoggerFactory? loggerFactory = default,
        IEnumerable<Did>? subscribedLabelers = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(service);
        ArgumentNullException.ThrowIfNull(accessCredentials);
        ArgumentNullException.ThrowIfNull(httpClient);

        if (repliesOnly is not null && excludeReplies is not null)
        {
            throw new ArgumentException($"Cannot set both {nameof(repliesOnly)} and {nameof(excludeReplies)}.");
        }

        if (limit is not null)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan((int)limit, 1);
            ArgumentOutOfRangeException.ThrowIfGreaterThan((int)limit, 100);
        }

        StringBuilder queryStringBuilder = new();

        if (query is not null)
        {
            queryStringBuilder.Append(CultureInfo.InvariantCulture, $"query={Uri.EscapeDataString(query)}");
        }

        if (sort is not null)
        {
            queryStringBuilder.Append(CultureInfo.InvariantCulture, $"&sort={sort.ToString()!.ToLowerInvariant()}");
        }

        if (authors is not null && authors.Count > 0)
        {
            foreach (AtIdentifier author in authors)
            {
                queryStringBuilder.Append(CultureInfo.InvariantCulture, $"&authors={Uri.EscapeDataString(author.ToString())}");
            }
        }

        if (mentions is not null && mentions.Count > 0)
        {
            foreach (AtIdentifier mention in mentions)
            {
                queryStringBuilder.Append(CultureInfo.InvariantCulture, $"&mentions={Uri.EscapeDataString(mention.ToString())}");
            }
        }

        if (domains is not null && domains.Count > 0)
        {
            foreach (string domain in domains)
            {
                queryStringBuilder.Append(CultureInfo.InvariantCulture, $"&domains={Uri.EscapeDataString(domain)}");
            }
        }

        if (urls is not null && urls.Count > 0)
        {
            foreach (Uri url in urls)
            {
                queryStringBuilder.Append(CultureInfo.InvariantCulture, $"&urls={Uri.EscapeDataString(url.ToString())}");
            }
        }

        if (embeddedAtUris is not null && embeddedAtUris.Count > 0)
        {
            foreach (AtUri embeddedAtUri in embeddedAtUris)
            {
                queryStringBuilder.Append(CultureInfo.InvariantCulture, $"&embedded={Uri.EscapeDataString(embeddedAtUri.ToString())}");
            }
        }

        if (hashTags is not null && hashTags.Count > 0)
        {
            foreach (string hashTag in hashTags)
            {
                queryStringBuilder.Append(CultureInfo.InvariantCulture, $"&hashtags={Uri.EscapeDataString(hashTag)}");
            }
        }

        if (excludeAuthors is not null && excludeAuthors.Count > 0)
        {
            foreach (AtIdentifier excludeAuthor in excludeAuthors)
            {
                queryStringBuilder.Append(CultureInfo.InvariantCulture, $"&excludeAuthors={Uri.EscapeDataString(excludeAuthor.ToString())}");
            }
        }

        if (excludeMentions is not null && excludeMentions.Count > 0)
        {
            foreach (AtIdentifier excludeMention in excludeMentions)
            {
                queryStringBuilder.Append(CultureInfo.InvariantCulture, $"&excludeMentions={Uri.EscapeDataString(excludeMention.ToString())}");
            }
        }

        if (excludeDomains is not null && excludeDomains.Count > 0)
        {
            foreach (string excludeDomain in excludeDomains)
            {
                queryStringBuilder.Append(CultureInfo.InvariantCulture, $"&excludeDomains={Uri.EscapeDataString(excludeDomain)}");
            }
        }

        if (excludeUrls is not null && excludeUrls.Count > 0)
        {
            foreach (Uri excludeUrl in excludeUrls)
            {
                queryStringBuilder.Append(CultureInfo.InvariantCulture, $"&excludeUrls={Uri.EscapeDataString(excludeUrl.ToString())}");
            }
        }

        if (excludeEmbeddedAtUris is not null && excludeEmbeddedAtUris.Count > 0)
        {
            foreach (AtUri excludeEmbeddedAtUri in excludeEmbeddedAtUris)
            {
                queryStringBuilder.Append(CultureInfo.InvariantCulture, $"&excludeEmbedded={Uri.EscapeDataString(excludeEmbeddedAtUri.ToString())}");
            }
        }

        if (excludeHashTags is not null && excludeHashTags.Count > 0)
        {
            foreach (string excludeHashTag in excludeHashTags)
            {
                queryStringBuilder.Append(CultureInfo.InvariantCulture, $"&excludeHashtags={Uri.EscapeDataString(excludeHashTag)}");
            }
        }

        if (since is not null)
        {
            queryStringBuilder.Append(CultureInfo.InvariantCulture, $"&since={Uri.EscapeDataString(since.Value.ToString("o", CultureInfo.InvariantCulture))}");
        }

        if (until is not null)
        {
            queryStringBuilder.Append(CultureInfo.InvariantCulture, $"&until={Uri.EscapeDataString(until.Value.ToString("o", CultureInfo.InvariantCulture))}");
        }

        if (allTime is not null)
        {
            queryStringBuilder.Append(CultureInfo.InvariantCulture, $"&until={Uri.EscapeDataString(allTime.Value.ToString(CultureInfo.InvariantCulture).ToLowerInvariant())}");
        }

        if (languages is not null && languages.Count > 0)
        {
            foreach (string language in languages)
            {
                queryStringBuilder.Append(CultureInfo.InvariantCulture, $"&languages={Uri.EscapeDataString(language)}");
            }
        }

        if (excludeLanguages is not null && excludeLanguages.Count > 0)
        {
            foreach (string excludeLanguage in excludeLanguages)
            {
                queryStringBuilder.Append(CultureInfo.InvariantCulture, $"&excludeLanguages={Uri.EscapeDataString(excludeLanguage)}");
            }
        }

        if (hasMedia is not null)
        {
            queryStringBuilder.Append(CultureInfo.InvariantCulture, $"&hasMedia={Uri.EscapeDataString(hasMedia.Value.ToString(CultureInfo.InvariantCulture).ToLowerInvariant())}");
        }

        if (hasVideo is not null)
        {
            queryStringBuilder.Append(CultureInfo.InvariantCulture, $"&hasVideo={Uri.EscapeDataString(hasVideo.Value.ToString(CultureInfo.InvariantCulture).ToLowerInvariant())}");
        }

        if (replyParentUri is not null)
        {
            queryStringBuilder.Append(CultureInfo.InvariantCulture, $"&replyParent={Uri.EscapeDataString(replyParentUri.ToString())}");
        }

        if (threadRootUri is not null)
        {
            queryStringBuilder.Append(CultureInfo.InvariantCulture, $"&threadRoot={Uri.EscapeDataString(threadRootUri.ToString())}");
        }

        if (excludeReplies is not null)
        {
            queryStringBuilder.Append(CultureInfo.InvariantCulture, $"&excludeReplies={Uri.EscapeDataString(excludeReplies.Value.ToString(CultureInfo.InvariantCulture).ToLowerInvariant())}");
        }

        if (repliesOnly is not null)
        {
            queryStringBuilder.Append(CultureInfo.InvariantCulture, $"&repliesOnly={Uri.EscapeDataString(repliesOnly.Value.ToString(CultureInfo.InvariantCulture).ToLowerInvariant())}");
        }

        if (following is not null)
        {
            queryStringBuilder.Append(CultureInfo.InvariantCulture, $"&following={Uri.EscapeDataString(following.Value.ToString(CultureInfo.InvariantCulture).ToLowerInvariant())}");
        }

        if (queryLanguage is not null)
        {
            queryStringBuilder.Append(CultureInfo.InvariantCulture, $"&queryLanguage={Uri.EscapeDataString(queryLanguage)}");
        }

        if (queryStringBuilder.Length == 0)
        {
            throw new ArgumentException("A query or at least one filter is required.");
        }

        if (limit is not null)
        {
            queryStringBuilder.Append(CultureInfo.InvariantCulture, $"&limit={limit}");
        }
        if (!string.IsNullOrWhiteSpace(cursor))
        {
            queryStringBuilder.Append(CultureInfo.InvariantCulture, $"&cursor={Uri.EscapeDataString(cursor)}");
        }

        string queryString = queryStringBuilder.ToString();

        BlueskyHttpClient<SearchPostsV2Response> client = new(AppViewProxy, loggerFactory);
        AtProtoHttpResult<SearchPostsV2Response> response = await client.Get(
            service,
            $"/xrpc/app.bsky.feed.searchPostsV2?{queryString}",
            credentials: accessCredentials,
            httpClient: httpClient,
            jsonSerializerOptions: BlueskyJsonSerializerOptions,
            onCredentialsUpdated: onCredentialsUpdated,
            subscribedLabelers: subscribedLabelers,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        if (response.Succeeded)
        {
            return new AtProtoHttpResult<SearchV2Results>(
                new SearchV2Results(response.Result.Posts, response.Result.HitsTotal, response.Result.Cursor, response.Result.DetectedQueryLanguages),
                response.StatusCode,
                response.HttpResponseHeaders,
                response.AtErrorDetail,
                response.RateLimit);
        }
        else
        {
            return new AtProtoHttpResult<SearchV2Results>(
                new SearchV2Results([], null, null, null),
                response.StatusCode,
                response.HttpResponseHeaders,
                response.AtErrorDetail,
                response.RateLimit);
        }
    }
}
