// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.AtProto
{
    /// <summary>
    /// The exception that is thrown when a problem occurs parsing a response from an atproto server.
    /// </summary>
    [Serializable]
    public class ResponseParseException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ResponseParseException"/> class.
        /// </summary>
        public ResponseParseException() : base() { }

        /// <summary>
        /// Initializes a new instance of the <see cref="ResponseParseException"/> class with a specified error message.
        /// </summary>
        /// <param name="message">The message that describes the parsing error.</param>
        public ResponseParseException(string message)
            : base(message) { }

        /// <summary>
        /// Initializes a new instance of the <see cref="ResponseParseException"/> class with a specified error message.
        /// </summary>
        /// <param name="message">The message that describes the parsing error.</param>
        /// <param name="inner">The exception that is the cause of the current exception</param>
        public ResponseParseException(string message, Exception inner)
            : base(message, inner) { }
    }
}
