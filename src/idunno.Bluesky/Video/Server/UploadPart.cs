// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Net.Http.Headers;

using idunno.AtProto;
using idunno.AtProto.Authentication;
using idunno.Bluesky.Video;

using Microsoft.Extensions.Logging;

namespace idunno.Bluesky;

public static partial class BlueskyServer
{
    /// <summary>
    /// Upload one part of a multi-part upload started with <see cref="StartUpload"/>. Parts are idempotent and may be retried or re-sent while the session is created.
    /// Each expected length is derived from the upload size and part size, and Content-Length must match exactly.
    /// ETags are never exposed to clients.
    /// </summary>
    /// <param name="jobId">The job id of the upload to which this part belongs.</param>
    /// <param name="part">The part number of the part being uploaded.</param>
    /// <param name="bytes">The bytes of the part being uploaded.</param>
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
    [SuppressMessage("ApiDesign", "RS0026:Do not add multiple public overloads with optional parameters", Justification = "Only difference between methods is the type of the bytes parameter")]
    public static async Task<AtProtoHttpResult<UploadPartResponse>> UploadPart(
        string jobId,
        long part,
        byte[] bytes,
        Uri service,
        ServiceCredential serviceCredential,
        HttpClient httpClient,
        ILoggerFactory? loggerFactory = default,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(jobId);
        ArgumentOutOfRangeException.ThrowIfLessThan(part, 1);
        ArgumentNullException.ThrowIfNull(bytes);
        ArgumentOutOfRangeException.ThrowIfZero(bytes.Length);

        ArgumentNullException.ThrowIfNull(service);
        ArgumentNullException.ThrowIfNull(serviceCredential);
        ArgumentNullException.ThrowIfNull(httpClient);

        List<NameValueHeaderValue> contentHeaders =
        [
            new NameValueHeaderValue("Content-Type", "application/octet-stream"),
            new NameValueHeaderValue("Content-Length", bytes.Length.ToString(CultureInfo.InvariantCulture))
        ];

        BlueskyHttpClient<UploadPartResponse> client = new(AppViewProxy, loggerFactory);
        return await client.PostBlob(
                service: service,
                endpoint: $"/xrpc/app.bsky.video.uploadPart?jobId={Uri.EscapeDataString(jobId)}&part={part}",
                blob: bytes,
                requestHeaders: null,
                contentHeaders: contentHeaders,
                credentials: serviceCredential,
                httpClient: httpClient,
                jsonSerializerOptions: BlueskyJsonSerializerOptions,
                onCredentialsUpdated: null, // Service credentials don't get updates
                cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Upload one part of a multi-part upload started with <see cref="StartUpload"/>. Parts are idempotent and may be retried or re-sent while the session is created.
    /// Each expected length is derived from the upload size and part size, and Content-Length must match exactly.
    /// ETags are never exposed to clients.
    /// </summary>
    /// <param name="jobId">The job id of the upload to which this part belongs.</param>
    /// <param name="part">The part number of the part being uploaded.</param>
    /// <param name="bytes">The bytes of the part being uploaded.</param>
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
    [SuppressMessage("ApiDesign", "RS0026:Do not add multiple public overloads with optional parameters", Justification = "Only difference between methods is the type of the bytes parameter")]
    public static async Task<AtProtoHttpResult<UploadPartResponse>> UploadPart(
        string jobId,
        long part,
        ReadOnlyMemory<byte> bytes,
        Uri service,
        ServiceCredential serviceCredential,
        HttpClient httpClient,
        ILoggerFactory? loggerFactory = default,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(jobId);
        ArgumentOutOfRangeException.ThrowIfLessThan(part, 1);
        ArgumentOutOfRangeException.ThrowIfZero(bytes.Length);

        ArgumentNullException.ThrowIfNull(service);
        ArgumentNullException.ThrowIfNull(serviceCredential);
        ArgumentNullException.ThrowIfNull(httpClient);

        List<NameValueHeaderValue> contentHeaders =
        [
            new NameValueHeaderValue("Content-Type", "application/octet-stream"),
            new NameValueHeaderValue("Content-Length", bytes.Length.ToString(CultureInfo.InvariantCulture))
        ];

        BlueskyHttpClient<UploadPartResponse> client = new(AppViewProxy, loggerFactory);
        return await client.PostBlob(
                service: service,
                endpoint: $"/xrpc/app.bsky.video.uploadPart?jobId={Uri.EscapeDataString(jobId)}&part={part}",
                blob: bytes,
                requestHeaders: null,
                contentHeaders: contentHeaders,
                credentials: serviceCredential,
                httpClient: httpClient,
                jsonSerializerOptions: BlueskyJsonSerializerOptions,
                onCredentialsUpdated: null, // Service credentials don't get updates
                cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}
