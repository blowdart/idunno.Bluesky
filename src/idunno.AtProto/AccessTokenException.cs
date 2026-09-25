// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.AtProto;

/// <summary>
/// The exception that is thrown when a request is made which requires authentication but the
/// <see cref="Authentication.AccessCredentials"/> are invalid for the request.
/// </summary>
public class AccessTokenException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AccessTokenException"/> class.
    /// </summary>
    public AccessTokenException() : base() { }

    /// <summary>
    /// Initializes a new instance of the <see cref="AccessTokenException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the parsing error.</param>
    public AccessTokenException(string message)
        : base(message) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="AccessTokenException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the parsing error.</param>
    /// <param name="inner">The exception that is the cause of the current exception</param>
    public AccessTokenException(string message, Exception inner)
        : base(message, inner) { }
}