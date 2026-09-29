// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;
using idunno.AtProto.Authentication;
using idunno.Bluesky.Video;

namespace idunno.Bluesky;

public partial class BlueskyAgent
{
    /// <summary>
    /// Gets the authoritative upload-phase status for the specified multipart video upload.
    /// </summary>
    /// <param name="jobId">The job id whose status should be queried.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="jobId"/> is <see langword="null"/> or whitespace.</exception>
    /// <exception cref="AuthenticationRequiredException">Thrown when the agent is not authenticated.</exception>
    public async Task<AtProtoHttpResult<UploadStatus>> GetUploadStatus(
        string jobId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(jobId);

        if (!IsAuthenticated)
        {
            throw new AuthenticationRequiredException();
        }

        using (_logger.BeginScope($"Getting uploadStatus for {jobId}"))
        {
            AtProtoHttpResult<ServiceCredential> getServiceAuthResult = await GetServiceAuth(
                Service,
                lxm: UploadBlobLxm,
                expiry: new TimeSpan(0, 30, 0),
                cancellationToken: cancellationToken).ConfigureAwait(false);

            if (!getServiceAuthResult.Succeeded)
            {
                Logger.GetUploadStatusServiceAuthFailed(_logger, Did, Service, getServiceAuthResult.StatusCode, getServiceAuthResult.AtErrorDetail?.Error, getServiceAuthResult.AtErrorDetail?.Message);

                return new AtProtoHttpResult<UploadStatus>(
                    null,
                    getServiceAuthResult.StatusCode,
                    getServiceAuthResult.HttpResponseHeaders,
                    getServiceAuthResult.AtErrorDetail,
                    getServiceAuthResult.RateLimit);
            }

            AtProtoHttpResult<UploadStatus> result = await BlueskyServer.GetUploadStatus(
                jobId,
                _videoServer,
                getServiceAuthResult.Result,
                HttpClient,
                LoggerFactory,
                maximumResponseSize: MaximumResponseSize,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            if (result.Succeeded)
            {
                Logger.GetUploadStatusSucceeded(_logger, jobId, result.Result.State, result.Result.ReceivedParts.Count);
            }
            else
            {
                string? error = null;
                string? message = null;
                if (result.AtErrorDetail is not null)
                {
                    error = result.AtErrorDetail.Error;
                    message = result.AtErrorDetail.Message;
                }

                Logger.GetUploadStatusFailed(_logger, result.StatusCode, error, message);
            }

            return result;
        }
    }
}
