// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Net;

namespace idunno.AtProto.Events
{
    /// <summary>
    /// A class holding information on why a session refresh failed.
    /// </summary>
    /// <param name="did">The <see cref="Did"/> the credentials belonged to</param>
    /// <param name="service">The <see cref="Uri"/> of the service that returned the error, if an error was returned from the API.</param>
    /// <param name="refreshToken">The refresh token that failed to refresh</param>
    /// <param name="statusCode">The <see cref="HttpStatusCode"/> returned from the API, if an error was from the API.</param>
    /// <param name="error">The <see cref="AtErrorDetail" /> returned from the API, if any.</param>
    public sealed class TokenRefreshFailedEventArgs(
        Did did,
        Uri service,
        string refreshToken,
        HttpStatusCode? statusCode,
        AtErrorDetail? error) : BaseAuthenticationEventArgs(did, service)
    {

        /// <summary>
        /// Gets the refresh token which failed to refresh
        /// </summary>
        public string RefreshToken { get; } = refreshToken;

        /// <summary>
        /// Gets the <see cref="HttpStatusCode"/> returned from the API, if an error was from the API.
        /// </summary>
        public HttpStatusCode? StatusCode { get; } = statusCode;

        /// <summary>
        /// Gets the <see cref="AtErrorDetail" /> returned from the API, if any.
        /// </summary>
        public AtErrorDetail? Error { get; } = error;
    }
}
