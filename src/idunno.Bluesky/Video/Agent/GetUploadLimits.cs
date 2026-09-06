// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;
using idunno.AtProto.Authentication;
using idunno.Bluesky.Video;

namespace idunno.Bluesky;

public partial class BlueskyAgent
{
    /// <summary>
    /// Gets any video upload restrictions placed on the current user
    /// </summary>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="AuthenticationRequiredException">Thrown when the agent is not authenticated.</exception>
    public async Task<AtProtoHttpResult<UploadLimits>> GetUploadLimits(
        CancellationToken cancellationToken = default)
    {
        if (!IsAuthenticated)
        {
            throw new AuthenticationRequiredException();
        }

        AtProtoHttpResult<ServiceCredential> getServiceAuthResult = await GetServiceAuth(
            Service,
            audience: WellKnownDistributedIdentifiers.Video,
            lxm: "app.bsky.video.getUploadLimits",
            cancellationToken: cancellationToken).ConfigureAwait(false);

        if (!getServiceAuthResult.Succeeded)
        {
            Logger.GetUploadLimitsServiceAuthFailed(_logger, Did, Service, getServiceAuthResult.StatusCode, getServiceAuthResult.AtErrorDetail?.Error, getServiceAuthResult.AtErrorDetail?.Message);

            return new AtProtoHttpResult<UploadLimits>(
                null,
                getServiceAuthResult.StatusCode,
                getServiceAuthResult.HttpResponseHeaders,
                getServiceAuthResult.AtErrorDetail,
                getServiceAuthResult.RateLimit);
        }

        return await BlueskyServer.GetUploadLimits(
            _videoServer,
            serviceCredential: getServiceAuthResult.Result,
            httpClient: HttpClient,
            loggerFactory: LoggerFactory,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}
