// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Net;
using System.Runtime.Serialization;

namespace idunno.AtProto
{
    /// <summary>
    /// The exception thrown when a problem occurs when making a request in the <see cref="AtProtoHttpClient{TResult}"/>.
    /// </summary>
    [Serializable]
    public class AtProtoHttpRequestException : HttpRequestException
    {
        /// <summary>
        /// Creates a new instance of the <see cref="AtProtoHttpRequestException"/> class for serialization.
        /// </summary>
        /// <param name="info">The data needed to serialize or deserialize.</param>
        /// <param name="context">the source and destination of serialized stream.</param>
        protected AtProtoHttpRequestException(SerializationInfo info, StreamingContext context) : base()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AtProtoHttpRequestException" /> class.
        /// </summary>
        public AtProtoHttpRequestException() : this(null, null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AtProtoHttpRequestException" /> class with a specific message that describes the current exception.
        /// </summary>
        /// <param name="message">A message that describes the current exception.</param>
        public AtProtoHttpRequestException(string? message) : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AtProtoHttpRequestException"/> class with a specific message that describes the current exception and an inner exception.
        /// </summary>
        /// <param name="message">A message that describes the current exception.</param>
        /// <param name="innerException">The inner exception.</param>
        public AtProtoHttpRequestException(string? message, Exception? innerException) : base(message, innerException)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AtProtoHttpRequestException"/> class with a specific message that describes the current exception, an inner exception amd n, and an HTTP status code.
        /// </summary>
        /// <param name="message">A message that describes the current exception.</param>
        /// <param name="innerException">The inner exception.</param>
        /// <param name="statusCode">The HTTP status code.</param>
        public AtProtoHttpRequestException(string? message, Exception? innerException, HttpStatusCode? statusCode) : base(message, innerException, statusCode)
        {
        }
    }
}
