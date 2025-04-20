// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;

using idunno.AtProto;
using idunno.AtProto.Authentication;

using idunno.Bluesky.Labeler;
using idunno.Bluesky.Labeler.Model;

namespace idunno.Bluesky
{
    public static partial class BlueskyServer
    {
        // https://docs.bsky.app/docs/api/app-bsky-labeler-get-services
        private const string LablerGetServicesEndpoint = "/xrpc/app.bsky.labeler.getServices";

        /// <summary>
        /// Gets information about the labeller services identified by the specified <paramref name="dids"/>.
        /// </summary>
        /// <param name="dids">A collection of <see cref="Did"/>s for the labellers whose service views should be returned</param>
        /// <param name="getDetailedViews">Flag indicating whether a detailed view for each service should be returned.</param>
        /// <param name="service">The <see cref="Uri"/> of the service to retrieve the profile from.</param>
        /// <param name="accessCredentials">The <see cref="AccessCredentials"/> used to authenticate to <paramref name="service"/>.</param>
        /// <param name="httpClient">An <see cref="HttpClient"/> to use when making a request to the <paramref name="service"/>.</param>
        /// <param name="onCredentialsUpdated">An <see cref="Action{T}" /> to call if the credentials in the request need updating.</param>
        /// <param name="loggerFactory">An instance of <see cref="ILoggerFactory"/> to use to create a logger.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="dids"/>, <paramref name="service"/>, <paramref name="accessCredentials"/> or <paramref name="httpClient"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="dids"/> is an empty collection.</exception>
        [UnconditionalSuppressMessage(
            "Trimming",
            "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
            Justification = "All types are preserved in the JsonSerializerOptions call to Get().")]
        [UnconditionalSuppressMessage("AOT",
            "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
            Justification = "All types are preserved in the JsonSerializerOptions call to Get().")]
        public static async Task<AtProtoHttpResult<ICollection<LabelerView>>> GetLabelerServices(
            IEnumerable<Did> dids,
            bool getDetailedViews,
            Uri service,
            AccessCredentials accessCredentials,
            HttpClient httpClient,
            Action<AtProtoCredential>? onCredentialsUpdated = null,
            ILoggerFactory? loggerFactory = default,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(dids);
            ArgumentOutOfRangeException.ThrowIfLessThan(dids.Count(), 1);

            ArgumentNullException.ThrowIfNull(service);
            ArgumentNullException.ThrowIfNull(accessCredentials);
            ArgumentNullException.ThrowIfNull(httpClient);

            string queryString = string.Join("&", dids.Select(did => $"dids={Uri.EscapeDataString(did.ToString())}"));

            if (getDetailedViews)
            {
                queryString += "&detailed=true";
            }

            AtProtoHttpClient<GetServicesResponse> request = new(AppViewProxy, loggerFactory);
            AtProtoHttpResult<GetServicesResponse> response = await request.Get(
                service,
                $"{LablerGetServicesEndpoint}?{queryString}",
                credentials: accessCredentials,
                httpClient: httpClient,
                jsonSerializerOptions: BlueskyJsonSerializerOptions,
                onCredentialsUpdated: onCredentialsUpdated,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            if (response.Succeeded)
            {
                List<LabelerView> labels;

                if (response.Result.Views is null)
                {
                    labels = [];
                }
                else
                {
                    labels = [.. response.Result.Views];
                }

                return new AtProtoHttpResult<ICollection<LabelerView>>(
                    result: labels,
                    statusCode: response.StatusCode,
                    httpResponseHeaders: response.HttpResponseHeaders,
                    atErrorDetail: response.AtErrorDetail,
                    rateLimit: response.RateLimit);
            }
            else
            {
                return new AtProtoHttpResult<ICollection<LabelerView>>(
                    result: [],
                    statusCode: response.StatusCode,
                    httpResponseHeaders: response.HttpResponseHeaders,
                    atErrorDetail: response.AtErrorDetail,
                    rateLimit: response.RateLimit);
            }
        }
    }
}
