// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;

namespace idunno.Bluesky;

/// <summary>
/// The exception that is thrown when a handle cannot be resolved to a DID.
/// </summary>
public class HandleResolutionException : Exception
{
    private readonly Handle? _handle;

    /// <summary>
    /// Initializes a new instance of the <see cref="HandleResolutionException"/> class with a specified error message.
    /// </summary>
    public HandleResolutionException() : base() { }

    /// <summary>
    /// Initializes a new instance of the <see cref="HandleResolutionException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the parsing error.</param>
    public HandleResolutionException(string message) : base(message) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="HandleResolutionException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the parsing error.</param>
    /// <param name="handle">The handle that could not be resolved.</param>
    public HandleResolutionException(string message, Handle? handle) : base(message)
    {
        _handle = handle;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="HandleResolutionException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the parsing error.</param>
    /// <param name="inner">The exception that is the cause of the current exception</param>
    public HandleResolutionException(string message, Exception inner) : base(message, inner) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="HandleResolutionException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the parsing error.</param>
    /// <param name="handle">The handle that could not be resolved.</param>
    /// <param name="inner">The exception that is the cause of the current exception</param>
    public HandleResolutionException(string message, Handle? handle, Exception inner) : base(message, inner)
    {
        _handle = handle;
    }

    /// <summary>
    /// Gets the handle that could not be resolved.
    /// </summary>
    public Handle? Handle { get => _handle; }
}