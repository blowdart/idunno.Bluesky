// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.AtProto
{
    /// <summary>
    /// The exception that is thrown when a the expected record type returned by a record request does not match expectation.
    /// </summary>
    [Serializable]
    public class RecordException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="InvalidResponseTypeException"/> class with a specified error message.
        /// </summary>
        public RecordException() : base() { }

        /// <summary>
        /// Initializes a new instance of the <see cref="InvalidResponseTypeException"/> class with a specified error message.
        /// </summary>
        /// <param name="message">The message that describes the parsing error.</param>
        public RecordException(string message) : base(message) { }

        /// <summary>
        /// Initializes a new instance of the <see cref="InvalidResponseTypeException"/> class with a specified error message.
        /// </summary>
        /// <param name="message">The message that describes the parsing error.</param>
        /// <param name="inner">The exception that is the cause of the current exception</param>
        public RecordException(string message, Exception inner) : base(message, inner) { }
    }
}
