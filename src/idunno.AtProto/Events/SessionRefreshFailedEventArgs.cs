// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Net;

namespace idunno.AtProto.Events
{
    /// <summary>
    /// A class holding information on why a session refresh failed.
    /// </summary>
    public sealed class SessionRefreshFailedEventArgs : EventArgs
    {
        /// <summary>
        /// Creates a new instance of <see cref="SessionRefreshFailedEventArgs"/>.
        /// </summary>
        /// <param name="sessionErrors">Any session configuration problems that caused the failure.</param>
        /// <param name="service">The <see cref="Uri"/> of the service that returned the error, if an error was returned from the API.</param>
        /// <param name="did">The <see cref="Did"/> of the user whose api call caused an error, if an error was from the API.</param>
        /// <param name="statusCode">The <see cref="HttpStatusCode"/> returned from the API, if an error was from the API.</param>
        /// <param name="error">The <see cref="AtErrorDetail" /> returned from the API, if any.</param>
        public SessionRefreshFailedEventArgs(
            SessionConfigurationErrorType sessionErrors,
            Did? did = null,
            Uri? service = null,
            HttpStatusCode? statusCode = null,
            AtErrorDetail? error = null)
        {
            SessionErrors = sessionErrors;
            Service = service;
            Did = did;
            StatusCode = statusCode;
            Error = error;
        }

        /// <summary>
        /// Gets any <see cref="SessionConfigurationErrorType"/> errors that caused the session refresh to fail."/>
        /// </summary>
        /// <value>Any <see cref="SessionConfigurationErrorType"/> errors that caused the session refresh to fail."/></value>
        public SessionConfigurationErrorType SessionErrors { get; }


        /// <summary>
        /// Gets the <see cref="Did"/> the session was created for.
        /// </summary>
        /// <value>The <see cref="Did"/> the session was created for.</value>
        public Did? Did { get; }

        /// <summary>
        /// Gets the <see cref="Uri"/> of the service the session was created on.
        /// </summary>
        /// <value>The <see cref="Uri"/> of the service the session was created on.</value>
        public Uri? Service { get; }

        /// <summary>
        /// Gets the <see cref="HttpStatusCode"/> returned from the API, if an error was from the API.
        /// </summary>
        /// <value>
        /// The <see cref="HttpStatusCode"/> returned from the API, if an error was from the API.
        /// </value>
        public HttpStatusCode? StatusCode { get; }

        /// <summary>
        /// Gets the <see cref="AtErrorDetail" /> returned from the API, if any.
        /// </summary>
        /// <value>
        ///The <see cref="AtErrorDetail" /> returned from the API, if any.
        /// </value>
        public AtErrorDetail? Error { get; }
    }
}
