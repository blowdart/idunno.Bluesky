// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

#pragma warning disable CS0618 // The event argument preserves its original public property type.

namespace idunno.AtProto.Jetstream.Events;

/// <summary>
/// Contains the results of parsing a message from the Jetstream.
/// </summary>
/// <param name="parsedEvent">The parsed event.</param>
public sealed class RecordReceivedEventArgs(AtJetstreamEvent parsedEvent) : EventArgs
{
    /// <summary>
    /// Gets the message that trigged the event, parsed as its json object.
    /// </summary>
    public AtJetstreamEvent ParsedEvent { get; } = parsedEvent;
}

#pragma warning restore CS0618