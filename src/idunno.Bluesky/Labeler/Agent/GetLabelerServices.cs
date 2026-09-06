// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;
using idunno.Bluesky.Actor;
using idunno.Bluesky.Labeler;

namespace idunno.Bluesky;

public partial class BlueskyAgent
{
    /// <summary>
    /// Gets information about the labeler services that the current user subscribes to.
    /// </summary>
    /// <param name="getDetailedViews">Flag indicating whether a detailed view for each service should be returned.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="AuthenticationRequiredException">Thrown when this instance of the agent is not authenticated.</exception>
    public async Task<AtProtoHttpResult<ICollection<LabelerView>>> GetLabelerServices(
        bool getDetailedViews = false,
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

            return new AtProtoHttpResult<ICollection<LabelerView>>(
                result: null,
                statusCode: userPreferencesResult.StatusCode,
                httpResponseHeaders: userPreferencesResult.HttpResponseHeaders,
                atErrorDetail: userPreferencesResult.AtErrorDetail,
                rateLimit: userPreferencesResult.RateLimit);
        }

        return await BlueskyServer.GetLabelerServices(
            dids: userPreferencesResult.Result.SubscribedLabelers,
            getDetailedViews: getDetailedViews,
            service: Service,
            accessCredentials: Credentials,
            httpClient: HttpClient,
            onCredentialsUpdated: InternalOnCredentialsUpdatedCallBack,
            loggerFactory: LoggerFactory,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }


    /// <summary>
    /// Gets information about the labeler services identified by the specified <paramref name="dids"/>.
    /// </summary>
    /// <param name="dids">A collection of <see cref="Did"/>s for the labelers whose service views should be returned</param>
    /// <param name="getDetailedViews">Flag indicating whether a detailed view for each service should be returned.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="dids"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="dids"/> is an empty collection.</exception>
    /// <exception cref="AuthenticationRequiredException">Thrown when this instance of the agent is not authenticated.</exception>
    public async Task<AtProtoHttpResult<ICollection<LabelerView>>> GetLabelerServices(
        IEnumerable<Did> dids,
        bool getDetailedViews = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dids);
        ArgumentOutOfRangeException.ThrowIfLessThan(dids.Count(), 1);

        if (!IsAuthenticated)
        {
            throw new AuthenticationRequiredException();
        }

        return await BlueskyServer.GetLabelerServices(
            dids: dids,
            getDetailedViews: getDetailedViews,
            service: Service,
            accessCredentials: Credentials,
            httpClient: HttpClient,
            onCredentialsUpdated: InternalOnCredentialsUpdatedCallBack,
            loggerFactory: LoggerFactory,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}
