// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Net;

namespace idunno.AtProto;

/// <summary>
/// WebSocket options for the Jetstream and Firehose.
/// </summary>
public sealed record WebSocketOptions
{
    /// <summary>
    /// Gets the proxy for WebSocket requests.
    /// </summary>
    public IWebProxy? Proxy { get; init; }

    /// <summary>
    /// Gets the WebSocket protocol keep-alive interval.
    /// </summary>
    /// <remarks>
    /// <para>A keep-alive interval on its own only sends pings, it does not act on a peer which never answers one. Set
    /// <see cref="KeepAliveTimeout"/> as well for an unresponsive peer to be detected.</para>
    /// </remarks>
    public TimeSpan? KeepAliveInterval { get; init; }

    /// <summary>
    /// Gets how long to wait for a peer to answer a keep-alive ping before the connection is aborted.
    /// </summary>
    /// <remarks>
    /// <para>Without this a connection lost to a network failure, rather than to a close either end performed, leaves the
    /// socket reporting itself as open and a read on it waiting for data which is never going to arrive. Nothing tells a
    /// consumer the connection needs remaking, so it waits alongside it.</para>
    /// <para>This is ignored when running on .NET 8, whose <see cref="System.Net.WebSockets.ClientWebSocketOptions"/> has
    /// no keep-alive timeout to apply it to.</para>
    /// </remarks>
    public TimeSpan? KeepAliveTimeout { get; init; }
}