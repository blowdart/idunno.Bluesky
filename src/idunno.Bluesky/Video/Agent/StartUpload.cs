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
    /// Start a multipart video upload. The declared size is exact, while optional media properties are advisory and used only for early failure;
    /// the authoritative probe runs asynchronously after upload.
    /// </summary>
    /// <param name="size">Exact size, in bytes, of the complete upload-ready video file before it is split into parts.</param>
    /// <param name="mimeType">Declared MIME type of the video.</param>
    /// <param name="name">An optional client-provided file name.</param>
    /// <param name="duration">An optional duration of the video in milliseconds.</param>
    /// <param name="width">An optional width of the video in pixels.</param>
    /// <param name="height">An optional height of the video in pixels.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when a provided argument is out of the allowable range.</exception>
    /// <exception cref="ArgumentException">Thrown when a provided argument is invalid.</exception>
    /// <exception cref="AuthenticationRequiredException">Thrown when agent is not authenticated.</exception>
    public async Task<AtProtoHttpResult<StartUploadResponse>> StartUpload(
        long size,
        string mimeType,
        string? name = null,
        long? duration = null,
        int? width = null,
        int? height = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfZero(size);
        ArgumentOutOfRangeException.ThrowIfNegative(size);
        ArgumentException.ThrowIfNullOrWhiteSpace(mimeType);
        ArgumentOutOfRangeException.ThrowIfLessThan(mimeType.Length, 3);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(mimeType.Length, 255);

        if (!mimeType.Contains('/', StringComparison.InvariantCulture))
        {
            throw new ArgumentException("MIME type must contain a '/' character.", nameof(mimeType));
        }

        if (duration.HasValue)
        {
            ArgumentOutOfRangeException.ThrowIfZero(duration.Value);
            ArgumentOutOfRangeException.ThrowIfNegative(duration.Value);
        }

        if (width.HasValue)
        {
            ArgumentOutOfRangeException.ThrowIfZero(width.Value);
            ArgumentOutOfRangeException.ThrowIfNegative(width.Value);
        }

        if (height.HasValue)
        {
            ArgumentOutOfRangeException.ThrowIfZero(height.Value);
            ArgumentOutOfRangeException.ThrowIfNegative(height.Value);
        }

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
                return new AtProtoHttpResult<StartUploadResponse>(
                    null,
                    getServiceAuthResult.StatusCode,
                    getServiceAuthResult.HttpResponseHeaders,
                    getServiceAuthResult.AtErrorDetail,
                    getServiceAuthResult.RateLimit);
            }

            return await BlueskyServer.StartUpload(
                size,
                mimeType,
                name,
                duration,
                width,
                height,
                service: _videoServer,
                serviceCredential: getServiceAuthResult.Result,
                httpClient: HttpClient,
                loggerFactory: LoggerFactory,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        else
        {
            Logger.StartUploadGetServerDescriptionFailed(
                _logger,
                Did,
                Service,
                serverDescriptionResult.StatusCode,
                serverDescriptionResult.AtErrorDetail?.Error,
                serverDescriptionResult.AtErrorDetail?.Message);

            return new AtProtoHttpResult<StartUploadResponse>(
                null,
                serverDescriptionResult.StatusCode,
                serverDescriptionResult.HttpResponseHeaders,
                serverDescriptionResult.AtErrorDetail,
                serverDescriptionResult.RateLimit);
        }
    }
}
