// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;

using idunno.AtProto;
using idunno.Bluesky.Unspecced;

namespace idunno.Bluesky;

public partial class BlueskyAgent
{
    /// <summary>
    /// Get posts in a thread. It is based in an anchor post at any depth of the tree, and returns posts above it (recursively resolving the parent, without further branching to their replies)
    /// and below it.
    /// </summary>
    /// <param name="anchor">Reference <see cref="AtUri"/> to post record. This is the anchor post, and the thread will be built around it. It can be any post in the tree, not necessarily a root post.</param>
    /// <param name="above">Flag indicating whether to include parents above the anchor.</param>
    /// <param name="below">How many levels to include below the anchor. If specified must be between 1 and 20.</param>
    /// <param name="branchingFactor">
    ///   Maximum of replies to include at each level of the thread,
    ///   except for the direct replies to the anchor, which are (NOTE: currently, during unspecced phase) all returned (NOTE: later they might be paginated).
    ///   If specified must be between 1 and 100.</param>
    /// <param name="sort">The sort order for the thread. Known values are "newest", "oldest" and "top".</param>
    /// <param name="subscribedLabelers">An optional list of labeler <see cref="Did"/>s to accept labels from.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="anchor"/> is <see langword="null" />.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="below"/> or <paramref name="branchingFactor"/> are out of range.</exception>
    /// <exception cref="AuthenticationRequiredException">Thrown when the agent is not authenticated.</exception>
    [Experimental("BSKYUnspecced", UrlFormat = "https://bluesky.idunno.dev/docs/unspecced.html")]
    public async Task<AtProtoHttpResult<PostThreadV2>> GetPostThreadV2(
        AtUri anchor,
        bool? above = true,
        int? below = 6,
        int? branchingFactor = 10,
        string? sort = "oldest",
        IEnumerable<Did>? subscribedLabelers = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(anchor);

        if (below.HasValue)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(below.Value);
            ArgumentOutOfRangeException.ThrowIfZero(below.Value);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(below.Value, 20);
        }

        if (branchingFactor.HasValue)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(branchingFactor.Value);
            ArgumentOutOfRangeException.ThrowIfZero(branchingFactor.Value);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(branchingFactor.Value, 100);
        }

        if (!IsAuthenticated)
        {
            throw new AuthenticationRequiredException();
        }

#pragma warning disable BSKYUnspecced
        return await BlueskyServer.GetPostThreadV2(
            anchor,
            above,
            below,
            branchingFactor,
            sort,
            service: Service,
            accessCredentials: Credentials,
            httpClient: HttpClient,
            onCredentialsUpdated: InternalOnCredentialsUpdatedCallBack,
            loggerFactory: LoggerFactory,
            subscribedLabelers: subscribedLabelers,
            cancellationToken: cancellationToken).ConfigureAwait(false);
#pragma warning restore BSKYUnspecced
    }
}
