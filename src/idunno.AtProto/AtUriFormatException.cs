// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.AtProto
{
    /// <summary>
    /// The exception that is thrown when AtUri validation fails.
    /// </summary>
    [Serializable]
    public class AtUriFormatException : Exception
    {
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
