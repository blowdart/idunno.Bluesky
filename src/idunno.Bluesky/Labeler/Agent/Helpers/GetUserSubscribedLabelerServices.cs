// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;
using idunno.Bluesky.Actor;
using idunno.Bluesky.Labeler;

namespace idunno.Bluesky;

public partial class BlueskyAgent
{
    /// <summary>
    /// Gets detailed information about the labeler services that the current user subscribes to.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="AuthenticationRequiredException">Thrown when this instance of the agent is not authenticated.</exception>
    public async Task<AtProtoHttpResult<ICollection<LabelerViewDetailed>>> GetUserSubscribedLabelerServices(
        CancellationToken cancellationToken = default)
    {
        if (!IsAuthenticated)
        {
            throw new AuthenticationRequiredException();
        }

        AtProtoHttpResult<Preferences> userPreferencesResult = await GetPreferences(cancellationToken: cancellationToken).ConfigureAwait(false);

        if (!userPreferencesResult.Succeeded)
        {
            Logger.GetUserPreferencesFailedInGetLabelerServices(
                _logger,
                userPreferencesResult.StatusCode,
                userPreferencesResult.AtErrorDetail!.Error,
                userPreferencesResult.AtErrorDetail.Message);

            return new AtProtoHttpResult<ICollection<LabelerViewDetailed>>(
                result: null,
                statusCode: userPreferencesResult.StatusCode,
                httpResponseHeaders: userPreferencesResult.HttpResponseHeaders,
                atErrorDetail: userPreferencesResult.AtErrorDetail,
                rateLimit: userPreferencesResult.RateLimit);
        }

        AtProtoHttpResult<ICollection<LabelerView>> getLabelerServicesResult = await BlueskyServer.GetLabelerServices(
            dids: userPreferencesResult.Result.SubscribedLabelers,
            getDetailedViews: true,
            service: Service,
            accessCredentials: Credentials,
            httpClient: HttpClient,
            onCredentialsUpdated: InternalOnCredentialsUpdatedCallBack,
            loggerFactory: LoggerFactory,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        List<LabelerViewDetailed>? labelerViewsDetailed = null;
        if (getLabelerServicesResult.Succeeded)
        {
            labelerViewsDetailed = [];
            foreach (LabelerView labelerView in getLabelerServicesResult.Result)
            {
                if (labelerView is LabelerViewDetailed ld)
                {
                    labelerViewsDetailed.Add(ld);
                }
            }
        }

        return new AtProtoHttpResult<ICollection<LabelerViewDetailed>>(
            result: labelerViewsDetailed,
            statusCode: getLabelerServicesResult.StatusCode,
            httpResponseHeaders: getLabelerServicesResult.HttpResponseHeaders,
            atErrorDetail: getLabelerServicesResult.AtErrorDetail,
            rateLimit: getLabelerServicesResult.RateLimit);
    }
}