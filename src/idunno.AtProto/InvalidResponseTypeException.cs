// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

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
