// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;
using idunno.AtProto.Repo;

namespace idunno.Bluesky;

public partial class BlueskyAgent
{
    /// <summary>
    /// Gets the post record for the specified <see cref="StrongReference"/>.
    /// </summary>
    /// <param name="uri">The <see cref="AtUri" /> of the post to return the <see cref="AtProtoRepositoryRecord{Post}"/> for.</param>
    /// <param name="cid">An optional <see cref="Cid" /> of the post to return the <see cref="AtProtoRepositoryRecord{Post}"/> for.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="uri"/> is <see langword="null"/>.</exception>
    public async Task<AtProtoHttpResult<AtProtoRepositoryRecord<Post>>> GetPostRecord(
        AtUri uri,
        Cid? cid = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(uri);

        AtProtoHttpResult<AtProtoRepositoryRecord<Post>> result = await BlueskyServer.GetPostRecord(
            uri,
            cid,
            service: AuthenticatedOrUnauthenticatedServiceUri,
            accessCredentials: Credentials,
            httpClient: HttpClient,
            onCredentialsUpdated: InternalOnCredentialsUpdatedCallBack,
            loggerFactory: LoggerFactory,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        if (result.Succeeded)
        {
            if (IsAuthenticated)
            {
                Logger.GetPostRecordSucceeded(_logger, Did, uri, cid, AuthenticatedOrUnauthenticatedServiceUri);
            }
            else
            {
                Logger.GetPostRecordSucceededAnon(_logger, uri, cid, AuthenticatedOrUnauthenticatedServiceUri);
            }
        }
        else
        {
            if (result.StatusCode == System.Net.HttpStatusCode.OK && result.Result is null)
            {
                Logger.GetPostRecordSucceededButReturnedNullResult(_logger, uri, cid, AuthenticatedOrUnauthenticatedServiceUri);
            }
            else
            {
                Logger.GetPostRecordFailed(
                    _logger,
                    result.StatusCode,
                    uri,
                    cid,
                    result.AtErrorDetail?.Error,
                    result.AtErrorDetail?.Message,
                    AuthenticatedOrUnauthenticatedServiceUri);
            }
        }

        return result;
    }

    /// <summary>
    /// Gets the post record for the specified <see cref="StrongReference"/>.
    /// </summary>
    /// <param name="strongReference">The <see cref="StrongReference" /> of the post to return the <see cref="AtProtoRepositoryRecord{Post}"/> for.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="strongReference"/> is <see langword="null"/>.</exception>
    public async Task<AtProtoHttpResult<AtProtoRepositoryRecord<Post>>> GetPostRecord(
        StrongReference strongReference,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(strongReference);

        return await GetPostRecord(strongReference.Uri, strongReference.Cid, cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}
