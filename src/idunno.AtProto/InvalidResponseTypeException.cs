// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Runtime.Serialization;

namespace idunno.AtProto
{
    /// <summary>
    /// The exception that is thrown when a problem occurs parsing the response from an atproto server has no
    /// <see cref="System.Net.HttpResponseHeader.ContentType"/> or the MediaType does not indicate a json response.
    /// </summary>
    [Serializable]
    public class InvalidResponseTypeException : Exception
    {
        /// <summary>
        /// Creates a new instance of the <see cref="InvalidResponseTypeException"/> class for serialization.
        /// </summary>
        /// <param name="info">The data needed to serialize or deserialize.</param>
        /// <param name="context">the source and destination of serialized stream.</param>
        protected InvalidResponseTypeException(SerializationInfo info, StreamingContext context) : base()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="InvalidResponseTypeException"/> class with a specified error message.
        /// </summary>
        public InvalidResponseTypeException() : base() { }

        /// <summary>
        /// Initializes a new instance of the <see cref="InvalidResponseTypeException"/> class with a specified error message.
        /// </summary>
        /// <param name="message">The message that describes the parsing error.</param>
        public InvalidResponseTypeException(string message) : base(message) { }

        /// <summary>
        /// Initializes a new instance of the <see cref="InvalidResponseTypeException"/> class with a specified error message.
        /// </summary>
        /// <param name="message">The message that describes the parsing error.</param>
        /// <param name="inner">The exception that is the cause of the current exception</param>
        public InvalidResponseTypeException(string message, Exception inner) : base(message, inner) { }
    }
}
