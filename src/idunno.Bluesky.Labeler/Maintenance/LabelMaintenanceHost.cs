// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.Bluesky.Labeler.Maintenance;

/// <summary>Describes a host participating in durable labeler maintenance.</summary>
/// <param name="InstanceId">The process-unique host identity.</param>
/// <param name="Acknowledged">Whether the host confirmed that it drained in-flight work.</param>
/// <param name="KeyActivated">Whether the host activated the replacement signing key.</param>
/// <param name="Stopped">Whether the host has been explicitly confirmed as stopped.</param>
/// <param name="LastSeen">The time the host last refreshed its durable presence.</param>
public sealed record LabelMaintenanceHost(Guid InstanceId, bool Acknowledged, bool KeyActivated, bool Stopped, DateTimeOffset LastSeen)
{
    /// <summary>Gets a value that indicates whether the host must activate the replacement signing key.</summary>
    /// <value><see langword="true"/> for signing participants; <see langword="false"/> for keyless distribution replicas.</value>
    public bool RequiresSigningKey { get; init; } = true;
}
