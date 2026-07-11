// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Net;
using System.Text.Json;

using idunno.AtProto;
using idunno.AtProto.Authentication;
using idunno.AtProto.Server.Models;
using idunno.Bluesky.Video;

namespace idunno.Bluesky;

public partial class BlueskyAgent
{
    /// <summary>
    /// Uploads video to be processed and stored.
    /// </summary>
    /// <param name="fileName">The filename of the media.</param>
    /// <param name="media">The media to upload as bytes.</param>
    /// <param name="mimeType">The MIME type of the media.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="fileName"/> is <see langword="null"/> or empty.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="media"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="media"/> is empty.</exception>
    /// <exception cref="AuthenticationRequiredException">Thrown when the agent is not authenticated.</exception>
    internal async Task<AtProtoHttpResult<JobStatus>> UploadMedia(
        string fileName,
        byte[] media,
        string mimeType,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(fileName);
        ArgumentNullException.ThrowIfNull(media);
        ArgumentException.ThrowIfNullOrWhiteSpace(mimeType);
        ArgumentOutOfRangeException.ThrowIfZero(media.Length);

        using (_logger.BeginScope($"Uploading media for {Did}"))
        {
            if (!IsAuthenticated)
            {
                throw new AuthenticationRequiredException();
            }

            // Get the server description so we can get the DID of the server.
            AtProtoHttpResult<ServerDescription> serverDescriptionResult = await DescribeServer(Service, cancellationToken).ConfigureAwait(false);

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

                Logger.UploadMediaStarted(_logger, Did, _videoServer, fileName, media.Length, mimeType);

                AtProtoHttpResult<JobStatus> result = await BlueskyServer.UploadMedia(
                    serverDescriptionResult.Result.Did,
                    fileName,
                    mimeType,
                    media,
                    service: _videoServer,
                    serviceCredential: getServiceAuthResult.Result,
                    httpClient: HttpClient,
                    loggerFactory: LoggerFactory,
                    cancellationToken: cancellationToken).ConfigureAwait(false);

                if (result.Succeeded)
                {
                    Logger.UploadMediaSucceeded(_logger, result.Result.JobId, Did);
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

                    Logger.UploadMediaFailed(_logger, result.StatusCode, Did, error, message);
                }

                return result;
            }
            else
            {
                Logger.UploadMediaGetServerDescriptionFailed(
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
}