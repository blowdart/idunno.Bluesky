// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;
using idunno.Bluesky.Chat.Group.Model;

namespace idunno.Bluesky;

public partial class BlueskyAgent
{
    /// <summary>
    /// Get public information about groups from join links. The output array matches the input codes one-to-one by position (and each view also carries its 'code'). Disabled codes return a disabledJoinLinkPreviewView, and codes that do not map to a previewable link return an invalidJoinLinkPreviewView.
    /// </summary>
    /// <param name="codes">The codes of the group conversations to get join link previews for.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="codes"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="codes"/> is empty.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="codes"/> contains more than 50 items.</exception>
    public async Task<AtProtoHttpResult<GetJoinLinkPreviewsResponse>> GetJoinLinkPreviews(
        ICollection<string> codes,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(codes);
        ArgumentOutOfRangeException.ThrowIfZero(codes.Count);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(codes.Count, 50);

        return await BlueskyServer.GetJoinLinkPreviews(
            codes,
            service: Service,
            accessCredentials: Credentials,
            httpClient: HttpClient,
            onCredentialsUpdated: InternalOnCredentialsUpdatedCallBack,
            loggerFactory: LoggerFactory,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}