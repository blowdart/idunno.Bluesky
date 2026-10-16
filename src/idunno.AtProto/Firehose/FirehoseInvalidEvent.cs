// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.AtProto.Firehose;

/// <summary>
/// Encapsulates an event which was well formed but failed validation.
/// </summary>
/// <remarks>
/// <para>An event is invalid when, for example, a content identifier does not match its data, the commit's DID does not
/// match the event, it exceeds a size limit, or its signature does not verify. The stream continues after an invalid event,
/// and its <see cref="FirehoseEvent.Sequence"/> advances the cursor, so it is not delivered again on reconnection.</para>
/// <para>Do not act on the content of an invalid event. The payload is provided for diagnostics only.</para>
/// </remarks>
public sealed record FirehoseInvalidEvent : FirehoseEvent
{
    /// <summary>
    /// Creates a new instance of <see cref="FirehoseInvalidEvent"/>.
    /// </summary>
    /// <param name="sequence">The sequence number of the event, if it has one.</param>
    /// <param name="type">The message type from the frame header.</param>
    /// <param name="did">The DID the event claims to be about, if it could be read.</param>
    /// <param name="reason">Why the event is invalid.</param>
    /// <param name="payload">The DAG-CBOR encoded payload.</param>
    internal FirehoseInvalidEvent(long? sequence, string type, Did? did, string reason, ReadOnlyMemory<byte> payload) : base(sequence)
    {
        Type = type;
        Did = did;
        Reason = reason;
        Payload = payload;
    }

    /// <summary>
    /// Gets the message type from the frame header, such as <c>#commit</c>.
    /// </summary>
    public string Type { get; }

    /// <summary>
    /// Gets the DID the event claims to be about, if it could be read.
    /// </summary>
    /// <remarks>
    /// <para>The event failed validation, so the DID is a claim rather than a fact.</para>
    /// </remarks>
    public Did? Did { get; }

    /// <summary>
    /// Gets a description of why the event is invalid.
    /// </summary>
    public string Reason { get; }

    /// <summary>
    /// Gets the DAG-CBOR encoded payload.
    /// </summary>
    public ReadOnlyMemory<byte> Payload { get; }
}
