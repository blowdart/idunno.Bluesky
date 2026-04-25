// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.AtProto.FireHose.Events;

/// <summary>
/// Encapsulates information about a message received from the firehose.
/// </summary>
/// <param name="message">The message received.</param>
/// <remarks>
/// <para>Creates a new instance of <see cref="MessageReceivedEventArgs"/>.</para>
/// </remarks>
public sealed class MessageReceivedEventArgs(byte[] message) : EventArgs
{
    /// <summary>
    /// The message received from the firehose.
    /// </summary>
    public ReadOnlyMemory<byte> Message => message;
}
