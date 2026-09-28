// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Formats.Cbor;

namespace idunno.AtProto.Firehose;

/// <summary>
/// A single event stream frame, a DAG-CBOR header followed by a DAG-CBOR payload.
/// </summary>
/// <param name="Operation">The <c>op</c> from the header.</param>
/// <param name="Type">The <c>t</c> from the header, if any.</param>
/// <param name="Payload">The encoded payload.</param>
/// <remarks>
/// <para>See https://atproto.com/specs/event-stream.</para>
/// </remarks>
internal readonly record struct FirehoseFrame(long Operation, string? Type, ReadOnlyMemory<byte> Payload)
{
    /// <summary>
    /// The operation of a regular message.
    /// </summary>
    internal const long MessageOperation = 1;

    /// <summary>
    /// The operation of an error.
    /// </summary>
    internal const long ErrorOperation = -1;

    /// <summary>
    /// Splits <paramref name="message"/> into its header and payload.
    /// </summary>
    /// <param name="message">The binary web socket message.</param>
    /// <returns>The parsed frame.</returns>
    /// <exception cref="InvalidDataException">
    /// The message is not exactly two valid DAG-CBOR values, the header is not a map with an integer <c>op</c>,
    /// or a regular message has no <c>t</c>.
    /// </exception>
    /// <remarks>
    /// <para>Invalid framing is a hard error, so any failure here must drop the connection rather than skip the frame.
    /// Unknown header fields are ignored.</para>
    /// </remarks>
    internal static FirehoseFrame Parse(ReadOnlyMemory<byte> message)
    {
        if (message.IsEmpty)
        {
            throw new InvalidDataException("The frame is empty.");
        }

        (ReadOnlyMemory<byte> header, ReadOnlyMemory<byte> payload) = FirehoseCbor.Wrap(() =>
        {
            // Each value is walked by the depth limited validator rather than read with ReadEncodedValue, which tracks every
            // nesting level it passes through and so allocates many times the size of a deeply nested frame before any depth
            // check could run.
            CborReader reader = new(message, CborConformanceMode.Canonical, allowMultipleRootLevelValues: true);
            ReadOnlyMemory<byte> encodedHeader = FirehoseCbor.ReadValidatedValue(reader, message);

            if (reader.BytesRemaining == 0)
            {
                throw new InvalidDataException("The frame has no payload.");
            }

            ReadOnlyMemory<byte> encodedPayload = FirehoseCbor.ReadValidatedValue(reader, message);

            if (reader.BytesRemaining != 0)
            {
                throw new InvalidDataException("The frame contains trailing data after its payload.");
            }

            return (encodedHeader, encodedPayload);
        });

        CborFields headerFields = FirehoseCbor.ReadFields(header);
        long operation = headerFields.GetInteger("op");
        string? type = headerFields.GetOptionalString("t");

        if (operation == MessageOperation && type is null)
        {
            throw new InvalidDataException("The frame header has no message type.");
        }

        return new FirehoseFrame(operation, type, payload);
    }
}
