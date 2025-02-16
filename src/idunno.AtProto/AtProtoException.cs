// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Runtime.Serialization;

namespace idunno.AtProto
{
    /// <summary>
    /// A general exception thrown by a component of the AtProto agent.
    /// </summary>
    [Serializable]
    public class AtProtoException : Exception
    {
        /// <summary>
        /// Creates a new instance of the <see cref="AtProtoException"/> class for serialization.
        /// </summary>
        /// <param name="info">The data needed to serialize or deserialize.</param>
        /// <param name="context">the source and destination of serialized stream.</param>
        protected AtProtoException(SerializationInfo info, StreamingContext context) : base()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AtProtoException"/> class with a specified error message.
        /// </summary>
        public AtProtoException() : base() { }

        /// <summary>
        /// Initializes a new instance of the <see cref="AtProtoException"/> class with a specified error message.
        /// </summary>
        /// <param name="message">The message that describes the parsing error.</param>
        public AtProtoException(string message) : base(message) { }

        /// <summary>
        /// Initializes a new instance of the <see cref="AtProtoException"/> class with a specified error message.
        /// </summary>
        /// <param name="message">The message that describes the parsing error.</param>
        /// <param name="inner">The exception that is the cause of the current exception</param>
        public AtProtoException(string message, Exception inner) : base(message, inner) { }
    }
}
