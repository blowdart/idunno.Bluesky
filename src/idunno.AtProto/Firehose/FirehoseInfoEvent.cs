// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.AtProto.Firehose;

/// <summary>
/// Encapsulates an <c>#info</c> event, an informational message from the server which is not sequenced.
/// </summary>
/// <remarks>
/// <para>A server sends an <c>OutdatedCursor</c> info event when the requested cursor is older than the events it retains,
/// then streams from the oldest event it has. Events between the cursor and that point have been missed.</para>
/// </remarks>
public sealed record FirehoseInfoEvent : FirehoseEvent
{
    /// <summary>
    /// The name of the info event a server sends when a cursor is older than the events it retains.
    /// </summary>
    public const string OutdatedCursor = "OutdatedCursor";

    /// <summary>
    /// Creates a new instance of <see cref="FirehoseInfoEvent"/>.
    /// </summary>
    /// <param name="name">The name of the message.</param>
    /// <param name="message">The human readable message, if any.</param>
    internal FirehoseInfoEvent(string name, string? message) : base((long?)null)
    {
        Name = name;
        Message = message;
    }

    /// <summary>
    /// Gets the name of the message, such as <see cref="OutdatedCursor"/>.
    /// </summary>
    /// <remarks>
    /// <para>The name comes from the server and is untrusted. It is only lightly sanitized, stripped of control and bidirectional
    /// formatting characters and truncated, so compare it against known names rather than displaying or acting on it directly.</para>
    /// </remarks>
    public string Name { get; }

    /// <summary>
    /// Gets the human readable message the server sent, if any.
    /// </summary>
    /// <remarks>
    /// <para>The message comes from the server and is untrusted. It is only lightly sanitized, stripped of control and bidirectional
    /// formatting characters and truncated, and must not be treated as trusted input. Encode it for the context it is used in, such as HTML.</para>
    /// </remarks>
    public string? Message { get; }
}
