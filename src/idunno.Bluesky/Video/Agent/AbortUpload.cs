// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;
using idunno.AtProto.Authentication;
using idunno.AtProto.Server;
using idunno.Bluesky.Video;

namespace idunno.Bluesky;

public partial class BlueskyAgent
{
    /// <summary>
    /// Abort an upload only while it is created, releasing its quota reservation immediately. Terminal sessions are unchanged and return their terminal outcome.
    /// A finishing session returns UploadNotReady.
    /// </summary>
    /// <param name="jobId">The job id of the upload to abort.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="jobId"/> is <see langword="null"/> or whitespace.</exception>
    /// <exception cref="AuthenticationRequiredException">Thrown when agent is not authenticated.</exception>
    public async Task<AtProtoHttpResult<AbortUploadResponse>> AbortUpload(
        string jobId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(jobId);

        if (!IsAuthenticated)
        {
            throw new AuthenticationRequiredException();
        }

        using (_logger.BeginScope($"Aborting upload for {jobId}"))
        {
            AtProtoHttpResult<ServiceCredential> getServiceAuthResult = await GetServiceAuth(
                Service,
                lxm: UploadBlobLxm,
                expiry: new TimeSpan(0, 30, 0),
                cancellationToken: cancellationToken).ConfigureAwait(false);

            if (!getServiceAuthResult.Succeeded)
            {
                Logger.AbortUploadServiceAuthFailed(_logger, Did, Service, getServiceAuthResult.StatusCode, getServiceAuthResult.AtErrorDetail?.Error, getServiceAuthResult.AtErrorDetail?.Message);
                return new AtProtoHttpResult<AbortUploadResponse>(
                    null,
                    getServiceAuthResult.StatusCode,
                    getServiceAuthResult.HttpResponseHeaders,
                    getServiceAuthResult.AtErrorDetail,
                    getServiceAuthResult.RateLimit);
            }

            AtProtoHttpResult<AbortUploadResponse> result = await BlueskyServer.AbortUpload(
                jobId,
                service: _videoServer,
                serviceCredential: getServiceAuthResult.Result,
                httpClient: HttpClient,
                loggerFactory: LoggerFactory,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            if (!result.Succeeded)
            {
                Logger.AbortUploadFailed(_logger, jobId, result.StatusCode, result.AtErrorDetail?.Error, result.AtErrorDetail?.Message);
            }

            return result;
        }
    }
}
