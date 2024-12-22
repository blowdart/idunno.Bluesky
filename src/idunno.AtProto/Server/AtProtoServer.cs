// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using Microsoft.Extensions.Logging;

using idunno.AtProto.Models;
using idunno.AtProto.Server;

namespace idunno.AtProto
{
    /// <summary>
    /// Represents an atproto server and provides methods to send messages and receive responses from the server.
    /// </summary>
    public static partial class AtProtoServer
    {
        // https://docs.bsky.app/docs/api/com-atproto-server-describe-server
        private const string DescribeEndpoint = "/xrpc/com.atproto.server.describeServer";

        // https://docs.bsky.app/docs/api/com-atproto-server-create-session
        private const string CreateSessionEndpoint = "/xrpc/com.atproto.server.createSession";

        // https://docs.bsky.app/docs/api/com-atproto-server-delete-session
        private const string DeleteSessionEndpoint = "/xrpc/com.atproto.server.deleteSession";

        // https://docs.bsky.app/docs/api/com-atproto-server-refresh-session
        private const string RefreshSessionEndpoint = "/xrpc/com.atproto.server.refreshSession";

        // https://docs.bsky.app/docs/api/com-atproto-server-get-session
        private const string GetSessionEndpoint = "/xrpc/com.atproto.server.getSession";

        /// <summary>
        /// Describes the server's account creation requirements and capabilities.
        /// </summary>
        /// <param name="service">The service whose account description is to be retrieved.</param>
        /// <param name="httpClient">An <see cref="HttpClient"/> to use when making a request to the <paramref name="service"/>.</param>
        /// <param name="loggerFactory">An instance of <see cref="ILoggerFactory"/> to use to create a logger.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="service"/> or <paramref name="httpClient"/> is null.</exception>
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

            AtProtoHttpResult<ServerDescription> result = await request.Get(service, DescribeEndpoint, httpClient, cancellationToken).ConfigureAwait(false);

            if (result.Succeeded &&
                (result.Result.AvailableUserDomains is null || result.Result.AvailableUserDomains.Count == 0))
            {
                throw new ResponseParseException("Response missing required availableUserDomains array.");
            }

            return result;
        }

        /// <summary>
        /// Create an authenticated session on the specified <paramref name="service"/>.
        /// </summary>
        /// <param name="credentials"><see cref="Credentials"/> containing the user identifier and password to authenticate with.</param>
        /// <param name="service">The service to create an authenticated session on.</param>
        /// <param name="httpClient">An <see cref="HttpClient"/> to use when making a request to the <paramref name="service"/>.</param>
        /// <param name="loggerFactory">An instance of <see cref="ILoggerFactory"/> to use to create a logger.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Throw if <paramref name="credentials"/>, <paramref name="service"/> or <paramref name="httpClient"/> is null.</exception>
        public static async Task<AtProtoHttpResult<CreateSessionResponse>> CreateSession(
            Credentials credentials, Uri service, HttpClient httpClient, ILoggerFactory? loggerFactory = default, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(credentials);
            ArgumentNullException.ThrowIfNull(service);
            ArgumentNullException.ThrowIfNull(httpClient);

            AtProtoHttpClient<CreateSessionResponse> request = new(loggerFactory);

            AtProtoHttpResult<CreateSessionResponse> result = await request.Post(
                service,
                CreateSessionEndpoint,
                credentials,
                accessToken: null, // We have no access token at this point.
                httpClient: httpClient,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            return result;
        }

        /// <summary>
        /// Deletes an authenticated session.
        /// </summary>
        /// <param name="refreshToken">The refresh token for which the associated session should be deleted.</param>
        /// <param name="service">The service to create an authenticated session on.</param>
        /// <param name="httpClient">An <see cref="HttpClient"/> to use when making a request to the <paramref name="service"/>.</param>
        /// <param name="loggerFactory">An instance of <see cref="ILoggerFactory"/> to use to create a logger.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <remarks><para>Delete session requires the refresh token, not the access token.</para></remarks>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="refreshToken"/>, <paramref name="service"/> or <paramref name="httpClient"/> is null.</exception>
        public static async Task<AtProtoHttpResult<EmptyResponse>> DeleteSession(
            string refreshToken,
            Uri service,
            HttpClient httpClient,
            ILoggerFactory? loggerFactory = default,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNullOrWhiteSpace(refreshToken);
            ArgumentNullException.ThrowIfNull(service);
            ArgumentNullException.ThrowIfNull(httpClient);

            AtProtoHttpClient<EmptyResponse> request = new(loggerFactory);

            return await request.Post(
                service,
                DeleteSessionEndpoint,
                refreshToken,
                httpClient: httpClient,
                cancellationToken:
                cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Refreshes an authenticated session.
        /// </summary>
        /// <param name="refreshToken">The refresh token for the session to be refreshed.</param>
        /// <param name="service">The service to refresh the authenticated session on.</param>
        /// <param name="httpClient">An <see cref="HttpClient"/> to use when making a request to the <paramref name="service"/>.</param>
        /// <param name="loggerFactory">An instance of <see cref="ILoggerFactory"/> to use to create a logger.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="refreshToken"/>, <paramref name="service"/> or <paramref name="httpClient"/> is null.</exception>
        public static async Task<AtProtoHttpResult<RefreshSessionResponse>> RefreshSession(
            string refreshToken,
            Uri service,
            HttpClient httpClient,
            ILoggerFactory? loggerFactory = default,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNullOrEmpty(refreshToken);
            ArgumentNullException.ThrowIfNull(service);
            ArgumentNullException.ThrowIfNull(httpClient);

            AtProtoHttpClient<RefreshSessionResponse> request = new(loggerFactory);

            AtProtoHttpResult<RefreshSessionResponse> result = await request.Post(
                service,
                RefreshSessionEndpoint,
                refreshToken,
                httpClient: httpClient,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            return result;
        }

        /// <summary>
        /// Gets information about the current authenticated session the specified <paramref name="service"/>.
        /// </summary>
        /// <param name="accessToken">The access session for the token on the specified <paramref name="service" />.</param>
        /// <param name="service">The service to retrieve the session information from.</param>
        /// <param name="httpClient">An <see cref="HttpClient"/> to use when making a request to the <paramref name="service"/>.</param>
        /// <param name="loggerFactory">An instance of <see cref="ILoggerFactory"/> to use to create a logger.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="accessToken"/>, <paramref name="service"/> or <paramref name="httpClient"/> is null.</exception>
        public static async Task<AtProtoHttpResult<GetSessionResponse>> GetSession(
            string accessToken,
            Uri service,
            HttpClient httpClient,
            ILoggerFactory? loggerFactory = default,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNullOrEmpty(accessToken);
            ArgumentNullException.ThrowIfNull(service);
            ArgumentNullException.ThrowIfNull(httpClient);

            AtProtoHttpClient<GetSessionResponse> request = new(loggerFactory);

            return await request.Get(service, GetSessionEndpoint, accessToken, httpClient: httpClient, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
    }
}
