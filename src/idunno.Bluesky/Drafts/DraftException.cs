// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Runtime.Serialization;

namespace idunno.Bluesky.Drafts
{
    /// <summary>
    /// The exception that is thrown when a draft cannot be converted to a post.
    /// </summary>
    [Serializable]
    public class DraftException : Exception
    {
        /// <summary>
        /// Creates a new instance of the <see cref="DraftException"/> class for serialization.
        /// </summary>
        /// <param name="info">The data needed to serialize or deserialize.</param>
        /// <param name="context">the source and destination of serialized stream.</param>
        protected DraftException(SerializationInfo info, StreamingContext context) : base()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DraftException"/> class with a specified error message.
        /// </summary>
        public DraftException() : base() { }

        /// <summary>
        /// Initializes a new instance of the <see cref="DraftException"/> class with a specified error message.
        /// </summary>
        /// <param name="message">The message that describes the parsing error.</param>
        public DraftException(string message) : base(message) { }

        /// <summary>
        /// Initializes a new instance of the <see cref="DraftException"/> class with a specified error message.
        /// </summary>
        /// <param name="message">The message that describes the parsing error.</param>
        /// <param name="inner">The exception that is the cause of the current exception</param>
        public DraftException(string message, Exception inner) : base(message, inner) { }
    }
}
