// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.AtProto.FireHose;

/// <summary>
/// Encapsulates the raw payload of a message, passed back when the frame type is unknown.
/// The raw message includes both the header and the body.
/// </summary>
public record UnknownPayload : IFramePayload
{
    /// <summary>
    /// Creates a new instance of <see cref="UnknownPayload"/> with the specified raw message.
    /// </summary>
    /// <param name="rawMessage">The message, as bytes.</param>
    /// <exception cref="System.ArgumentNullException">Thrown when <paramref name="rawMessage"/> is <see langword="null" />.</exception>
    public UnknownPayload(byte[] rawMessage)
    {
        ArgumentNullException.ThrowIfNull(rawMessage);

        RawMessage = Array.AsReadOnly(rawMessage);
    }

    /// <summary>
    /// Gets the raw message as a list of bytes.
    /// </summary>
    public IReadOnlyList<byte> RawMessage { get; }
}
