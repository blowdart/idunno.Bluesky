// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.AtProto.FireHose.Events;

/// <summary>
/// Raised when a repo message has been parsed.
/// </summary>
/// <param name="header">The header of the repo message.</param>
/// <param name="framePayload">The payload of the repo message.</param>
public class RepoMessageReceivedEventArgs(FrameHeader header, IFramePayload? framePayload) : EventArgs
{
    /// <summary>
    /// The decoded header for the message.
    /// </summary>
    public FrameHeader Header => header;

    /// <summary>
    /// The decoded payload for the message.
    /// </summary>
    public IFramePayload? Payload => framePayload;
}
