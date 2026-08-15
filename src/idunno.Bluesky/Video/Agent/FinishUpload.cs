// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;
using idunno.AtProto.Authentication;
using idunno.Bluesky.Video;

namespace idunno.Bluesky;

public partial class BlueskyAgent
{
    /// <summary>
    /// Finish an upload. This call is idempotent and safe to retry.
    /// On deduplication the returned completedJobId may differ from the <paramref name="jobId"/>;
    /// Poll <see cref="GetJobStatus(string, CancellationToken)"/> with <see cref="FinishUploadResponse.CompletedJobId"/>.
    /// Probe-based validation failures surface later as <see cref="JobState.Failed"/> from <see cref="GetJobStatus(string, CancellationToken)"/>,
    /// not as errors from this call.
    /// </summary>
    /// <param name="jobId">The job id of the upload to finish.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="jobId"/> is <see langword="null"/> or whitespace.</exception>
    /// <exception cref="AuthenticationRequiredException">Thrown when agent is not authenticated.</exception>
    public async Task<AtProtoHttpResult<FinishUploadResponse>> FinishUpload(
        string jobId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(jobId);

        if (!IsAuthenticated)
        {
            throw new AuthenticationRequiredException();
        }

        using (_logger.BeginScope($"Finishing upload for {jobId}"))
        {
            AtProtoHttpResult<ServiceCredential> getServiceAuthResult = await GetServiceAuth(
                Service,
                lxm: UploadBlobLxm,
                expiry: new TimeSpan(0, 30, 0),
                cancellationToken: cancellationToken).ConfigureAwait(false);

            if (!getServiceAuthResult.Succeeded)
            {
                Logger.FinishUploadServiceAuthFailed(_logger, Did, Service, getServiceAuthResult.StatusCode, getServiceAuthResult.AtErrorDetail?.Error, getServiceAuthResult.AtErrorDetail?.Message);
                return new AtProtoHttpResult<FinishUploadResponse>(
                    null,
                    getServiceAuthResult.StatusCode,
                    getServiceAuthResult.HttpResponseHeaders,
                    getServiceAuthResult.AtErrorDetail,
                    getServiceAuthResult.RateLimit);
            }

            AtProtoHttpResult<FinishUploadResponse> result = await BlueskyServer.FinishUpload(
                jobId,
                service: _videoServer,
                serviceCredential: getServiceAuthResult.Result,
                httpClient: HttpClient,
                loggerFactory: LoggerFactory,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            if (!result.Succeeded)
            {
                Logger.FinishUploadFailed(_logger, jobId, result.StatusCode, result.AtErrorDetail?.Error, result.AtErrorDetail?.Message);
            }

            return result;
        }
    }
}
