// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace idunno.AtProto.Sync;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// A repository hosted by a personal data server or relay, as returned by <c>com.atproto.sync.listRepos</c>.
/// </summary>
public sealed record HostedRepository : Repo.AtProtoObject
{
    [JsonConstructor]
    internal HostedRepository(Did did, Cid head, string rev, bool? active, RepoStatus? status)
    {
        ArgumentNullException.ThrowIfNull(did);
        ArgumentNullException.ThrowIfNull(head);
        ArgumentException.ThrowIfNullOrWhiteSpace(rev);

        Did = did;
        Head = head;
        Rev = rev;
        Active = active;
        Status = status;
    }

    /// <summary>
    /// Gets the DID of the repository.
    /// </summary>
    [JsonRequired]
    public Did Did { get; init; }

    /// <summary>
    /// Gets the CID of the current commit of the repository.
    /// </summary>
    [JsonRequired]
    public Cid Head { get; init; }

    /// <summary>
    /// Gets the current revision of the repository.
    /// </summary>
    [JsonRequired]
    public string Rev { get; init; }

    /// <summary>
    /// Gets a flag indicating whether the repository is active on the host, if reported.
    /// </summary>
    public bool? Active { get; init; }

    /// <summary>
    /// Gets a possible reason why the repository is not active, if any.
    /// </summary>
    /// <remarks>
    /// <para>This is only expected when <see cref="Active"/> is <see langword="false"/>. If the repository is not active and no status
    /// is supplied, the host makes no claim about why the repository is no longer being hosted.</para>
    /// </remarks>
    public RepoStatus? Status { get; init; }
}
