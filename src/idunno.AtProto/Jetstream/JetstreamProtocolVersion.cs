// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;

namespace idunno.AtProto.Jetstream;

/// <summary>
/// The version of the jetstream protocol to use when connecting to a jetstream server.
/// </summary>
[SuppressMessage("Design", "CA1008:Enums should have zero value", Justification = "The values are protocol version numbers, and there is no version zero.")]
public enum JetstreamProtocolVersion
{
    /// <summary>
    /// The original jetstream protocol, served from <c>/subscribe</c>.
    /// </summary>
    /// <remarks>
    /// <para>Version 1 servers accept filter changes on an open connection, but do not send sequence numbers or <see cref="JetStreamEventKind.Sync"/> events.</para>
    /// </remarks>
    V1 = 1,

    /// <summary>
    /// The jetstream protocol served from <c>/xrpc/network.bsky.jetstream.subscribeEvents</c>.
    /// </summary>
    /// <remarks>
    /// <para>Version 2 servers send a sequence number with every event, which is used as the cursor, can filter by
    /// event kind, and send <see cref="JetStreamEventKind.Sync"/> events. Filters can only be set when connecting, so
    /// changing one on an open connection reconnects.</para>
    /// </remarks>
    V2 = 2
}