// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Net;
using System.Runtime.Serialization;

namespace idunno.AtProto
{
    /// <summary>
    /// Thrown when the Session restoration process fails.
    /// </summary>
    [Serializable]
    public class SessionRestorationFailedException : Exception
    {
        /// <summary>
        /// Creates a new instance of the <see cref="SessionRestorationFailedException"/> class for serialization.
        /// </summary>
        /// <param name="info">The data needed to serialize or deserialize.</param>
        /// <param name="context">the source and destination of serialized stream.</param>
        protected SessionRestorationFailedException(SerializationInfo info, StreamingContext context) : base()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="InvalidSessionException"/> class.
        /// </summary>
        public SessionRestorationFailedException() : base() { }

        /// <summary>
        /// Initializes a new instance of the <see cref="InvalidSessionException"/> class with a specified error message.
        /// </summary>
        /// <param name="message">The message that describes the parsing error.</param>
        public SessionRestorationFailedException(string message) : base(message) { }

        /// <summary>
        /// Initializes a new instance of the <see cref="InvalidSessionException"/> class with a specified error message
        /// and inner exception.
        /// </summary>
        /// <param name="message">The message that describes the parsing error.</param>
        /// <param name="inner">The exception that is the cause of the current exception</param>
        public SessionRestorationFailedException(string message, Exception inner) : base(message, inner) { }

        /// <summary>
        /// The <see cref="HttpStatusCode"/> returned by the API call.
        /// </summary>
        public HttpStatusCode StatusCode { get; set; }

        /// <summary>
        /// The <see cref="AtErrorDetail"/> returned by the API call, if any.
        /// </summary>

        public AtErrorDetail? Error { get; set; }
    }
}
