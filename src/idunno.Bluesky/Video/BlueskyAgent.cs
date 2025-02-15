// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Net;
using System.Text.Json;

using idunno.AtProto;
using idunno.AtProto.Authentication;
using idunno.AtProto.Repo;
using idunno.Bluesky.Embed;
using idunno.Bluesky.Video;

namespace idunno.Bluesky
{
    public partial class BlueskyAgent
    {
        private readonly Uri _videoServer = new("https://video.bsky.app/");

        private const string GetUploadLimitsLxm = "app.bsky.video.getUploadLimits";

        private const string UploadBlobLxm = "com.atproto.repo.uploadBlob";

        /// <summary>
        /// Gets the status details for the specified video processing job.
        /// </summary>
        /// <param name="jobId">The job id whose status should be queried.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="jobId"/> is null or whitespace.</exception>
        public async Task<AtProtoHttpResult<JobStatus>> GetVideoJobStatus(
            string jobId,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(jobId);

            using (_logger.BeginScope($"Getting jobStatus for {jobId}"))
            {
                AtProtoHttpResult<JobStatus> result = await BlueskyServer.GetVideoJobStatus(
                    jobId,
                    _videoServer,
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

                    Logger.GetJobStatusFailed(_logger, result.StatusCode, error, message);
                }

                return result;
            }
        }

        /// <summary>
        /// Gets any video upload restrictions placed on the current user 
        /// </summary>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="AuthenticationRequiredException">Thrown when the agent is not authenticated.</exception>
        public async Task<AtProtoHttpResult<UploadLimits>> GetVideoUploadLimits(CancellationToken cancellationToken = default)
        {
            if (!IsAuthenticated)
            {
                throw new AuthenticationRequiredException();
            }

            using (_logger.BeginScope($"Getting video upload limits for {Did}"))
            {
                AtProtoHttpResult<ServiceCredential> getServiceAuthResult = await GetServiceAuth(
                    service: Service,
                    audience: WellKnownDistributedIdentifiers.Video,
                    lxm: GetUploadLimitsLxm,
                    cancellationToken: cancellationToken).ConfigureAwait(false);

                if (!getServiceAuthResult.Succeeded)
                {
                    return new AtProtoHttpResult<UploadLimits>(
                        null,
                        getServiceAuthResult.StatusCode,
                        getServiceAuthResult.HttpResponseHeaders,
                        getServiceAuthResult.AtErrorDetail,
                        getServiceAuthResult.RateLimit);
                }

                AtProtoHttpResult<UploadLimits> result = await BlueskyServer.GetVideoUploadStatus(
                    _videoServer,
                    serviceCredential: getServiceAuthResult.Result,
                    httpClient: HttpClient,
                    loggerFactory: LoggerFactory,
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
        /// <exception cref="ArgumentException">Thrown when <paramref name="fileName"/> is null or empty.</exception>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="video"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="video"/> is empty.</exception>
        /// <exception cref="AuthenticationRequiredException">Thrown when the agent is not authenticated.</exception>
        public async Task<AtProtoHttpResult<JobStatus>> UploadVideo(
            string fileName,
            byte[] video,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrEmpty(fileName);
            ArgumentNullException.ThrowIfNull(video);
            ArgumentOutOfRangeException.ThrowIfZero(video.Length);

            using (_logger.BeginScope($"Uploading video for {Did}"))
            {
                if (!IsAuthenticated)
                {
                    throw new AuthenticationRequiredException();
                }

                AtProtoHttpResult<AtProto.Server.ServerDescription> serverDescriptionResult = await DescribeServer(Service, cancellationToken).ConfigureAwait(false);

                if (serverDescriptionResult.Succeeded)
                {
                    AtProtoHttpResult<ServiceCredential> getServiceAuthResult = await GetServiceAuth(
                        Service,
                        audience: serverDescriptionResult.Result.Did,
                        lxm: UploadBlobLxm,
                        expiry: new TimeSpan(0, 30, 0),
                        cancellationToken: cancellationToken).ConfigureAwait(false);

                    if (!getServiceAuthResult.Succeeded)
                    {
                        return new AtProtoHttpResult<JobStatus>(
                            null,
                            getServiceAuthResult.StatusCode,
                            getServiceAuthResult.HttpResponseHeaders,
                            getServiceAuthResult.AtErrorDetail,
                            getServiceAuthResult.RateLimit);
                    }

                    Logger.UploadVideoStarted(_logger, Did, _videoServer, fileName, video.Length);

                    AtProtoHttpResult<JobStatus> result = await BlueskyServer.UploadVideo(
                        serverDescriptionResult.Result.Did,
                        fileName,
                        video,
                        service: _videoServer,
                        serviceCredential: getServiceAuthResult.Result,
                        httpClient: HttpClient,
                        loggerFactory: LoggerFactory,
                        cancellationToken: cancellationToken).ConfigureAwait(false);

                    if (result.Succeeded)
                    {
                        Logger.UploadVideoSucceeded(_logger, result.Result.JobId, Did);
                    }
                    else
                    {
                        if (result.StatusCode == HttpStatusCode.Conflict &&
                            result.AtErrorDetail is not null &&
                            string.Equals("already_exists", result.AtErrorDetail.Error, StringComparison.Ordinal) &&
                            result.AtErrorDetail.ExtensionData is not null &&
                            result.AtErrorDetail.ExtensionData.TryGetValue("jobId", out JsonElement jobIdElement))
                        {
                            string jobId = jobIdElement.GetString()!;

                            return await GetVideoJobStatus(jobId, cancellationToken).ConfigureAwait(false);
                        }

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
                else
                {
                    Logger.UploadVideoGetServerDescriptionFailed(
                        _logger,
                        Did,
                        Service,
                        serverDescriptionResult.StatusCode,
                        serverDescriptionResult.AtErrorDetail?.Error,
                        serverDescriptionResult.AtErrorDetail?.Message);

                    return new AtProtoHttpResult<JobStatus>(
                        null,
                        serverDescriptionResult.StatusCode,
                        serverDescriptionResult.HttpResponseHeaders,
                        serverDescriptionResult.AtErrorDetail,
                        serverDescriptionResult.RateLimit);
                }
            }
        }

        /// <summary>
        /// Uploads an caption file to be referenced in an embedded video.
        /// </summary>
        /// <param name="captionsAsBytes">The captions, as a byte array.</param>
        /// <param name="captionLanguage">The language the captions are in.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="captionsAsBytes"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="captionLanguage"/> is null or empty.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="captionsAsBytes"/> is a zero length array.</exception>
        /// <exception cref="AuthenticationRequiredException">Thrown when the current session is not an authenticated session.</exception>
        public async Task<AtProtoHttpResult<Caption>> UploadCaptions(
            byte[] captionsAsBytes,
            string captionLanguage,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(captionsAsBytes);
            ArgumentException.ThrowIfNullOrEmpty(captionLanguage);
            ArgumentOutOfRangeException.ThrowIfZero(captionsAsBytes.Length);

            if (!IsAuthenticated)
            {
                throw new AuthenticationRequiredException();
            }

            AtProtoHttpResult<Blob> uploadResult = await UploadBlob(
                captionsAsBytes,
                "text/vtt",
                cancellationToken: cancellationToken).ConfigureAwait(false);


            if (uploadResult.Succeeded)
            {
                return new AtProtoHttpResult<Caption>(
                    new Caption(captionLanguage, uploadResult.Result),
                    uploadResult.StatusCode,
                    uploadResult.HttpResponseHeaders,
                    uploadResult.AtErrorDetail,
                    uploadResult.RateLimit);
            }
            else
            {
                return new AtProtoHttpResult<Caption>(
                    null,
                    uploadResult.StatusCode,
                    uploadResult.HttpResponseHeaders,
                    uploadResult.AtErrorDetail,
                    uploadResult.RateLimit);
            }
        }
    }
}
