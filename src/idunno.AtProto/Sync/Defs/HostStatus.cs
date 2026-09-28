// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace idunno.AtProto.Sync;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// The status of an upstream host, as seen by a relay.
/// </summary>
[JsonConverter(typeof(HostStatusConverter))]
public enum HostStatus
{
    /// <summary>
    /// The host is active.
    /// </summary>
    Active,

    /// <summary>
    /// The host is idle.
    /// </summary>
    Idle,

    /// <summary>
    /// The host is offline.
    /// </summary>
    Offline,

    /// <summary>
    /// The host has been throttled.
    /// </summary>
    Throttled,

    /// <summary>
    /// The host has been banned.
    /// </summary>
    Banned,

    /// <summary>
    /// The host status is not one this library recognizes.
    /// </summary>
    /// <remarks>
    /// <para>The set of host statuses is decided by the service, not by this library, so a status added
    /// upstream is surfaced as <see cref="Unknown"/> rather than causing the response to fail to deserialize.</para>
    /// </remarks>
    Unknown
}
