// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.Bluesky
{
    /// <summary>
    /// Thrown when a PostBuilder has an internal consistency problem.
    /// </summary>
    public class PostBuilderException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PostBuilderException"/> class.
        /// </summary>
        public PostBuilderException() : base() { }

        /// <summary>
        /// Initializes a new instance of the <see cref="PostBuilderException"/> class with a specified error message.
        /// </summary>
        /// <param name="message">The message that describes the parsing error.</param>
        public PostBuilderException(string message) : base(message) { }

        /// <summary>
        /// Initializes a new instance of the <see cref="PostBuilderException"/> class with a specified error message
        /// and inner exception.
        /// </summary>
        /// <param name="message">The message that describes the parsing error.</param>
        /// <param name="inner">The exception that is the cause of the current exception</param>
        public PostBuilderException(string message, Exception inner) : base(message, inner) { }
    }
}
