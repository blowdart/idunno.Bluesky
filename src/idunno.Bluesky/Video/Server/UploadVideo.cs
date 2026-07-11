// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;

using idunno.AtProto;
using idunno.AtProto.Authentication;
using idunno.Bluesky.Video;

using Microsoft.Extensions.Logging;

namespace idunno.Bluesky;

public partial class BlueskyServer
{
    /// <summary>
    /// Uploads a video to be processed and stored on the specified <paramref name="service"/>.
    /// </summary>
    /// <param name="did">The <see cref="AtProto.Did"/> of the account uploading the video.</param>
    /// <param name="fileName">The filename of the video.</param>
    /// <param name="video">The video to upload.</param>
    /// <param name="mimeType">The MIME type of the video.</param>
    /// <param name="service">The <see cref="Uri"/> of the service to upload video to.</param>
    /// <param name="serviceCredential">AccessCredentials for service access used to authenticate against the <paramref name="service"/>.</param>
    /// <param name="httpClient">An <see cref="HttpClient"/> to use when making a request to the <paramref name="service"/>.</param>
    /// <param name="loggerFactory">An instance of <see cref="ILoggerFactory"/> to use to create a logger.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="fileName"/> is <see langword="null"/> or empty, or <paramref name="mimeType"/> is <see langword="null"/> or whitespace.</exception>
    /// <exception cref="ArgumentNullException">Thrown when any of <paramref name="did"/>, <paramref name="serviceCredential"/>, <paramref name="video"/>, <paramref name="service"/> or <paramref name="httpClient"/> are <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="video"/> is empty.</exception>
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
        Justification = "All types are preserved in the JsonSerializerOptions call to Post().")]
    [UnconditionalSuppressMessage("AOT",
        "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
        Justification = "All types are preserved in the JsonSerializerOptions call to Post().")]
    public static async Task<AtProtoHttpResult<JobStatus>> UploadVideo(
        Did did,
        string fileName,
        byte[] video,
        string mimeType,
        Uri service,
        ServiceCredential serviceCredential,
        HttpClient httpClient,
        ILoggerFactory? loggerFactory = default,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(did);
        ArgumentException.ThrowIfNullOrEmpty(fileName);
        ArgumentNullException.ThrowIfNull(video);
        ArgumentOutOfRangeException.ThrowIfZero(video.Length);
        ArgumentException.ThrowIfNullOrWhiteSpace(mimeType);
        ArgumentNullException.ThrowIfNull(service);
        ArgumentNullException.ThrowIfNull(serviceCredential);
        ArgumentNullException.ThrowIfNull(httpClient);

        if (!mimeType.StartsWith("video/", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("The MIME type must start with 'video/'.", nameof(mimeType));
        }

        return await UploadMedia(
            did,
            fileName,
            mimeType,
            video,
            service,
            serviceCredential,
            httpClient,
            loggerFactory,
            cancellationToken).ConfigureAwait(false);
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
    /// <exception cref="ArgumentException">Thrown when <paramref name="fileName"/> is <see langword="null"/> or empty.</exception>
    /// <exception cref="ArgumentNullException">Thrown when any of <paramref name="serviceCredential"/>, <paramref name="video"/>, <paramref name="service"/> or <paramref name="httpClient"/> are <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="video"/> is empty.</exception>
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
        Justification = "All types are preserved in the JsonSerializerOptions call to Post().")]
    [UnconditionalSuppressMessage("AOT",
        "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
        Justification = "All types are preserved in the JsonSerializerOptions call to Post().")]
    [Obsolete("This method is obsolete. Use UploadVideo(Did did, string fileName, byte[] video, string mimeType, Uri service, ServiceCredential serviceCredential, HttpClient httpClient, ILoggerFactory? loggerFactory = default, CancellationToken cancellationToken = default) instead.")]
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

        return await UploadVideo(
            did: did,
            fileName: fileName,
            video: video,
            mimeType: "video/mp4",
            service: service,
            serviceCredential: serviceCredential,
            httpClient: httpClient,
            loggerFactory: loggerFactory,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}