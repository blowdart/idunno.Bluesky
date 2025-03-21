﻿// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Net.Http.Headers;

using Microsoft.Extensions.Logging;

using idunno.AtProto;
using idunno.Bluesky.Video;
using idunno.Bluesky.Video.Model;
using idunno.AtProto.Authentication;

namespace idunno.Bluesky
{
    public partial class BlueskyServer
    {
        // https://docs.bsky.app/docs/api/app-bsky-video-get-job-status
        private const string GetJobStatusEndpoint = "/xrpc/app.bsky.video.getJobStatus";

        // https://docs.bsky.app/docs/api/app.bsky.video.getUploadLimits
        private const string GetUploadLimitsEndpoint = "/xrpc/app.bsky.video.getUploadLimits";

        // https://docs.bsky.app/docs/api/app-bsky-video-upload-video
        private const string UploadVideoEndpoint = "/xrpc/app.bsky.video.uploadVideo";

        /// <summary>
        /// Gets the status details for the specified video processing job.
        /// </summary>
        /// <param name="jobId">The job id whose status should be queried.</param>
        /// <param name="service">The <see cref="Uri"/> of the service to query the status against.</param>
        /// <param name="httpClient">An <see cref="HttpClient"/> to use when making a request to the <paramref name="service"/>.</param>
        /// <param name="loggerFactory">An instance of <see cref="ILoggerFactory"/> to use to create a logger.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="jobId"/> is null or whitespace.</exception>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="service"/> or <paramref name="httpClient"/> is null.</exception>
        public static async Task<AtProtoHttpResult<JobStatus>> GetVideoJobStatus(
            string jobId,
            Uri service,
            HttpClient httpClient,
            ILoggerFactory? loggerFactory = default,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(jobId);
            ArgumentNullException.ThrowIfNull(service);
            ArgumentNullException.ThrowIfNull(httpClient);

            AtProtoHttpClient<JobStatusResponse> client = new(loggerFactory);
            AtProtoHttpResult<JobStatusResponse> response = await client.Get(
                service, $"{GetJobStatusEndpoint}?jobId={Uri.EscapeDataString(jobId)}",
                httpClient: httpClient,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            // Flatten
            return new AtProtoHttpResult<JobStatus>(
                response.Result?.JobStatus,
                response.StatusCode,
                response.HttpResponseHeaders,
                response.AtErrorDetail,
                response.RateLimit);
        }

        /// <summary>
        /// Gets any video upload restrictions placed on the current user 
        /// </summary>
        /// <param name="service">The <see cref="Uri"/> of the service to upload video to.</param>
        /// <param name="serviceCredential">A service credential to authenticate against the <paramref name="service"/> with.</param>
        /// <param name="httpClient">An <see cref="HttpClient"/> to use when making a request to the <paramref name="service"/>.</param>
        /// <param name="loggerFactory">An instance of <see cref="ILoggerFactory"/> to use to create a logger.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="service"/>, <paramref name="serviceCredential"/> or <paramref name="httpClient"/> is null.</exception>
        public static async Task<AtProtoHttpResult<UploadLimits>> GetVideoUploadStatus(
            Uri service,
            ServiceCredential serviceCredential,
            HttpClient httpClient,
            ILoggerFactory? loggerFactory = default,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(service);
            ArgumentNullException.ThrowIfNull(serviceCredential);
            ArgumentNullException.ThrowIfNull(httpClient);

            AtProtoHttpClient<UploadLimits> client = new (loggerFactory);

            return await client.Get(
                service,
                GetUploadLimitsEndpoint,
                credentials: serviceCredential,
                httpClient: httpClient,
                onCredentialsUpdated: null,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Uploads a video to be processed and stored on the specified <paramref name="service"/>.
        /// </summary>
        /// <param name="did">The <see cref="AtProto.Did"/> of the account uploading the video.</param>
        /// <param name="fileName">The filename of the video.</param>
        /// <param name="video">The video to upload.</param>
        /// <param name="service">The <see cref="Uri"/> of the service to upload video to.</param>
        /// <param name="serviceCredential">AccessCredentials for service access used to authenticate against the <paramref name="service"/>.</param>
        /// <param name="httpClient">An <see cref="HttpClient"/> to use when making a request to the <paramref name="service"/>.</param>
        /// <param name="loggerFactory">An instance of <see cref="ILoggerFactory"/> to use to create a logger.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="fileName"/> is null or empty.</exception>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="serviceCredential"/>, <paramref name="video"/>, <paramref name="service"/> or <paramref name="httpClient"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="video"/> is empty.</exception>
        public static async Task<AtProtoHttpResult<JobStatus>> UploadVideo(
            Did did,
            string fileName,
            byte[] video,
            Uri service,
            ServiceCredential serviceCredential,
            HttpClient httpClient,
            ILoggerFactory? loggerFactory = default,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrEmpty(fileName);
            ArgumentNullException.ThrowIfNull(video);
            ArgumentNullException.ThrowIfNull(service);
            ArgumentNullException.ThrowIfNull(serviceCredential);
            ArgumentNullException.ThrowIfNull(httpClient);

            ArgumentOutOfRangeException.ThrowIfZero(video.Length);

            List<NameValueHeaderValue> contentHeaders =
            [
                new NameValueHeaderValue("Content-Type", "video/mp4")
            ];

            AtProtoHttpClient<JobStatus> client = new(loggerFactory);

            AtProtoHttpResult<JobStatus> response =
                await client.PostBlob(
                    service,
                    $"{UploadVideoEndpoint}?did={Uri.EscapeDataString(did)}&name={Uri.EscapeDataString(fileName)}",
                    video,
                    requestHeaders: null,
                    contentHeaders : contentHeaders,
                    credentials: serviceCredential,
                    httpClient: httpClient,
                    onCredentialsUpdated: null, // Service credentials don't get updates
                    cancellationToken: cancellationToken).ConfigureAwait(false);

            return response;
        }
    }
}
