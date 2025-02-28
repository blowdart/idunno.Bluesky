// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Extensions.Logging;

using idunno.AtProto.Server.Models;

namespace idunno.AtProto
{
    /// <summary>
    /// Represents an atproto server and provides methods to send messages and receive responses from the server.
    /// </summary>
    public static partial class AtProtoServer
    {
        // https://docs.bsky.app/docs/api/com-atproto-server-describe-server
        internal const string DescribeServerEndpoint = "/xrpc/com.atproto.server.describeServer";


        /// <summary>
        /// Describes the server's account creation requirements and capabilities.
        /// </summary>
        /// <param name="service">The service whose account description is to be retrieved.</param>
        /// <param name="httpClient">An <see cref="HttpClient"/> to use when making a request to the <paramref name="service"/>.</param>
        /// <param name="loggerFactory">An instance of <see cref="ILoggerFactory"/> to use to create a logger.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="service"/> or <paramref name="httpClient"/> is null.</exception>
        /// <exception cref="ResponseParseException">Thrown when the response from the service cannot be parsed or does not pass validation.</exception>
        public static async Task<AtProtoHttpResult<ServerDescription>> DescribeServer(
            Uri service,
            HttpClient httpClient,
            ILoggerFactory? loggerFactory = default,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(service);
            ArgumentNullException.ThrowIfNull(httpClient);

            AtProtoHttpClient<ServerDescription> request = new(loggerFactory);

            AtProtoHttpResult<ServerDescription> result = await request.Get(service, DescribeServerEndpoint, httpClient, cancellationToken).ConfigureAwait(false);

            if (result.Succeeded &&
                (result.Result.AvailableUserDomains is null || result.Result.AvailableUserDomains.Count == 0))
            {
                throw new ResponseParseException("Response missing required availableUserDomains array.");
            }

            return result;
        }
    }
}
