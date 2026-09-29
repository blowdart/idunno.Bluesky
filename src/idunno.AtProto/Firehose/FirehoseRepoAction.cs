// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.AtProto.Firehose;

/// <summary>
/// The action a <see cref="FirehoseRepoOperation"/> performs on a record.
/// </summary>
public enum FirehoseRepoAction
{
    /// <summary>
    /// The record was created.
    /// </summary>
    Create,

    /// <summary>
    /// The record was updated.
    /// </summary>
    Update,

    /// <summary>
    /// The record was deleted.
    /// </summary>
    Delete,

    /// <summary>
    /// The action is not one this library recognizes.
    /// </summary>
    /// <remarks>
    /// <para>The set of actions is an open list decided by the protocol, so an action added upstream is surfaced as
    /// <see cref="Unknown"/> rather than causing the event to fail.</para>
    /// </remarks>
    Unknown
}
