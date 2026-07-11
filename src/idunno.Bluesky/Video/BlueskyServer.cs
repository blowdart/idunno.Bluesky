// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;

using idunno.AtProto;
using idunno.AtProto.Authentication;
using idunno.Bluesky.Video;
using idunno.Bluesky.Video.Model;

using Microsoft.Extensions.Logging;

namespace idunno.Bluesky;

public partial class BlueskyServer
{
    // https://docs.bsky.app/docs/api/app-bsky-video-get-job-status
    private const string GetJobStatusEndpoint = "/xrpc/app.bsky.video.getJobStatus";

    // https://docs.bsky.app/docs/api/app.bsky.video.getUploadLimits
    private const string GetUploadLimitsEndpoint = "/xrpc/app.bsky.video.getUploadLimits";

    /// <summary>
    /// Gets the status details for the specified video processing job.
    /// </summary>
    /// <param name="jobId">The job id whose status should be queried.</param>
    /// <param name="service">The <see cref="Uri"/> of the service to query the status against.</param>
    /// <param name="httpClient">An <see cref="HttpClient"/> to use when making a request to the <paramref name="service"/>.</param>
    /// <param name="loggerFactory">An instance of <see cref="ILoggerFactory"/> to use to create a logger.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="jobId"/> is <see langword="null"/> or whitespace.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="service"/> or <paramref name="httpClient"/> are <see langword="null"/>.</exception>
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
        Justification = "All types are preserved in the JsonSerializerOptions call to Get().")]
    [UnconditionalSuppressMessage("AOT",
        "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
        Justification = "All types are preserved in the JsonSerializerOptions call to Get().")]
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

        BlueskyHttpClient<JobStatusResponse> client = new(AppViewProxy, loggerFactory);
        AtProtoHttpResult<JobStatusResponse> response = await client.Get(
            service, $"{GetJobStatusEndpoint}?jobId={Uri.EscapeDataString(jobId)}",
            httpClient: httpClient,
            jsonSerializerOptions: BlueskyJsonSerializerOptions,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        JobStatus? result = null;

        if (response.Result is not null && response.Result.JobStatus is not null)
        {
            result = new JobStatus(response.Result.JobStatus);
        }

        // Flatten
        return new AtProtoHttpResult<JobStatus>(
            result,
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
    /// <exception cref="ArgumentNullException">Thrown when any of <paramref name="service"/>, <paramref name="serviceCredential"/> or <paramref name="httpClient"/> are <see langword="null"/>.</exception>
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
        Justification = "All types are preserved in the JsonSerializerOptions call to Get().")]
    [UnconditionalSuppressMessage("AOT",
        "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
        Justification = "All types are preserved in the JsonSerializerOptions call to Get().")]
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

        BlueskyHttpClient<UploadLimits> client = new(AppViewProxy, loggerFactory);

        return await client.Get(
            service,
            GetUploadLimitsEndpoint,
            credentials: serviceCredential,
            httpClient: httpClient,
            jsonSerializerOptions: BlueskyJsonSerializerOptions,
            onCredentialsUpdated: null,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}