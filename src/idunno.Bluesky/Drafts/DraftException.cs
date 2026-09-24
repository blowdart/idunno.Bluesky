// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.Bluesky.Drafts;

/// <summary>
/// The exception that is thrown when a draft cannot be converted to a post.
/// </summary>
public class DraftException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DraftException"/> class with a specified error message.
    /// </summary>
    public DraftException() : base() { }

    /// <summary>
    /// Initializes a new instance of the <see cref="DraftException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the parsing error.</param>
    public DraftException(string message) : base(message) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="DraftException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the parsing error.</param>
    /// <param name="inner">The exception that is the cause of the current exception</param>
    public DraftException(string message, Exception inner) : base(message, inner) { }
}