// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.AtProto
{
    /// <summary>
    /// The exception that is thrown when a request is made which requires authentication but the
    /// <see cref="Agent"/> is unauthenticated.
    /// </summary>
    [Serializable]
    public class AuthenticatedSessionRequiredException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AuthenticatedSessionRequiredException"/> class.
        /// </summary>
        public AuthenticatedSessionRequiredException() : base() { }

        /// <summary>
        /// Initializes a new instance of the <see cref="AuthenticatedSessionRequiredException"/> class with a specified error message.
        /// </summary>
        /// <param name="message">The message that describes the parsing error.</param>
        public AuthenticatedSessionRequiredException(string message)
            : base(message) { }

        /// <summary>
        /// Initializes a new instance of the <see cref="AuthenticatedSessionRequiredException"/> class with a specified error message.
        /// </summary>
        /// <param name="message">The message that describes the parsing error.</param>
        /// <param name="inner">The exception that is the cause of the current exception</param>
        public AuthenticatedSessionRequiredException(string message, Exception inner)
            : base(message, inner) { }
    }
}
