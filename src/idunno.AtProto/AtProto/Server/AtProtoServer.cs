// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.


// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto.Server;

namespace idunno.AtProto
{
    /// <summary>
    /// Represents an atproto server and provides methods to send messages and receive responses from the server.
    /// </summary>
    internal partial class AtProtoServer
    {
        // https://docs.bsky.app/docs/api/com-atproto-server-describe-server
        private const string DescribeEndpoint = "/xrpc/com.atproto.server.describeServer";

        // https://docs.bsky.app/docs/api/com-atproto-server-create-session
        private const string CreateSessionEndpoint = "/xrpc/com.atproto.server.createSession";

        // https://docs.bsky.app/docs/api/com-atproto-server-delete-session
        private const string DeleteSessionEndpoint = "/xrpc/com.atproto.server.deleteSession";

        // https://docs.bsky.app/docs/api/com-atproto-server-refresh-session
        private const string RefreshSessionEndpoint = "/xrpc/com.atproto.server.refreshSession";

        /// <summary>
        /// Describes the server's account creation requirements and capabilities.
        /// </summary>
        /// <param name="service">The service whose account description is to be retrieved.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <param name="httpClientHandler">An <see cref="HttpClientHandler"/> to use when making a request to the <paramref name="service"/>.</param>
        /// <returns>An <see cref="HttpResult"/> wrapping a <see cref="ServerDescription"/> containing the server account creation requirements and capabilities.</returns>
        /// <exception cref="ResponseParseException">Thrown when the response from the service cannot be parsed or does not pass validation.</exception>
        public static async Task<HttpResult<ServerDescription>> DescribeServer(Uri service, HttpClientHandler? httpClientHandler, CancellationToken cancellationToken)
        {
            AtProtoHttpClient<ServerDescription> request = new();

            HttpResult<ServerDescription> result = await request.Get(service, DescribeEndpoint, httpClientHandler, cancellationToken).ConfigureAwait(false);

            if (result.Succeeded && result.Result is not null)
            {
                if (result.Result.AvailableUserDomains is null ||
                    result.Result.AvailableUserDomains.Count == 0)
                {
                    throw new ResponseParseException("Response missing required availableUserDomains array.");
                }
            }

            return result;
        }

        /// <summary>
        /// Create an authenticated session on the specified <paramref name="service"/>.
        /// </summary>
        /// <param name="credentials"><see cref="Credentials"/> containing the user identifier and password to authenticate with.</param>
        /// <param name="service">The service to create an authenticated session on.</param>
        /// <param name="httpClientHandler">An <see cref="HttpClientHandler"/> to use when making a request to the <paramref name="service"/>.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <result>An <see cref="HttpResult"/> wrapping a <see cref="Session"/> created on the specified <paramref name="service"/>.</result>
        /// <exception cref="ResponseParseException">Thrown when the response from the service cannot be parsed or does not pass validation.</exception>
        public static async Task<HttpResult<Session>> CreateSession(Credentials credentials, Uri service, HttpClientHandler? httpClientHandler, CancellationToken cancellationToken)
        {
            AtProtoHttpClient<Session> request = new();

            return await request.Post(service, CreateSessionEndpoint, credentials, httpClientHandler, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Deletes an authenticated session.
        /// </summary>
        /// <param name="refreshToken">The refresh token for which the associated session should be deleted.</param>
        /// <param name="service">The service to create an authenticated session on.</param>
        /// <param name="httpClientHandler">An <see cref="HttpClientHandler"/> to use when making a request to the <paramref name="service"/>.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <exception cref="ResponseParseException">Thrown when the response from the service cannot be parsed or does not pass validation.</exception>
        public static async Task<HttpResult<EmptyResponse>> DeleteSession(string refreshToken, Uri service, HttpClientHandler? httpClientHandler, CancellationToken cancellationToken)
        {
            // delete-session works on the refresh token, not the access token.

            AtProtoHttpClient<EmptyResponse> request = new();

            return await request.Post(service, DeleteSessionEndpoint, null, refreshToken, httpClientHandler, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Refreshes an authenticated session.
        /// </summary>
        /// <param name="refreshToken">The refresh token for the session to be refreshed.</param>
        /// <param name="service">The service to refresh the authenticated session on.</param>
        /// <param name="httpClientHandler">An <see cref="HttpClientHandler"/> to use when making a request to the <paramref name="service"/>.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <exception cref="ResponseParseException">Thrown when the response from the service cannot be parsed or does not pass validation.</exception>
        public static async Task<HttpResult<Session>> RefreshSession(string refreshToken, Uri service, HttpClientHandler? httpClientHandler, CancellationToken cancellationToken)
        {
            AtProtoHttpClient<Session> request = new();

            return await request.Post(service, RefreshSessionEndpoint, requestBody: null, refreshToken, httpClientHandler, cancellationToken).ConfigureAwait(false);
        }
    }
}
