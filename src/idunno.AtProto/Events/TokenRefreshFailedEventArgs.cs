// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Net;

namespace idunno.AtProto.Events
{
    /// <summary>
    /// A class holding information on why a session refresh failed.
    /// </summary>
    /// <param name="service">The <see cref="Uri"/> of the service that returned the error, if an error was returned from the API.</param>
    /// <param name="refreshToken">The refresh token that failed to refresh</param>
    /// <param name="statusCode">The <see cref="HttpStatusCode"/> returned from the API, if an error was from the API.</param>
    /// <param name="error">The <see cref="AtErrorDetail" /> returned from the API, if any.</param>
    public sealed class TokenRefreshFailedEventArgs(
        string refreshToken,
        Uri? service = null,
        HttpStatusCode? statusCode = null,
        AtErrorDetail? error = null) : EventArgs
    {
        /// <summary>
        /// Gets the refresh token which failed to refresh
        /// </summary>
        public string RefreshToken { get; } = refreshToken;

        /// <summary>
        /// Gets the <see cref="Uri"/> of the service the session was created on.
        /// </summary>
        public Uri? Service { get; } = service;

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
