// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;

using idunno.AtProto;
using idunno.AtProto.Authentication;
using idunno.Bluesky.Chat.Convo.Model;
using idunno.Bluesky.Feed;

namespace idunno.Bluesky;

public partial class BlueskyAgent
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
    /// <param name="subscribedLabelers">An optional list of <see cref="Did"/>s of labelers to retrieve labels applied to the post view.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentException">Thrown when both <paramref name="repliesOnly"/> and <paramref name="excludeReplies"/> are set.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="limit"/> is less than 1 or greater than 100.</exception>
    /// <exception cref="AuthenticationRequiredException">Thrown when the agent is not authenticated.</exception>
    [SuppressMessage("Documentation", "CSENSE020:Potential ghost parameter reference in documentation", Justification = "Not a ghost reference in summary.")]
    public async Task<AtProtoHttpResult<SearchV2Results>> SearchPostsV2(
        string? cursor = null,
        int? limit = null,
        string? query = null,
        SortOrder? sort = null,
        ICollection<AtIdentifier>? authors = null,
        ICollection<AtIdentifier>? mentions = null,
        ICollection<string>? domains = null,
        ICollection<Uri>? urls = null,
        ICollection<AtUri>? embeddedAtUris = null,
        ICollection<string>? hashTags = null,
        ICollection<AtIdentifier>? excludeAuthors = null,
        ICollection<AtIdentifier>? excludeMentions = null,
        ICollection<string>? excludeDomains = null,
        ICollection<Uri>? excludeUrls = null,
        ICollection<AtUri>? excludeEmbeddedAtUris = null,
        ICollection<string>? excludeHashTags = null,
        DateTimeOffset? since = null,
        DateTimeOffset? until = null,
        bool? allTime = null,
        ICollection<string>? languages = null,
        ICollection<string>? excludeLanguages = null,
        bool? hasMedia = null,
        bool? hasVideo = null,
        AtUri? replyParentUri = null,
        AtUri? threadRootUri = null,
        bool? excludeReplies = null,
        bool? repliesOnly = null,
        bool? following = null,
        string? queryLanguage = null,
        IEnumerable<Did>? subscribedLabelers = null,
        CancellationToken cancellationToken = default)
    {
        if (!IsAuthenticated)
        {
            throw new AuthenticationRequiredException();
        }

        if (repliesOnly is not null && excludeReplies is not null)
        {
            throw new ArgumentException($"Cannot set both {nameof(repliesOnly)} and {nameof(excludeReplies)}.");
        }

        if (limit is not null)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan((int)limit, 1);
            ArgumentOutOfRangeException.ThrowIfGreaterThan((int)limit, 100);
        }

        return await BlueskyServer.SearchPostsV2(
            cursor,
            limit,
            query,
            sort,
            authors,
            mentions,
            domains,
            urls,
            embeddedAtUris,
            hashTags,
            excludeAuthors,
            excludeMentions,
            excludeDomains,
            excludeUrls,
            excludeEmbeddedAtUris,
            excludeHashTags,
            since,
            until,
            allTime,
            languages,
            excludeLanguages,
            hasMedia,
            hasVideo,
            replyParentUri,
            threadRootUri,
            excludeReplies,
            repliesOnly,
            following,
            queryLanguage,
            AuthenticatedOrUnauthenticatedServiceUri,
            accessCredentials: Credentials,
            httpClient: HttpClient,
            onCredentialsUpdated: InternalOnCredentialsUpdatedCallBack,
            loggerFactory: LoggerFactory,
            subscribedLabelers: subscribedLabelers,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}
