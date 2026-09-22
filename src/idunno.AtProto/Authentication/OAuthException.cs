// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.


namespace idunno.AtProto.Authentication;

/// <summary>
/// The exception that is thrown when a problem occurs during OAuth.
/// </summary>
public class OAuthException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OAuthException"/> class with a specified error message.
    /// </summary>
    public OAuthException() : base() { }

    /// <summary>
    /// Initializes a new instance of the <see cref="OAuthException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the parsing error.</param>
    public OAuthException(string message) : base(message) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="OAuthException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the parsing error.</param>
    /// <param name="inner">The exception that is the cause of the current exception</param>
    public OAuthException(string message, Exception inner) : base(message, inner) { }
}