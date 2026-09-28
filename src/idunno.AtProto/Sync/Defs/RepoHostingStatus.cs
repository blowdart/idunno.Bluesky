// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace idunno.AtProto.Sync;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// The hosting status of a repository, as reported by a personal data server or relay.
/// </summary>
public sealed record RepoHostingStatus
{
    [JsonConstructor]
    internal RepoHostingStatus(Did did, bool active, RepoStatus? status, string? rev)
    {
        ArgumentNullException.ThrowIfNull(did);

        Did = did;
        Active = active;
        Status = status;
        Rev = rev;
    }

    /// <summary>
    /// Gets the DID of the repository.
    /// </summary>
    [JsonRequired]
    public Did Did { get; init; }

    /// <summary>
    /// Gets a flag indicating whether the repository is active on the host.
    /// </summary>
    [JsonRequired]
    public bool Active { get; init; }

    /// <summary>
    /// Gets a possible reason why the repository is not active, if any.
    /// </summary>
    /// <remarks>
    /// <para>This is only expected when <see cref="Active"/> is <see langword="false"/>. If the repository is not active and no status
    /// is supplied, the host makes no claim about why the repository is no longer being hosted.</para>
    /// </remarks>
    public RepoStatus? Status { get; init; }

    /// <summary>
    /// Gets the current revision of the repository, if any.
    /// </summary>
    /// <remarks>
    /// <para>This is only expected when <see cref="Active"/> is <see langword="true"/>.</para>
    /// </remarks>
    public string? Rev { get; init; }
}
