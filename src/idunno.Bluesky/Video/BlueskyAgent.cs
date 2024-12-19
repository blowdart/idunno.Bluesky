// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;
using idunno.Bluesky.Video;
using Microsoft.Extensions.Logging;

namespace idunno.Bluesky
{
    public partial class BlueskyAgent
    {
        private readonly Uri _videoServer = new("https://video.bsky.app/");

        /// <summary>
        /// Gets the status details for the specified video processing job.
        /// </summary>
        /// <param name="jobId">The job id whose status should be queried.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="jobId"/> is null or whitespace.</exception>
        public async Task<AtProtoHttpResult<JobStatus>> GetVideoJobStatus(
            string jobId,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNullOrWhiteSpace(jobId);

            if (!IsAuthenticated)
            {
                throw new AuthenticatedSessionRequiredException();
            }

            using (_logger.BeginScope($"Getting jobStatus for {jobId}, user is {Did}"))
            {
                AtProtoHttpResult<string> videoAccessToken = await GetVideoAuth(
                    Did,
                    Service,
                    AccessToken,
                    HttpClient,
                    LoggerFactory,
                    cancellationToken: cancellationToken).ConfigureAwait(false);

                if (!videoAccessToken.Succeeded)
                {
                    return new AtProtoHttpResult<JobStatus>(null, videoAccessToken.StatusCode, videoAccessToken.AtErrorDetail, videoAccessToken.RateLimit);
                }

                AtProtoHttpResult<JobStatus> result = await BlueskyServer.GetVideoJobStatus(
                    jobId,
                    _videoServer,
                    videoAccessToken.Result,
                    HttpClient,
                    LoggerFactory,
                    cancellationToken: cancellationToken).ConfigureAwait(false);

                if (result.Succeeded)
                {
                    Logger.GetJobStatusSucceeded(_logger, jobId, result.Result.State, result.Result.Progress);
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

                    Logger.GetJobStatusFailed(_logger, result.StatusCode, Did, error, message);
                }

                return result;
            }
        }

        /// <summary>
        /// Gets any video upload restrictions placed on the current user 
        /// </summary>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="AuthenticatedSessionRequiredException">Thrown when the agent is not authenticated.</exception>
        public async Task<AtProtoHttpResult<UploadLimits>> GetVideoUploadLimits(CancellationToken cancellationToken = default)
        {
            if (!IsAuthenticated)
            {
                throw new AuthenticatedSessionRequiredException();
            }

            using (_logger.BeginScope($"Getting video restrictions for {Did}"))
            {
                AtProtoHttpResult<string> videoAccessToken = await GetVideoAuth(
                    Did,
                    Service,
                    AccessToken,
                    HttpClient,
                    LoggerFactory,
                    cancellationToken: cancellationToken).ConfigureAwait(false);

                if (!videoAccessToken.Succeeded)
                {
                    return new AtProtoHttpResult<UploadLimits>(null, videoAccessToken.StatusCode, videoAccessToken.AtErrorDetail, videoAccessToken.RateLimit);
                }

                AtProtoHttpResult<UploadLimits> result = await BlueskyServer.GetVideoUploadStatus(
                    _videoServer,
                    videoAccessToken.Result,
                    HttpClient,
                    LoggerFactory,
                    cancellationToken: cancellationToken).ConfigureAwait(false);

                if (result.Succeeded)
                {
                    Logger.GetUploadLimitsSucceeded(_logger, Did, result.Result.CanUpload, result.Result.RemainingDailyVideos, result.Result.RemainingDailyBytes);
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

                    Logger.GetUploadLimitsFailed(_logger, result.StatusCode, Did, error, message);
                }

                return result;
            }
        }

        /// <summary>
        /// Uploads a video to be processed and stored.
        /// </summary>
        /// <param name="fileName">The filename of the video.</param>
        /// <param name="video">The video to upload as bytes.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="fileName"/> is null or empty or <paramref name="video"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="video"/> is empty.</exception>
        /// <exception cref="AuthenticatedSessionRequiredException">Thrown when the agent is not authenticated.</exception>
        public async Task<AtProtoHttpResult<JobStatus>> UploadVideo(
            string fileName,
            byte[] video,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNullOrEmpty(fileName);
            ArgumentNullException.ThrowIfNull(video);

            ArgumentOutOfRangeException.ThrowIfZero(video.Length);

            if (!IsAuthenticated)
            {
                throw new AuthenticatedSessionRequiredException();
            }

            using (_logger.BeginScope($"Uploading video for {Did}"))
            {
                AtProtoHttpResult<string> videoAccessToken = await GetVideoAuth(
                    Did,
                    Service,
                    AccessToken,
                    HttpClient,
                    LoggerFactory,
                    cancellationToken: cancellationToken).ConfigureAwait(false);

                if (!videoAccessToken.Succeeded)
                {
                    return new AtProtoHttpResult<JobStatus>(null, videoAccessToken.StatusCode, videoAccessToken.AtErrorDetail, videoAccessToken.RateLimit);
                }

                Logger.UploadVideoStarted(_logger, Did, _videoServer, fileName, video.Length);

                AtProtoHttpResult<JobStatus> result = await BlueskyServer.UploadVideo(
                    Did,
                    fileName,
                    video,
                    _videoServer,
                    videoAccessToken.Result,
                    HttpClient,
                    LoggerFactory,
                    cancellationToken: cancellationToken).ConfigureAwait(false);

                if (result.Succeeded)
                {
                    Logger.UploadVideoSucceeded(_logger, result.Result.JobId, Did);
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

                    Logger.UploadVideoFailed(_logger, result.StatusCode, Did, error, message);
                }

                return result;
            }
        }

        private async Task<AtProtoHttpResult<string>> GetVideoAuth(
            Did did,
            Uri service,
            string accessToken,
            HttpClient httpClient,
            ILoggerFactory loggerFactory,
            CancellationToken cancellationToken = default)
        {
            AtProtoHttpResult<string> getServiceAuthResult = await AtProtoServer.GetServiceAuth(
                WellKnownDistributedIdentifiers.Video,
                new TimeSpan(0, 0, 60),
                "app.bsky.video.getUploadLimits",
                service,
                accessToken,
                httpClient,
                loggerFactory,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            if (!getServiceAuthResult.Succeeded)
            {
                string? error = null;
                string? message = null;
                if (getServiceAuthResult.AtErrorDetail is not null)
                {
                    error = getServiceAuthResult.AtErrorDetail.Error;
                    message = getServiceAuthResult.AtErrorDetail.Message;
                }

                Logger.UploadVideoServiceTokenAcquisitionFailed(_logger, getServiceAuthResult.StatusCode, did, error, message);
            }

            return getServiceAuthResult;
        }
    }
}
