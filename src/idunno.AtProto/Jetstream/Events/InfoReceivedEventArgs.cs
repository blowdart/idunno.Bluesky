// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.AtProto.Jetstream.Events;

/// <summary>
/// Encapsulates an advisory notice sent by a jetstream server.
/// </summary>
/// <param name="name">The name of the notice, for example <c>OutdatedCursor</c>.</param>
/// <param name="message">The message, if any, which accompanied the notice.</param>
/// <remarks>
/// <para>Notices are only sent by <see cref="JetstreamProtocolVersion.V2"/> servers. They do not end the connection.</para>
/// </remarks>
public class InfoReceivedEventArgs(string name, string? message) : EventArgs
{
    /// <summary>
    /// Gets the name of the notice.
    /// </summary>
    public string Name { get; } = name;

    /// <summary>
    /// Gets the message, if any, which accompanied the notice.
    /// </summary>
    public string? Message { get; } = message;
}