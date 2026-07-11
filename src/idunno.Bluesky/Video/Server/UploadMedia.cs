// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Net.Http.Headers;

using idunno.AtProto;
using idunno.AtProto.Authentication;
using idunno.Bluesky.Video;
using idunno.Bluesky.Video.Model;

using Microsoft.Extensions.Logging;

namespace idunno.Bluesky;

public partial class BlueskyServer
{
    /// <summary>
    /// Uploads media to be processed and stored on the specified <paramref name="service"/>.
    /// </summary>
    /// <param name="did">The <see cref="Did"/> of the account uploading the media.</param>
    /// <param name="fileName">The filename of the media.</param>
    /// <param name="mimeType">The MIME type of the media.</param>
    /// <param name="media">The media to upload.</param>
    /// <param name="service">The <see cref="Uri"/> of the service to upload media to.</param>
    /// <param name="serviceCredential">AccessCredentials for service access used to authenticate against the <paramref name="service"/>.</param>
    /// <param name="httpClient">An <see cref="HttpClient"/> to use when making a request to the <paramref name="service"/>.</param>
    /// <param name="loggerFactory">An instance of <see cref="ILoggerFactory"/> to use to create a logger.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="fileName"/> is <see langword="null"/> or empty, or when <paramref name="mimeType"/> is <see langword="null"/> or whitespace.</exception>
    /// <exception cref="ArgumentNullException">Thrown when any of <paramref name="serviceCredential"/>, <paramref name="media"/>, <paramref name="service"/> or <paramref name="httpClient"/> are <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="media"/> is empty.</exception>
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
        Justification = "All types are preserved in the JsonSerializerOptions call to Post().")]
    [UnconditionalSuppressMessage("AOT",
        "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
        Justification = "All types are preserved in the JsonSerializerOptions call to Post().")]
    internal static async Task<AtProtoHttpResult<JobStatus>> UploadMedia(
        Did did,
        string fileName,
        string mimeType,
        byte[] media,
        Uri service,
        ServiceCredential serviceCredential,
        HttpClient httpClient,
        ILoggerFactory? loggerFactory = default,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(fileName);
        ArgumentNullException.ThrowIfNull(media);
        ArgumentException.ThrowIfNullOrWhiteSpace(mimeType);
        ArgumentNullException.ThrowIfNull(service);
        ArgumentNullException.ThrowIfNull(serviceCredential);
        ArgumentNullException.ThrowIfNull(httpClient);

        ArgumentOutOfRangeException.ThrowIfZero(media.Length);

        List<NameValueHeaderValue> contentHeaders =
        [
            new NameValueHeaderValue("Content-Type", mimeType)
        ];

        BlueskyHttpClient<JobStatusWireFormat> client = new(AppViewProxy, loggerFactory);

        AtProtoHttpResult<JobStatusWireFormat> response =
            await client.PostBlob(
                service,
                $"/xrpc/app.bsky.video.uploadVideo?did={Uri.EscapeDataString(did)}&name={Uri.EscapeDataString(fileName)}",
                media,
                requestHeaders: null,
                contentHeaders: contentHeaders,
                credentials: serviceCredential,
                httpClient: httpClient,
                jsonSerializerOptions: BlueskyJsonSerializerOptions,
                onCredentialsUpdated: null, // Service credentials don't get updates
                cancellationToken: cancellationToken).ConfigureAwait(false);

        JobStatus? result = null;

        if (response.Result is not null)
        {
            result = new JobStatus(response.Result);
        }

        return new AtProtoHttpResult<JobStatus>(
            result,
            response.StatusCode,
            response.HttpResponseHeaders,
            response.AtErrorDetail,
            response.RateLimit);
    }
}