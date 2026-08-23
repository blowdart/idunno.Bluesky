// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;

using Microsoft.Extensions.Logging;

using idunno.AtProto;
using idunno.AtProto.Authentication;
using idunno.Bluesky.Video;
using idunno.Bluesky.Video.Model;

namespace idunno.Bluesky;

public static partial class BlueskyServer
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
    /// <param name="service">The URI of the service to which the video will be uploaded.</param>
    /// <param name="serviceCredential">The credentials used to authenticate with the service.</param>
    /// <param name="httpClient">An <see cref="HttpClient"/> to use when making a request to the <paramref name="service"/>.</param>
    /// <param name="loggerFactory">An instance of <see cref="ILoggerFactory"/> to use to create a logger.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when a provided argument is out of the allowable range.</exception>
    /// <exception cref="ArgumentException">Thrown when a provided argument is invalid.</exception>
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
        Justification = "All types are preserved in the JsonSerializerOptions call to Post().")]
    [UnconditionalSuppressMessage("AOT",
        "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
        Justification = "All types are preserved in the JsonSerializerOptions call to Post().")]
    public static async Task<AtProtoHttpResult<StartUploadResponse>> StartUpload(
        int size,
        string mimeType,
        string? name,
        long? duration,
        int? width,
        int? height,
        Uri service,
        ServiceCredential serviceCredential,
        HttpClient httpClient,
        ILoggerFactory? loggerFactory = default,
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

        ArgumentNullException.ThrowIfNull(service);
        ArgumentNullException.ThrowIfNull(serviceCredential);
        ArgumentNullException.ThrowIfNull(httpClient);

        // AppView proxy is not needed as we're hitting the video service directly.
        BlueskyHttpClient<StartUploadResponse> client = new(loggerFactory);

        AtProtoHttpResult<StartUploadResponse> response = await client.Post(
            service: service,
            endpoint: "/xrpc/app.bsky.video.startUpload",
            record: new StartUploadRequest(size, mimeType, name, duration, width, height),
            credentials: serviceCredential,
            httpClient: httpClient,
            jsonSerializerOptions: BlueskyJsonSerializerOptions,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return response;
    }
}
