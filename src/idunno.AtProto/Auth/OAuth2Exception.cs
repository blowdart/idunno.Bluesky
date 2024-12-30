// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Runtime.Serialization;

namespace idunno.AtProto.Auth
{
    /// <summary>
    /// The exception that is thrown when a problem occurs during OAuth.
    /// </summary>
    [Serializable]
    public class OAuth2Exception : Exception
    {
        /// <summary>
        /// Creates a new instance of the <see cref="OAuth2Exception"/> class for serialization.
        /// </summary>
        /// <param name="info">The data needed to serialize or deserialize.</param>
        /// <param name="context">the source and destination of serialized stream.</param>
        protected OAuth2Exception(SerializationInfo info, StreamingContext context) : base()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="OAuth2Exception"/> class with a specified error message.
        /// </summary>
        public OAuth2Exception() : base() { }

        /// <summary>
        /// Initializes a new instance of the <see cref="OAuth2Exception"/> class with a specified error message.
        /// </summary>
        /// <param name="message">The message that describes the parsing error.</param>
        public OAuth2Exception(string message) : base(message) { }

        /// <summary>
        /// Initializes a new instance of the <see cref="OAuth2Exception"/> class with a specified error message.
        /// </summary>
        /// <param name="message">The message that describes the parsing error.</param>
        /// <param name="inner">The exception that is the cause of the current exception</param>
        public OAuth2Exception(string message, Exception inner) : base(message, inner) { }
    }
}
