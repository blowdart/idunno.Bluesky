// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.AtProto.Firehose;

/// <summary>
/// Encapsulates an event with a message type this library does not recognize.
/// </summary>
/// <remarks>
/// <para>New message types can be added to an event stream at any time, so they are surfaced rather than ending the stream.
/// The payload has been checked to be valid DAG-CBOR but is otherwise untrusted.</para>
/// </remarks>
public sealed record FirehoseUnknownEvent : FirehoseEvent
{
    /// <summary>
    /// Creates a new instance of <see cref="FirehoseUnknownEvent"/>.
    /// </summary>
    /// <param name="type">The message type from the frame header.</param>
    /// <param name="payload">The DAG-CBOR encoded payload.</param>
    internal FirehoseUnknownEvent(string type, ReadOnlyMemory<byte> payload) : base((long?)null)
    {
        Type = type;
        Payload = payload;
    }

    /// <summary>
    /// Gets the message type from the frame header, such as <c>#example</c>.
    /// </summary>
    /// <remarks>
    /// <para>The type comes from the server and is untrusted. It is only lightly sanitized, stripped of control and bidirectional
    /// formatting characters and truncated, and must not be treated as trusted input.</para>
    /// </remarks>
    public string Type { get; }

    /// <summary>
    /// Gets the DAG-CBOR encoded payload.
    /// </summary>
    /// <remarks>
    /// <para>Use <see cref="DagCbor.ToJsonElement(ReadOnlyMemory{byte})"/> to inspect it.</para>
    /// </remarks>
    public ReadOnlyMemory<byte> Payload { get; }
}
