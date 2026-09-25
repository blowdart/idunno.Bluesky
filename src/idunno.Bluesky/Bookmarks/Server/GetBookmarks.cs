// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;

using idunno.AtProto;
using idunno.AtProto.Authentication;
using idunno.Bluesky.Bookmarks;
using idunno.Bluesky.Bookmarks.Model;

using Microsoft.Extensions.Logging;

namespace idunno.Bluesky;

public static partial class BlueskyServer
{
    // Bookmarks - https://github.com/bluesky-social/atproto/pull/4163

    /// <summary>
    /// Gets a pageable list of the specified user's bookmarks.
    /// </summary>
    /// <param name="cursor">An optional cursor for pagination.</param>
    /// <param name="limit">The maximum number of bookmarks to return.</param>
    /// <param name="service">The <see cref="Uri"/> of the service to retrieve the profile from.</param>
    /// <param name="accessCredentials">The <see cref="AccessCredentials"/> used to authenticate to <paramref name="service"/>.</param>
    /// <param name="httpClient">An <see cref="HttpClient"/> to use when making a request to the <paramref name="service"/>.</param>
    /// <param name="onCredentialsUpdated">An <see cref="Func{T1, T2, TResult}" /> to await if the credentials in the request need updating.</param>
    /// <param name="loggerFactory">An instance of <see cref="ILoggerFactory"/> to use to create a logger.</param>
    /// <param name="subscribedLabelers">An optional list of <see cref="Did"/>s of labelers to retrieve labels applied to the account.</param>
    /// <param name="maximumResponseSize">The maximum number of bytes to read from the response body.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when any of <paramref name="service"/>, <paramref name="accessCredentials"/> or <paramref name="httpClient"/> are <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="limit"/> is &lt;1 or &gt;<see cref="Maximum.Bookmarks"/>.</exception>
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
        Justification = "All types are preserved in the JsonSerializerOptions call to Get().")]
    [UnconditionalSuppressMessage("AOT",
        "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
        Justification = "All types are preserved in the JsonSerializerOptions call to Get().")]
    public static async Task<AtProtoHttpResult<PagedViewReadOnlyCollection<BookmarkView>>> GetBookmarks(
        string? cursor,
        int? limit,
        Uri service,
        AccessCredentials accessCredentials,
        HttpClient httpClient,
        Func<AtProtoCredential, CancellationToken, Task>? onCredentialsUpdated = null,
        ILoggerFactory? loggerFactory = default,
        IEnumerable<Did>? subscribedLabelers = null,
        int maximumResponseSize = AtProtoHttpClient.DefaultMaximumResponseSize,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(service);
        ArgumentNullException.ThrowIfNull(accessCredentials);
        ArgumentNullException.ThrowIfNull(httpClient);

        if (limit is not null)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan((int)limit, 1);
            ArgumentOutOfRangeException.ThrowIfGreaterThan((int)limit, Maximum.Bookmarks);
        }

        StringBuilder queryString = new();

        if (limit is not null)
        {
            queryString.Append(CultureInfo.InvariantCulture, $"limit={limit}&");
        }

        if (!string.IsNullOrEmpty(cursor))
        {
            queryString.Append(CultureInfo.InvariantCulture, $"cursor={Uri.EscapeDataString(cursor)}&");
        }

        if (queryString.Length > 0)
        {
            queryString.Length--;
        }

        BlueskyHttpClient<GetBookmarksResponse> request = new(AppViewProxy, loggerFactory) { MaximumResponseSize = maximumResponseSize };

        AtProtoHttpResult<GetBookmarksResponse> response = await request.Get(
            service,
            $"/xrpc/app.bsky.bookmark.getBookmarks?{queryString}",
            credentials: accessCredentials,
            httpClient: httpClient,
            jsonSerializerOptions: BlueskyJsonSerializerOptions,
            onCredentialsUpdated: onCredentialsUpdated,
            subscribedLabelers: subscribedLabelers,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        if (response.Succeeded)
        {
            return new AtProtoHttpResult<PagedViewReadOnlyCollection<BookmarkView>>(
                new PagedViewReadOnlyCollection<BookmarkView>(WithoutNullEntries(response.Result.Bookmarks, service, nameof(response.Result.Bookmarks), loggerFactory).AsReadOnly(), response.Result.Cursor),
                response.StatusCode,
                response.HttpResponseHeaders,
                response.AtErrorDetail,
                response.RateLimit);
        }
        else
        {
            return new AtProtoHttpResult<PagedViewReadOnlyCollection<BookmarkView>>(
                default,
                response.StatusCode,
                response.HttpResponseHeaders,
                response.AtErrorDetail,
                response.RateLimit);
        }
    }
}
