// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace idunno.AtProto.Sync;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// The reason a repository is not active on a host.
/// </summary>
[JsonConverter(typeof(RepoStatusConverter))]
public enum RepoStatus
{
    /// <summary>
    /// The repository has been taken down.
    /// </summary>
    Takendown,

    /// <summary>
    /// The repository is suspended.
    /// </summary>
    Suspended,

    /// <summary>
    /// The repository has been deleted.
    /// </summary>
    Deleted,

    /// <summary>
    /// The repository has been deactivated.
    /// </summary>
    Deactivated,

    /// <summary>
    /// The repository is desynchronized.
    /// </summary>
    Desynchronized,

    /// <summary>
    /// The repository has been throttled.
    /// </summary>
    Throttled,

    /// <summary>
    /// The repository status is not one this library recognizes.
    /// </summary>
    /// <remarks>
    /// <para>The set of repository statuses is decided by the service, not by this library, so a status added
    /// upstream is surfaced as <see cref="Unknown"/> rather than causing the response to fail to deserialize.</para>
    /// </remarks>
    Unknown
}
