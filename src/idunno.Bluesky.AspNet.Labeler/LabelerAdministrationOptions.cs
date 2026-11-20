// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.Bluesky.AspNet.Labeler;

/// <summary>Configures how authenticated remote labeler administration is controlled.</summary>
/// <remarks>
/// <para><see cref="RemoteMode"/> takes precedence when configured. If it is <see langword="null"/>,
/// the legacy <see cref="RemoteEnabled"/> setting is used. Remote administration is disabled by default.
/// Protected local administration is independent and remains available.</para>
/// </remarks>
public sealed class LabelerAdministrationOptions
{
    /// <summary>Gets or sets the configured remote administration control mode.</summary>
    /// <value>A configured mode, or <see langword="null"/> to use <see cref="RemoteEnabled"/> for compatibility. The effective default is disabled.</value>
    public RemoteAdministrationMode? RemoteMode { get; set; }

    /// <summary>Gets or sets a value indicating whether remote administration routes are enabled when no mode is configured.</summary>
    /// <value><see langword="true"/> if the legacy setting enables authenticated remote routes; otherwise, <see langword="false"/>. The default is <see langword="false"/>.</value>
    public bool RemoteEnabled { get; set; }

    /// <summary>Gets or sets the per-peer request allowance in each one-minute admission window.</summary>
    /// <value>The per-process limit, initially 120, from one through 100,000, shared by native, issuance and Ozone routes, including admitted authentication and transport denials.</value>
    public int RequestsPerMinutePerPeer { get; set; } = 120;

    /// <summary>Gets or sets the maximum concurrent remote requests from one peer.</summary>
    /// <value>The per-process limit, initially 16, from one through 1,024, enforced before authentication and auditing.</value>
    public int MaximumConcurrentRequestsPerPeer { get; set; } = 16;

    /// <summary>Gets or sets the maximum number of peer addresses retained for admission.</summary>
    /// <value>The per-process limit, initially 1,024, from one through 65,536. Additional peers are rejected until unused entries expire.</value>
    public int MaximumTrackedPeers { get; set; } = 1024;
}

/// <summary>Specifies which authority controls authenticated remote labeler administration.</summary>
public enum RemoteAdministrationMode
{
    /// <summary>Keeps authenticated remote administration unavailable.</summary>
    Disabled,
    /// <summary>Keeps authenticated remote administration available.</summary>
    Enabled,
    /// <summary>Lets the labeler owner enable or disable authenticated remote administration in Xenon.</summary>
    WebUiControlled
}
