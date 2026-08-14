// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;

using idunno.AtProto;
using idunno.AtProto.Authentication;
using idunno.Bluesky.Video;
using idunno.Bluesky.Video.Model;

using Microsoft.Extensions.Logging;

namespace idunno.Bluesky;

public static partial class BlueskyServer
{
    /// <summary>
    /// Abort an upload only while it is created, releasing its quota reservation immediately. Terminal sessions are unchanged and return their terminal outcome.
    /// A finishing session returns UploadNotReady.
    /// </summary>
    /// <param name="jobId">The job id of the upload to abort.</param>
    /// <param name="service">The <see cref="Uri"/> of the service to send the abort request to.</param>
    /// <param name="serviceCredential">The credentials to use for the service.</param>
    /// <param name="httpClient">An <see cref="HttpClient"/> to use when making a request to the <paramref name="service"/>.</param>
    /// <param name="loggerFactory">An instance of <see cref="ILoggerFactory"/> to use to create a logger.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="jobId"/> is <see langword="null"/> or whitespace.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="service"/>, <paramref name="serviceCredential"/>, or <paramref name="httpClient"/> are <see langword="null"/>.</exception>  
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
        Justification = "All types are preserved in the JsonSerializerOptions call to Post().")]
    [UnconditionalSuppressMessage("AOT",
        "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
        Justification = "All types are preserved in the JsonSerializerOptions call to Post().")]
    public static async Task<AtProtoHttpResult<AbortUploadResponse>> AbortUpload(
        string jobId,
        Uri service,
        ServiceCredential serviceCredential,
        HttpClient httpClient,
        ILoggerFactory? loggerFactory = default,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(jobId);
        ArgumentNullException.ThrowIfNull(service);
        ArgumentNullException.ThrowIfNull(serviceCredential);
        ArgumentNullException.ThrowIfNull(httpClient);

        // AppView proxy is not needed as we're hitting the video service directly.
        BlueskyHttpClient<AbortUploadWireResponse> client = new(loggerFactory);

        AtProtoHttpResult<AbortUploadWireResponse> response = await client.Post(
                service,
                "/xrpc/app.bsky.video.abortUpload",
                new AbortUploadRequest(jobId),
                credentials: serviceCredential,
                httpClient: httpClient,
                jsonSerializerOptions: BlueskyJsonSerializerOptions,
                onCredentialsUpdated: null, // Service credentials don't get updates
                cancellationToken: cancellationToken).ConfigureAwait(false);

        AbortUploadResponse? result = null;

        if (response.Succeeded) {
            result = new AbortUploadResponse(response.Result);
        }

        // Flatten
        return new AtProtoHttpResult<AbortUploadResponse>(
            result,
            response.StatusCode,
            response.HttpResponseHeaders,
            response.AtErrorDetail,
            response.RateLimit);
    }
}
