// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Net;
using System.Runtime.Serialization;

namespace idunno.AtProto
{
    /// <summary>
    /// The exception that is thrown when a problem occurs parsing abandoning an authenticated session on a service.
    /// </summary>
    [Serializable]
    public class LogoutException : Exception
    {
        /// <summary>
        /// Creates a new instance of the <see cref="LogoutException"/> class for serialization.
        /// </summary>
        /// <param name="info">The data needed to serialize or deserialize.</param>
        /// <param name="context">the source and destination of serialized stream.</param>
        protected LogoutException(SerializationInfo info, StreamingContext context) : base()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="LogoutException"/> class.
        /// </summary>
        public LogoutException() : base() { }

        /// <summary>
        /// Initializes a new instance of the <see cref="LogoutException"/> class with a specified error message.
        /// </summary>
        /// <param name="message">The message that describes the parsing error.</param>
        public LogoutException(string message)
            : base(message) { }

        /// <summary>
        /// Initializes a new instance of the <see cref="LogoutException"/> class with a specified error message.
        /// </summary>
        /// <param name="message">The message that describes the parsing error.</param>
        /// <param name="inner">The exception that is the cause of the current exception</param>
        public LogoutException(string message, Exception inner)
            : base(message, inner) { }

        /// <summary>
        /// Gets or sets The HTTP status returned when the session was abandoned.
        /// </summary>
        /// <value>
        /// The HTTP status returned when the session was abandoned.
        /// </value>
        public HttpStatusCode StatusCode { get; set; }

        /// <summary>
        /// Gets or sets the error details, if any, returned when the session was abandoned.
        /// </summary>
        /// <value>
        /// The error details, if any, returned when the session was abandoned.
        /// </value>
        public AtErrorDetail? Error { get; set; }
    }
}
