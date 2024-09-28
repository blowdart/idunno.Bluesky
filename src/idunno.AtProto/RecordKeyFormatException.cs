// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.AtProto
{
    /// <summary>
    /// The exception that is thrown when the record key in an AT URI is not valid.
    /// </summary>
    [Serializable]
    public class RecordKeyFormatException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="InvalidResponseTypeException"/> class with a specified error message.
        /// </summary>
        public RecordKeyFormatException() : base() { }

        /// <summary>
        /// Initializes a new instance of the <see cref="InvalidResponseTypeException"/> class with a specified error message.
        /// </summary>
        /// <param name="message">The message that describes the parsing error.</param>
        public RecordKeyFormatException(string message) : base(message) { }

        /// <summary>
        /// Initializes a new instance of the <see cref="InvalidResponseTypeException"/> class with a specified error message.
        /// </summary>
        /// <param name="message">The message that describes the parsing error.</param>
        /// <param name="inner">The exception that is the cause of the current exception</param>
        public RecordKeyFormatException(string message, Exception inner) : base(message, inner) { }
    }
}
