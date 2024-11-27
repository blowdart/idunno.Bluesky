// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Runtime.Serialization;

namespace idunno.AtProto
{
    /// <summary>
    /// The exception thrown when a security token validation error occurs.
    /// </summary>
    [Serializable]
    public class SecurityTokenValidationException : Exception
    {
        /// <summary>
        /// Creates a new instance of the <see cref="SecurityTokenValidationException"/> class for serialization.
        /// </summary>
        /// <param name="info">The data needed to serialize or deserialize.</param>
        /// <param name="context">the source and destination of serialized stream.</param>
        protected SecurityTokenValidationException(SerializationInfo info, StreamingContext context) : base()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SecurityTokenValidationException"/> class.
        /// </summary>
        public SecurityTokenValidationException() : base() { }

        /// <summary>
        /// Initializes a new instance of the <see cref="SecurityTokenValidationException"/> class with a specified error message.
        /// </summary>
        /// <param name="message">The message that describes the parsing error.</param>
        public SecurityTokenValidationException(string message) : base(message) { }

        /// <summary>
        /// Initializes a new instance of the <see cref="SecurityTokenValidationException"/> class with a specified error message
        /// and inner exception.
        /// </summary>
        /// <param name="message">The message that describes the parsing error.</param>
        /// <param name="inner">The exception that is the cause of the current exception</param>
        public SecurityTokenValidationException(string message, Exception inner) : base(message, inner) { }
    }
}
