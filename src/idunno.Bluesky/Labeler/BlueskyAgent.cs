// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using idunno.AtProto;

using idunno.Bluesky.Actor;
using idunno.Bluesky.Labeler;
using idunno.Bluesky.Record;

namespace idunno.Bluesky
{
    public partial class BlueskyAgent
    {
        /// <summary>
        /// Gets the labeler declaration record value for the specified <paramref name="identifier"/>.
        /// </summary>
        /// <param name="identifier">The labeler <see cref="Handle"/> whose declaraction record should be returned.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="identifier"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="identifier"/> cannot be converted to a <see cref="Did"/> or <see cref="Handle"/>.</exception>
        public async Task<AtProtoHttpResult<LabelerDeclarationRecord>> GetLabelerDeclaration(
            AtIdentifier identifier,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(identifier);

            if (identifier is Did did)
            {
                return await GetLabelerDeclaration(did, cancellationToken: cancellationToken).ConfigureAwait(false);
            }
            else if (identifier is Handle handle)
            {
                return await GetLabelerDeclaration(handle, cancellationToken: cancellationToken).ConfigureAwait(false);
            }
            else
            {
                throw new ArgumentException("identifier is neither a handle or a did.", nameof(identifier));
            }

        }

        /// <summary>
        /// Gets the labeler declaration record value for the specified <paramref name="handle"/>.
        /// </summary>
        /// <param name="handle">The labeler <see cref="Handle"/> whose declaraction record should be returned.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="handle"/> is null.</exception>
        public async Task<AtProtoHttpResult<LabelerDeclarationRecord>> GetLabelerDeclaration(
            Handle handle,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(handle);

            Did? did = await ResolveHandle(handle, cancellationToken: cancellationToken).ConfigureAwait(false);

            if (did == null)
            {
                return new AtProtoHttpResult<LabelerDeclarationRecord>(
                    null,
                    statusCode: System.Net.HttpStatusCode.NotFound,
                    httpResponseHeaders: null);
            }

            return await GetLabelerDeclaration(did, cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Gets the labeler declaration record value for the specified <paramref name="did"/>.
        /// </summary>
        /// <param name="did">The labeler <see cref="AtProto.Did" /> whose declaraction record should be returned.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="did"/> is null.</exception>
        [UnconditionalSuppressMessage(
            "Trimming",
            "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
            Justification = "All types are preserved in the JsonSerializerOptions call to Get().")]
        [UnconditionalSuppressMessage("AOT",
            "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
            Justification = "All types are preserved in the JsonSerializerOptions call to Get().")]
        public async Task<AtProtoHttpResult<LabelerDeclarationRecord>> GetLabelerDeclaration(
            Did did,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(did);

            return await GetRecord<LabelerDeclarationRecord>(
                new AtUri($"at://{did}/app.bsky.labeler.service/self"),
                jsonSerializerOptions: BlueskyServer.BlueskyJsonSerializerOptions,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Gets information about the labeller services that the current user subscribes to.
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
                    userPreferencesResult.AtErrorDetail!.Message);

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
        /// Gets information about the labeller services identified by the specified <paramref name="dids"/>.
        /// </summary>
        /// <param name="dids">A collection of <see cref="Did"/>s for the labellers whose service views should be returned</param>
        /// <param name="getDetailedViews">Flag indicating whether a detailed view for each service should be returned.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="dids"/> is null.</exception>
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

        /// <summary>
        /// Gets detailed information about the labeller services that the current user subscribes to.
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
                    userPreferencesResult.AtErrorDetail!.Message);

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
}
