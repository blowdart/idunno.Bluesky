// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.AtProto.Jetstream;

/// <summary>
/// The kind of change a jetstream commit event describes.
/// </summary>
[JsonConverter(typeof(JetstreamCommitOperationConverter))]
public enum JetstreamCommitOperation
{
    /// <summary>
    /// The operation is unknown.
    /// </summary>
    /// <remarks>
    /// <para>The set of operations a jetstream emits is decided by the server, not by this library, so an operation
    /// added upstream is reported as unknown rather than making the event it arrived on unreadable.</para>
    /// </remarks>
    Unknown = 0,

    /// <summary>
    /// A record was created.
    /// </summary>
    Create,

    /// <summary>
    /// A record was updated.
    /// </summary>
    Update,

    /// <summary>
    /// A record was deleted.
    /// </summary>
    Delete
}
