// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Runtime.Serialization;

namespace idunno.AtProto
{
    /// <summary>
    /// The exception that is thrown when AtUri validation fails.
    /// </summary>
    [Serializable]
    public class AtUriFormatException : Exception
    {
        /// <summary>
        /// Creates a new instance of the <see cref="AtUriFormatException"/> class for serialization.
        /// </summary>
        /// <param name="info">The data needed to serialize or deserialize.</param>
        /// <param name="context">the source and destination of serialized stream.</param>
        protected AtUriFormatException(SerializationInfo info, StreamingContext context) : base()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="NsidFormatException"/> class with a specified error message.
        /// </summary>
        public AtUriFormatException() : base() { }

        /// <summary>
        /// Initializes a new instance of the <see cref="NsidFormatException"/> class with a specified error message.
        /// </summary>
        /// <param name="message">The message that describes the parsing error.</param>
        public AtUriFormatException(string message) : base(message) { }

        /// <summary>
        /// Initializes a new instance of the <see cref="NsidFormatException"/> class with a specified error message.
        /// </summary>
        /// <param name="message">The message that describes the parsing error.</param>
        /// <param name="inner">The exception that is the cause of the current exception</param>
        public AtUriFormatException(string message, Exception inner) : base(message, inner) { }
    }
}
