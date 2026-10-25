// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.Bluesky.AspNet.Labeler;

/// <summary>Configures whether authenticated remote labeler administration is currently available.</summary>
/// <remarks>
/// <para>Remote administration is disabled by default. Hosts may bind this option to reloadable
/// configuration; changing it affects mapped remote routes without restarting the host. Protected
/// local administration is independent and remains available.</para>
/// </remarks>
public sealed class LabelerAdministrationOptions
{
    /// <summary>Gets or sets a value indicating whether remote administration routes are enabled.</summary>
    /// <value><see langword="true"/> if authenticated remote routes are enabled; otherwise, <see langword="false"/>. The default is <see langword="false"/>.</value>
    public bool RemoteEnabled { get; set; }
}
