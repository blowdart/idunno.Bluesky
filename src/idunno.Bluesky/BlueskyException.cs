// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.Bluesky;

/// <summary>
/// A general exception thrown by a component of the Bluesky agent.
/// </summary>
public class BlueskyException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BlueskyException"/> class with a specified error message.
    /// </summary>
    public BlueskyException() : base() { }

    /// <summary>
    /// Initializes a new instance of the <see cref="BlueskyException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the parsing error.</param>
    public BlueskyException(string message) : base(message) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="BlueskyException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the parsing error.</param>
    /// <param name="inner">The exception that is the cause of the current exception</param>
    public BlueskyException(string message, Exception inner) : base(message, inner) { }
}