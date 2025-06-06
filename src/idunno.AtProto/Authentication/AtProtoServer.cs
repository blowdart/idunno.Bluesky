﻿// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Globalization;
using System.Text;

using Microsoft.Extensions.Logging;

using idunno.AtProto.Authentication;
using idunno.AtProto.Authentication.Models;
using System.Diagnostics.CodeAnalysis;

namespace idunno.AtProto
{
    public static partial class AtProtoServer
    {
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
        /// Create an authenticated session on the specified <paramref name="service"/>.
        /// </summary>
        /// <param name="identifier">The account identifier to authenticate with.</param>
        /// <param name="password">The account identifier to authenticate with.</param>
        /// <param name="authFactorToken">The multifactor authentication token to authenticate with.</param>
        /// <param name="service">The service to authenticate against.</param>
        /// <param name="httpClient">An <see cref="HttpClient"/> to use when making a request to the <paramref name="service"/>.</param>
        /// <param name="loggerFactory">An instance of <see cref="ILoggerFactory"/> to use to create a logger.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentException">Throw if <paramref name="identifier"/> or <paramref name="password"/> is null.</exception>
        /// <exception cref="ArgumentNullException">Throw if <paramref name="service"/> or <paramref name="httpClient"/> is null.</exception>
        [UnconditionalSuppressMessage("AOT",
            "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
            Justification = "All types are preserved in the JsonSerializerOptions call to Post().")]
        [UnconditionalSuppressMessage(
            "Trimming",
            "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
            Justification = "All types are preserved in the JsonSerializerOptions call to Post().")]
        public static async Task<AtProtoHttpResult<Session>> CreateSession(
            string identifier,
            string password,
            string? authFactorToken,
            Uri service,
            HttpClient httpClient,
            ILoggerFactory? loggerFactory = default,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(identifier);
            ArgumentException.ThrowIfNullOrWhiteSpace(password);

            ArgumentNullException.ThrowIfNull(service);
            ArgumentNullException.ThrowIfNull(httpClient);

            AtProtoHttpClient<CreateSessionResponse> request = new(loggerFactory);

            CreateSessionRequest loginRequestRecord = new() { Identifier = identifier, Password = password, AuthFactorToken = authFactorToken };

            AtProtoHttpResult<CreateSessionResponse> createSessionResponse = await request.Post(
                service,
                CreateSessionEndpoint,
                loginRequestRecord,
                httpClient: httpClient,
                jsonSerializerOptions: AtProtoJsonSerializerOptions,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            if (createSessionResponse.Succeeded)
            {
                return new AtProtoHttpResult<Session>(
                    result: new Session(createSessionResponse.Result),
                    statusCode: createSessionResponse.StatusCode,
                    httpResponseHeaders: createSessionResponse.HttpResponseHeaders,
                    atErrorDetail: createSessionResponse.AtErrorDetail,
                    rateLimit: createSessionResponse.RateLimit);
            }
            else
            {
                return new AtProtoHttpResult<Session>(
                    null,
                    statusCode: createSessionResponse.StatusCode,
                    httpResponseHeaders: createSessionResponse.HttpResponseHeaders,
                    atErrorDetail: createSessionResponse.AtErrorDetail,
                    rateLimit: createSessionResponse.RateLimit);
            }
        }

        /// <summary>
        /// Deletes an authenticated session.
        /// </summary>
        /// <param name="refreshCredential">The refresh credentials to delete the session for.</param>
        /// <param name="httpClient">An <see cref="HttpClient"/> to use when making a request`.</param>
        /// <param name="loggerFactory">An instance of <see cref="ILoggerFactory"/> to use to create a logger.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <remarks><para>Delete session requires the refresh token, not the access token.</para></remarks>
        /// <exception cref="ArgumentException">Thrown when <paramref name="refreshCredential"/>'s refresh token is null or whitespace.</exception>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="refreshCredential"/> or <paramref name="httpClient"/> is null.</exception>
        [UnconditionalSuppressMessage(
            "Trimming",
            "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
            Justification = "All types are preserved in the JsonSerializerOptions call to Post().")]
        [UnconditionalSuppressMessage("AOT",
            "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
            Justification = "All types are preserved in the JsonSerializerOptions call to Post().")]
        public static async Task<AtProtoHttpResult<EmptyResponse>> DeleteSession(
            RefreshCredential refreshCredential,
            HttpClient httpClient,
            ILoggerFactory? loggerFactory = default,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(refreshCredential);
            ArgumentNullException.ThrowIfNull(httpClient);

            ArgumentException.ThrowIfNullOrWhiteSpace(refreshCredential.RefreshToken);

            AtProtoHttpClient<EmptyResponse> request = new(loggerFactory);

            AtProtoHttpResult<EmptyResponse> result = await request.Post(
                service: refreshCredential.Service,
                endpoint: DeleteSessionEndpoint,
                credentials: refreshCredential,
                httpClient: httpClient,
                jsonSerializerOptions: AtProtoJsonSerializerOptions,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            return result;
        }

        /// <summary>
        /// Refreshes an authenticated session.
        /// </summary>
        /// <param name="refreshCredential">The access credentials to use.</param>
        /// <param name="httpClient">An <see cref="HttpClient"/> to use when making a request to the <paramref name="refreshCredential"/>'s service.</param>
        /// <param name="credentialsUpdated">An <see cref="Action{T}" /> to call if the credentials in the request need updating.</param>
        /// <param name="loggerFactory">An instance of <see cref="ILoggerFactory"/> to use to create a logger.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentException">Thrown when the <paramref name="refreshCredential"/> has a null or whitespace refresh token.</exception>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="refreshCredential"/> or <paramref name="httpClient"/> is null.</exception>
        [UnconditionalSuppressMessage(
            "Trimming",
            "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
            Justification = "All types are preserved in the JsonSerializerOptions call to Post().")]
        [UnconditionalSuppressMessage("AOT",
            "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
            Justification = "All types are preserved in the JsonSerializerOptions call to Post().")]
        public static async Task<AtProtoHttpResult<Session>> RefreshSession(
            RefreshCredential refreshCredential,
            HttpClient httpClient,
            Action<AtProtoCredential>? credentialsUpdated = null,
            ILoggerFactory? loggerFactory = default,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(refreshCredential);
            ArgumentNullException.ThrowIfNull(httpClient);

            ArgumentException.ThrowIfNullOrWhiteSpace(refreshCredential.RefreshToken);

            AtProtoHttpClient<RefreshSessionResponse> request = new(loggerFactory);

            AtProtoHttpResult<RefreshSessionResponse> refreshSessionResponse = await request.Post(
                service: refreshCredential.Service,
                endpoint: RefreshSessionEndpoint,
                credentials: refreshCredential,
                httpClient: httpClient,
                onCredentialsUpdated: credentialsUpdated,
                jsonSerializerOptions: AtProtoJsonSerializerOptions,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            if (!refreshSessionResponse.Succeeded)
            {
                return new AtProtoHttpResult<Session>(
                    null,
                    statusCode: refreshSessionResponse.StatusCode,
                    httpResponseHeaders: refreshSessionResponse.HttpResponseHeaders,
                    atErrorDetail: refreshSessionResponse.AtErrorDetail,
                    rateLimit: refreshSessionResponse.RateLimit);
            }

            // As a refreshSession call leaves out some session information, take the results and call GetSession to fill it out.
            return await GetSession(
                accessCredentials: new AccessCredentials(
                    refreshCredential.Service,
                    refreshCredential.AuthenticationType,
                    refreshSessionResponse.Result.AccessJwt,
                    refreshSessionResponse.Result.RefreshJwt),
                httpClient: httpClient,
                credentialsUpdated: credentialsUpdated,
                loggerFactory: loggerFactory,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Gets information about session associated with the specified <paramref name="accessCredentials"/>.
        /// </summary>
        /// <param name="accessCredentials">The access credentials to retrieve the session for.</param>
        /// <param name="httpClient">An <see cref="HttpClient"/> to use when making the API request.</param>
        /// <param name="credentialsUpdated">An <see cref="Action{T}" /> to call if the credentials in the request need updating.</param>
        /// <param name="loggerFactory">An instance of <see cref="ILoggerFactory"/> to use to create a logger.</param>
        /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="accessCredentials"/> or <paramref name="httpClient"/> is null.</exception>
        [UnconditionalSuppressMessage(
            "Trimming",
            "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
            Justification = "All types are preserved in the JsonSerializerOptions call to Get().")]
        [UnconditionalSuppressMessage("AOT",
            "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
            Justification = "All types are preserved in the JsonSerializerOptions call to Get().")]
        public static async Task<AtProtoHttpResult<Session>> GetSession(
            AccessCredentials accessCredentials,
            HttpClient httpClient,
            Action<AtProtoCredential>? credentialsUpdated = null,
            ILoggerFactory? loggerFactory = default,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(accessCredentials);
            ArgumentNullException.ThrowIfNull(httpClient);
            ArgumentException.ThrowIfNullOrEmpty(accessCredentials.AccessJwt);

            AtProtoHttpClient<GetSessionResponse> request = new(loggerFactory);

            AtProtoHttpResult<GetSessionResponse> getSessionResponse = await request.Get(
                accessCredentials.Service,
                GetSessionEndpoint,
                accessCredentials,
                httpClient: httpClient,
                onCredentialsUpdated: credentialsUpdated,
                jsonSerializerOptions: AtProtoJsonSerializerOptions,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            if (getSessionResponse.Succeeded)
            {
                return new AtProtoHttpResult<Session>(
                    result: new Session(getSessionResponse.Result, accessCredentials),
                    statusCode: getSessionResponse.StatusCode,
                    httpResponseHeaders: getSessionResponse.HttpResponseHeaders,
                    atErrorDetail: getSessionResponse.AtErrorDetail,
                    rateLimit: getSessionResponse.RateLimit);
            }
            else
            {
                return new AtProtoHttpResult<Session>(
                    null,
                    statusCode: getSessionResponse.StatusCode,
                    httpResponseHeaders: getSessionResponse.HttpResponseHeaders,
                    atErrorDetail: getSessionResponse.AtErrorDetail,
                    rateLimit: getSessionResponse.RateLimit);
            }
        }

        /// <summary>
        /// Get access credentials on behalf of the requesting DID for the requested <paramref name="audience"/>.
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
        [UnconditionalSuppressMessage(
            "Trimming",
            "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
            Justification = "All types are preserved in the JsonSerializerOptions call to Get().")]
        [UnconditionalSuppressMessage("AOT",
            "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
            Justification = "All types are preserved in the JsonSerializerOptions call to Get().")]
        public static async Task<AtProtoHttpResult<ServiceCredential>> GetServiceAuth(
            Did audience,
            TimeSpan? expiry,
            Nsid lxm,
            Uri service,
            AccessCredentials accessCredentials,
            HttpClient httpClient,
            Action<AtProtoCredential>? accessCredentialsUpdated = null,
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
                onCredentialsUpdated: accessCredentialsUpdated,
                jsonSerializerOptions: AtProtoJsonSerializerOptions,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            if (result.Succeeded)
            {
                return new AtProtoHttpResult<ServiceCredential>(
                    new ServiceCredential(service, accessJwt: result.Result.Token),
                    result.StatusCode,
                    result.HttpResponseHeaders, result
                    .AtErrorDetail,
                    result.RateLimit);
            }
            else
            {
                return new AtProtoHttpResult<ServiceCredential>(null, result.StatusCode, result.HttpResponseHeaders, result.AtErrorDetail, result.RateLimit);
            }
        }
    }
}
