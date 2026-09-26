// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.AtProto.Jetstream.Events;

/// <summary>
/// Encapsulates information given when a fault occurs in jetstream processing.
/// </summary>
/// <param name="fault">The fault message.</param>
public class FaultRaisedEventArgs(string fault) : EventArgs
{
    /// <summary>
    /// The message received from the jetstream.
    /// </summary>
    public string Fault { get; } = fault;

    /// <summary>
    /// Gets the name of the error the jetstream server sent, if the fault was caused by one.
    /// </summary>
    /// <remarks>
    /// <para>A <see cref="JetstreamProtocolVersion.V2"/> server sends a named error, such as <c>ConsumerTooSlow</c>,
    /// immediately before it closes the connection.</para>
    /// </remarks>
    public string? Error { get; init; }
}