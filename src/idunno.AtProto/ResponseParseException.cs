﻿// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Runtime.Serialization;

namespace idunno.AtProto
{
    /// <summary>
    /// The exception thrown when a problem occurs parsing a response from an atproto server.
    /// </summary>
    [Serializable]
    public class ResponseParseException : Exception
    {
        /// <summary>
        /// Creates a new instance of the <see cref="ResponseParseException"/> class for serialization.
        /// </summary>
        /// <param name="info">The data needed to serialize or deserialize.</param>
        /// <param name="context">the source and destination of serialized stream.</param>
        protected ResponseParseException(SerializationInfo info, StreamingContext context) : base()
        {
        }

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
