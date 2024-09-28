// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Net;

namespace idunno.AtProto
{
    public class SessionRestorationFailedException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="InvalidSessionException"/> class.
        /// </summary>
        public SessionRestorationFailedException() : base() { }

        /// <summary>
        /// Initializes a new instance of the <see cref="InvalidSessionException"/> class with a specified error message.
        /// </summary>
        /// <param name="message">The message that describes the parsing error.</param>
        public SessionRestorationFailedException(string message) : base(message) { }

        /// <summary>
        /// Initializes a new instance of the <see cref="InvalidSessionException"/> class with a specified error message
        /// and inner exception.
        /// </summary>
        /// <param name="message">The message that describes the parsing error.</param>
        /// <param name="inner">The exception that is the cause of the current exception</param>
        public SessionRestorationFailedException(string message, Exception inner) : base(message, inner) { }

        public HttpStatusCode StatusCode { get; set; }

        public AtErrorDetail? Error { get; set; }
    }
}
