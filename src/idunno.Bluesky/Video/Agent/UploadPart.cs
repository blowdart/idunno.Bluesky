// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;
using idunno.AtProto.Authentication;
using idunno.Bluesky.Video;

namespace idunno.Bluesky;

public partial class BlueskyAgent
{
    /// <summary>
    /// Upload one part of a multi-part upload started with <see cref="StartUpload"/>. Parts are idempotent and may be retried or re-sent while the session is created.
    /// Each expected length is derived from the upload size and part size, and Content-Length must match exactly.
    /// ETags are never exposed to clients.
    /// </summary>
    /// <param name="jobId">The job id of the upload to which this part belongs.</param>
    /// <param name="part">The part number of the part being uploaded.</param>
    /// <param name="bytes">The bytes of the part being uploaded.</param>
    /// <param name="timeout">An optional timeout for the request.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when a provided argument is out of the allowable range.</exception>
    /// <exception cref="ArgumentException">Thrown when a provided argument is invalid.</exception>
    /// <exception cref="AuthenticationRequiredException">Thrown when the agent is not authenticated.</exception>
    /// <exception cref="HttpRequestException">Thrown when the request fails due to an underlying issue such as network connectivity, DNS failure, server certificate validation or timeout.</exception>"
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Catch all exception logging")]
    public async Task<AtProtoHttpResult<UploadPartResponse>> UploadPart(
        string jobId,
        long part,
        byte[] bytes,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(jobId);
        ArgumentOutOfRangeException.ThrowIfLessThan(part, 1);
        ArgumentNullException.ThrowIfNull(bytes);
        ArgumentOutOfRangeException.ThrowIfZero(bytes.Length);

        if (!IsAuthenticated)
        {
            throw new AuthenticationRequiredException();
        }

        using (_logger.BeginScope($"Uploading part {part} for {jobId}"))
        {
            AtProtoHttpResult<ServiceCredential> getServiceAuthResult = await GetServiceAuth(
                Service,
                lxm: UploadBlobLxm,
                expiry: new TimeSpan(0, 30, 0),
                cancellationToken: cancellationToken).ConfigureAwait(false);

            if (!getServiceAuthResult.Succeeded)
            {
                Logger.UploadPartGetServiceAuthFailed(_logger, Did, Service, getServiceAuthResult.StatusCode, getServiceAuthResult.AtErrorDetail?.Error, getServiceAuthResult.AtErrorDetail?.Message);

                return new AtProtoHttpResult<UploadPartResponse>(
                    null,
                    getServiceAuthResult.StatusCode,
                    getServiceAuthResult.HttpResponseHeaders,
                    getServiceAuthResult.AtErrorDetail,
                    getServiceAuthResult.RateLimit);
            }

            using (HttpClient httpClient = HttpClient)
            {
                httpClient.Timeout = timeout ?? httpClient.Timeout;
                try
                {
                    AtProtoHttpResult<UploadPartResponse> result = await BlueskyServer.UploadPart(
                        jobId,
                        part,
                        bytes,
                        service: _videoServer,
                        serviceCredential: getServiceAuthResult.Result,
                        httpClient: httpClient,
                        loggerFactory: LoggerFactory,
                        cancellationToken: cancellationToken).ConfigureAwait(false);

                    if (result.Succeeded)
                    {
                        Logger.UploadPartSucceeded(_logger, part, jobId);
                    }
                    else
                    {
                        Logger.UploadPartFailed(_logger, part, jobId, Service, result.StatusCode, result.AtErrorDetail?.Error, result.AtErrorDetail?.Message);
                    }

                    return result;
                }
                catch (HttpRequestException ex)
                {
                    Logger.UploadPartThrew(_logger, part, jobId, Service, ex);
                    throw;
                }
            }
        }
    }
}
