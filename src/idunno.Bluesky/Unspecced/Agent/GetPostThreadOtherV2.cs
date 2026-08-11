// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;

using idunno.AtProto;
using idunno.Bluesky.Unspecced;

namespace idunno.Bluesky;

public partial class BlueskyAgent
{
    /// <summary>
    ///  Get additional posts under a thread e.g. replies hidden by threadgate.
    ///  Based on an anchor post at any depth of the tree, returns top-level replies below that anchor.
    ///  It does not include ancestors nor the anchor itself.
    ///  This should be called after exhausting <see cref="GetPostThreadV2(AtUri, bool?, int?, int?, string?, IEnumerable{Did}?, CancellationToken)"/>.
    ///  Does not require authentication, but additional metadata and filtering will be applied for authed requests.
    /// </summary>
    /// <param name="anchor">Reference <see cref="AtUri"/> to post record. This is the anchor post, and the thread will be built around it.
    /// It can be any post in the tree, not necessarily a root post.</param>
    /// <param name="subscribedLabelers">An optional list of labeler <see cref="Did"/>s to accept labels from.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="anchor"/> is <see langword="null" />.</exception>
    [Experimental("BSKYUnspecced", UrlFormat = "https://bluesky.idunno.dev/docs/unspecced.html")]
    public async Task<AtProtoHttpResult<IReadOnlyCollection<ThreadItem>>> GetPostThreadOtherV2(
        AtUri anchor,
        IEnumerable<Did>? subscribedLabelers = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(anchor);

#pragma warning disable BSKYUnspecced
        return await BlueskyServer.GetPostThreadOtherV2(
            anchor,
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
