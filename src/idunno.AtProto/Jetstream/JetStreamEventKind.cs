// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.AtProto.Jetstream;

/// <summary>
/// The kind of message sent.
/// </summary>
/// <remarks>
/// <para>The converter is applied to the type rather than to the property which carries it, so a kind serialized on
/// its own is written the way the jetstream writes it. Applying it only to the property leaves the source generated
/// contract for the enum using the string enum converter, which writes "Account" where the jetstream writes
/// "account".</para>
/// </remarks>
[JsonConverter(typeof(JetStreamEventKindConverter))]
public enum JetStreamEventKind
{
    /// <summary>
    /// The message kind is unknown.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// The message is for an account event.
    /// </summary>
    Account,

    /// <summary>
    /// The message is about a commit event.
    /// </summary>
    Commit,

    /// <summary>
    /// The message is about an identity event.
    /// </summary>
    Identity,

    /// <summary>
    /// The message is about a sync event, which says the commit chain for a repo is broken and the repo should be fetched again.
    /// </summary>
    /// <remarks>
    /// <para>Only sent by <see cref="JetstreamProtocolVersion.V2"/> servers.</para>
    /// </remarks>
    Sync
}