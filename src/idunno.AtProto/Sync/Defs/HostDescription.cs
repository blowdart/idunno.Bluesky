// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace idunno.AtProto.Sync;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Information about an upstream host, as consumed by a relay.
/// </summary>
public sealed record HostDescription : Repo.AtProtoObject
{
    [JsonConstructor]
    internal HostDescription(string hostname, long? seq, long? accountCount, HostStatus? status)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hostname);

        Hostname = hostname;
        Seq = seq;
        AccountCount = accountCount;
        Status = status;
    }

    /// <summary>
    /// Gets the hostname of the upstream host.
    /// </summary>
    [JsonRequired]
    public string Hostname { get; init; }

    /// <summary>
    /// Gets a recent repository event stream sequence number, if any.
    /// </summary>
    /// <remarks>
    /// <para>This may lag behind actual stream processing, for example it may be a persisted cursor rather than an in-memory one.</para>
    /// </remarks>
    public long? Seq { get; init; }

    /// <summary>
    /// Gets the number of accounts on the relay associated with the upstream host, if any.
    /// </summary>
    /// <remarks>
    /// <para>The upstream host may actually have more accounts.</para>
    /// </remarks>
    public long? AccountCount { get; init; }

    /// <summary>
    /// Gets the status of the upstream host, if any.
    /// </summary>
    public HostStatus? Status { get; init; }
}
