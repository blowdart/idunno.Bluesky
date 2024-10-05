// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.AtProto
{
    /// <summary>
    /// The exception thrown when a string cannot be converted to an <see cref="AtIdentifier"/>.
    /// </summary>
    [Serializable]
    public class AtIdentifierException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AtIdentifierException"/> class with a specified error message.
        /// </summary>
        public AtIdentifierException() : base() { }

        /// <summary>
        /// Initializes a new instance of the <see cref="AtIdentifierException"/> class with a specified error message.
        /// </summary>
        /// <param name="message">The message that describes the parsing error.</param>
        public AtIdentifierException(string message) : base(message) { }

        /// <summary>
        /// Initializes a new instance of the <see cref="AtIdentifierException"/> class with a specified error message.
        /// </summary>
        /// <param name="message">The message that describes the parsing error.</param>
        /// <param name="inner">The exception that is the cause of the current exception</param>
        public AtIdentifierException(string message, Exception inner) : base(message, inner) { }
    }
}
