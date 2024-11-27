// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Runtime.Serialization;

namespace idunno.AtProto
{
    /// <summary>
    /// The exception thrown when there is problem with the current session.
    /// </summary>
    [Serializable]
    public class InvalidSessionException : Exception
    {
        /// <summary>
        /// Creates a new instance of the <see cref="InvalidSessionException"/> class for serialization.
        /// </summary>
        /// <param name="info">The data needed to serialize or deserialize.</param>
        /// <param name="context">the source and destination of serialized stream.</param>
        protected InvalidSessionException(SerializationInfo info, StreamingContext context) : base()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="InvalidSessionException"/> class.
        /// </summary>
        public InvalidSessionException() : base() { }

        /// <summary>
        /// Initializes a new instance of the <see cref="InvalidSessionException"/> class with a specified error message.
        /// </summary>
        /// <param name="message">The message that describes the parsing error.</param>
        public InvalidSessionException(string message) : base(message) { }

        /// <summary>
        /// Initializes a new instance of the <see cref="InvalidSessionException"/> class with a specified error message
        /// and inner exception.
        /// </summary>
        /// <param name="message">The message that describes the parsing error.</param>
        /// <param name="inner">The exception that is the cause of the current exception</param>
        public InvalidSessionException(string message, Exception inner) : base(message, inner) { }

        /// <summary>
        /// Gets or sets the session configuration errors that caused the exception.
        /// </summary>
        public SessionConfigurationErrorType SessionErrors { get; set; } = SessionConfigurationErrorType.None;
    }

    /// <summary>
    /// Flag values indicating the type or types of session configuration errors that caused the exception
    /// </summary>
    [Flags]
    public enum SessionConfigurationErrorType
    {
        /// <summary>
        /// No session errors were found.
        /// </summary>
        None = 0,

        /// <summary>
        /// The session is null
        /// </summary>
        NullSession = 1,

        /// <summary>
        /// The session is missing the <see cref="Did"/> it belongs to.
        /// </summary>
        MissingDid = 2,

        /// <summary>
        /// The session is missing the <see cref="Uri"/> of the service it was created on.
        /// </summary>
        MissingService = 4,

        /// <summary>
        /// The session is missing an access token.
        /// </summary>
        MissingAccessToken = 8,

        /// <summary>
        /// The session is missing a refresh token.
        /// </summary>
        MissingRefreshToken = 16
    }
}
