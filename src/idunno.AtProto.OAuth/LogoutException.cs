// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Runtime.Serialization;

namespace idunno.AtProto.OAuth
{
    /// <summary>
    /// The exception when the CallbackServer encounters problems.
    /// </summary>
    [Serializable]
    public class CallbackServerException : Exception
    {
        /// <summary>
        /// Creates a new instance of the <see cref="CallbackServerException"/> class for serialization.
        /// </summary>
        /// <param name="info">The data needed to serialize or deserialize.</param>
        /// <param name="context">the source and destination of serialized stream.</param>
        protected CallbackServerException(SerializationInfo info, StreamingContext context) : base()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CallbackServerException"/> class.
        /// </summary>
        public CallbackServerException() : base() { }

        /// <summary>
        /// Initializes a new instance of the <see cref="CallbackServerException"/> class with a specified error message.
        /// </summary>
        /// <param name="message">The message that describes the parsing error.</param>
        public CallbackServerException(string message)
            : base(message) { }

        /// <summary>
        /// Initializes a new instance of the <see cref="CallbackServerException"/> class with a specified error message.
        /// </summary>
        /// <param name="message">The message that describes the parsing error.</param>
        /// <param name="inner">The exception that is the cause of the current exception</param>
        public CallbackServerException(string message, Exception inner)
            : base(message, inner) { }
    }
}
