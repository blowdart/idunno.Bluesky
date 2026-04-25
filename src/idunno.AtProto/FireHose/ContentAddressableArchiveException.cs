// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.AtProto.FireHose;

/// <summary>
/// Thrown when a parsing error is encountered when parsing a CAR file.
/// </summary>
public sealed class ContentAddressableArchiveException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ContentAddressableArchiveException"/> class.
    /// </summary>
    public ContentAddressableArchiveException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ContentAddressableArchiveException"/> class.
    /// </summary>
    /// <param name="message">The message that describes the parsing error.</param>
    public ContentAddressableArchiveException(string message) : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ContentAddressableArchiveException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the parsing error.</param>
    /// <param name="inner">The exception that is the cause of the current exception</param>
    public ContentAddressableArchiveException(string message, Exception inner) : base(message, inner)
    {
    }
}
