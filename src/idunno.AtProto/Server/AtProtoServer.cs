// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text;
using System.Globalization;

using Microsoft.Extensions.Logging;

using idunno.AtProto.Models;
using idunno.AtProto.Server;
using idunno.AtProto.Authentication;

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

        // https://docs.bsky.app/docs/api/com-atproto-server-get-service-auth
        private const string GetServiceAuthEndpoint = "/xrpc/com.atproto.server.getServiceAuth";

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
        /// <param name="credentials"><see cref="LoginCredentials"/> containing the user identifier and password to authenticate with.</param>
        /// <param name="service">The service to create an authenticated session on.</param>
        /// <param name="httpClient">An <see cref="HttpClient"/> to use when making a request to the <paramref name="service"/>.</param>
        /// <param name="loggerFactory">An instance of <see cref="ILoggerFactory"/> to use to create a logger.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Throw if <paramref name="credentials"/>, <paramref name="service"/> or <paramref name="httpClient"/> is null.</exception>
        public static async Task<AtProtoHttpResult<CreateSessionResponse>> CreateSession(
            LoginCredentials credentials,
            Uri service,
            HttpClient httpClient,
            ILoggerFactory? loggerFactory = default,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(credentials);
            ArgumentNullException.ThrowIfNull(service);
            ArgumentNullException.ThrowIfNull(httpClient);

            AtProtoHttpClient<CreateSessionResponse> request = new(loggerFactory);

            AtProtoHttpResult<CreateSessionResponse> result = await request.Post(
                service,
                CreateSessionEndpoint,
                credentials,
                accessCredentials: null, // We have no access token at this point.
                httpClient: httpClient,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            return result;
        }

        /// <summary>
        /// Deletes an authenticated session.
        /// </summary>
        /// <param name="accessCredentials">The access credentials to use.</param>
        /// <param name="httpClient">An <see cref="HttpClient"/> to use when making a request`  .</param>
        /// <param name="loggerFactory">An instance of <see cref="ILoggerFactory"/> to use to create a logger.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <remarks><para>Delete session requires the refresh token, not the access token.</para></remarks>
        /// <exception cref="ArgumentException">Thrown when <paramref name="accessCredentials"/>'s refresh token is null or whitespace.</exception>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="accessCredentials"/> or <paramref name="httpClient"/> is null.</exception>
        public static async Task<AtProtoHttpResult<EmptyResponse>> DeleteSession(
            AccessCredentials accessCredentials,
            HttpClient httpClient,
            ILoggerFactory? loggerFactory = default,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(accessCredentials );
            ArgumentNullException.ThrowIfNull(httpClient);

            ArgumentException.ThrowIfNullOrWhiteSpace(accessCredentials.RefreshJwt);

            AtProtoHttpClient<EmptyResponse> request = new(loggerFactory);

            AtProtoHttpResult<EmptyResponse> result = await request.Post(
                service: accessCredentials.Service,
                endpoint: DeleteSessionEndpoint,
                accessCredentials: accessCredentials,
                httpClient: httpClient,
                useRefreshToken: true,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            return result;
        }

        /// <summary>
        /// Refreshes an authenticated session.
        /// </summary>
        /// <param name="accessCredentials">The access credentials to use.</param>
        /// <param name="httpClient">An <see cref="HttpClient"/> to use when making a request to the <paramref name="accessCredentials"/>'s service.</param>
        /// <param name="accessCredentialsUpdated">An <see cref="Action{T}" /> to call if the credentials in the request need updating.</param>
        /// <param name="loggerFactory">An instance of <see cref="ILoggerFactory"/> to use to create a logger.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentException">Thrown when the <paramref name="accessCredentials"/> has a null or whitespace refresh token.</exception>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="accessCredentials"/> or <paramref name="httpClient"/> is null.</exception>
        public static async Task<AtProtoHttpResult<RefreshSessionResponse>> RefreshSession(
            AccessCredentials accessCredentials,
            HttpClient httpClient,
            Action<AccessCredentials>? accessCredentialsUpdated = null,
            ILoggerFactory? loggerFactory = default,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(accessCredentials);
            ArgumentNullException.ThrowIfNull(httpClient);

            ArgumentException.ThrowIfNullOrWhiteSpace(accessCredentials.RefreshJwt);

            AtProtoHttpClient<RefreshSessionResponse> request = new(loggerFactory);

            AtProtoHttpResult<RefreshSessionResponse> result = await request.Post(
                service: accessCredentials.Service,
                endpoint: RefreshSessionEndpoint,
                accessCredentials: accessCredentials,
                httpClient: httpClient,
                useRefreshToken: true,
                onAccessCredentialsUpdated: accessCredentialsUpdated,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            return result;
        }

        /// <summary>
        /// Gets information about session associated with the specified <paramref name="accessCredentials"/>.
        /// </summary>
        /// <param name="accessCredentials">The access credentials to retrieve the session for.</param>
        /// <param name="httpClient">An <see cref="HttpClient"/> to use when making the API request.</param>
        /// <param name="accessCredentialsUpdated">An <see cref="Action{T}" /> to call if the credentials in the request need updating.</param>
        /// <param name="loggerFactory">An instance of <see cref="ILoggerFactory"/> to use to create a logger.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="accessCredentials"/> or <paramref name="httpClient"/> is null.</exception>
        public static async Task<AtProtoHttpResult<GetSessionResponse>> GetSession(
            AccessCredentials accessCredentials,
            HttpClient httpClient,
            Action<AccessCredentials>? accessCredentialsUpdated = null,
            ILoggerFactory? loggerFactory = default,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(accessCredentials);
            ArgumentNullException.ThrowIfNull(httpClient);
            ArgumentException.ThrowIfNullOrEmpty(accessCredentials.AccessJwt);

            AtProtoHttpClient<GetSessionResponse> request = new(loggerFactory);

            return await request.Get(
                accessCredentials.Service,
                GetSessionEndpoint,
                accessCredentials,
                httpClient: httpClient,
                onAccessCredentialsUpdated: accessCredentialsUpdated,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Get a signed token on behalf of the requesting DID for the requested <paramref name="audience"/>.
        /// </summary>
        /// <param name="audience">The DID of the service that the token will be used to authenticate with.</param>
        /// <param name="expiry">The length of time the token should be valid for.</param>
        /// <param name="lxm">Lexicon (XRPC) method to bind the requested token to</param>
        /// <param name="service">The service to get a signed token from.</param>
        /// <param name="accessCredentials">The access credentials to retrieve the session for.</param>
        /// <param name="httpClient">An <see cref="HttpClient"/> to use when making a request to the <paramref name="service"/>.</param>
        /// <param name="accessCredentialsUpdated">An <see cref="Action{T}" /> to call if the credentials in the request need updating.</param>
        /// <param name="loggerFactory">An instance of <see cref="ILoggerFactory"/> to use to create a logger.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">
        ///   Thrown when <paramref name="expiry"/>, <paramref name="lxm"/>,
        ///   <paramref name="accessCredentials"/>, <paramref name="service"/> or <paramref name="httpClient"/> is null.
        /// </exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="audience"/> or the AccessJwt in <paramref name="accessCredentials"/> is null or whitespace.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="expiry"/> is zero or negative.</exception>
        public static async Task<AtProtoHttpResult<string>> GetServiceAuth(
            Did audience,
            TimeSpan? expiry,
            Nsid lxm,
            Uri service,
            AccessCredentials accessCredentials,
            HttpClient httpClient,
            Action<AccessCredentials>? accessCredentialsUpdated = null,
            ILoggerFactory? loggerFactory = default,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(audience);
            ArgumentNullException.ThrowIfNull(lxm);
            ArgumentNullException.ThrowIfNull(accessCredentials);
            ArgumentNullException.ThrowIfNull(service);
            ArgumentNullException.ThrowIfNull(httpClient);

            ArgumentException.ThrowIfNullOrWhiteSpace(accessCredentials.AccessJwt);
            ArgumentException.ThrowIfNullOrWhiteSpace(audience);

            if (expiry is not null)
            {
                ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(expiry.Value.TotalSeconds, 0);
            }

            StringBuilder endpointBuilder = new($"{GetServiceAuthEndpoint}?");
            endpointBuilder.Append(CultureInfo.InvariantCulture, $"aud={Uri.EscapeDataString(audience)}");
            if (expiry is not null)
            {
                DateTimeOffset expiresOn = DateTimeOffset.UtcNow + expiry.Value;

                endpointBuilder.Append(CultureInfo.InvariantCulture, $"&exp={expiresOn.ToUnixTimeSeconds()}");
            }
            endpointBuilder.Append(CultureInfo.InvariantCulture, $"&lxm={lxm}");
            string endpoint = endpointBuilder.ToString();

            AtProtoHttpClient<ServiceToken> client = new(loggerFactory);
            AtProtoHttpResult<ServiceToken> result = await client.Get(
                service,
                endpoint,
                accessCredentials,
                httpClient: httpClient,
                onAccessCredentialsUpdated: accessCredentialsUpdated,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            if (result.Succeeded)
            {
                return new AtProtoHttpResult<string>(result.Result.Token, result.StatusCode, result.HttpResponseHeaders, result.AtErrorDetail, result.RateLimit);
            }
            else
            {
                return new AtProtoHttpResult<string>(null, result.StatusCode, result.HttpResponseHeaders, result.AtErrorDetail, result.RateLimit);
            }
        }
    }
}
