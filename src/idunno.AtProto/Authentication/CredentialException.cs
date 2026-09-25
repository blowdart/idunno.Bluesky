// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.AtProto.Authentication;

/// <summary>
/// An exception thrown when an operation that requires a particular type of exception has been passed the wrong type.
/// </summary>
public class CredentialException : AtProtoException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CredentialException"/> class with a specified error message.
    /// </summary>
    public CredentialException() : base() { }

    /// <summary>
    /// Initializes a new instance of the <see cref="CredentialException"/> class with a specified error message.
    /// </summary>
    /// <param name="credential">The credential that caused the exception.</param>
    public CredentialException(AtProtoCredential credential) : base()
    {
        Credential = credential;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CredentialException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the parsing error.</param>
    public CredentialException(string message) : base(message) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="CredentialException"/> class with a specified error message.
    /// </summary>
    /// <param name="credential">The credential that caused the exception.</param>
    /// <param name="message">The message that describes the parsing error.</param>
    public CredentialException(AtProtoCredential credential, string message) : base(message)
    {
        Credential = credential;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CredentialException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the parsing error.</param>
    /// <param name="inner">The exception that is the cause of the current exception</param>
    public CredentialException(string message, Exception inner) : base(message, inner) { }


    /// <summary>
    /// Initializes a new instance of the <see cref="CredentialException"/> class with a specified error message.
    /// </summary>
    /// <param name="credential">The credential that caused the exception.</param>
    /// <param name="message">The message that describes the parsing error.</param>
    /// <param name="inner">The exception that is the cause of the current exception</param>
    public CredentialException(AtProtoCredential credential, string message, Exception inner) : base(message, inner)
    {
        Credential = credential;
    }

    /// <summary>
    /// Gets the credential that caused the exception.
    /// </summary>
    public AtProtoCredential? Credential { get; init; }
}