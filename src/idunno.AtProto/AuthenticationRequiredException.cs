// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.


namespace idunno.AtProto;

/// <summary>
/// The exception that is thrown when a request is made which requires authentication but the
/// <see cref="Agent"/> is unauthenticated.
/// </summary>
public class AuthenticationRequiredException : AtProtoException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AuthenticationRequiredException"/> class.
    /// </summary>
    public AuthenticationRequiredException() : base() { }

    /// <summary>
    /// Initializes a new instance of the <see cref="AuthenticationRequiredException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the parsing error.</param>
    public AuthenticationRequiredException(string message)
        : base(message) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="AuthenticationRequiredException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the parsing error.</param>
    /// <param name="inner">The exception that is the cause of the current exception</param>
    public AuthenticationRequiredException(string message, Exception inner)
        : base(message, inner) { }
}