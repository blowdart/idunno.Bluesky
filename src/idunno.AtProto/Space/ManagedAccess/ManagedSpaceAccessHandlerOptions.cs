// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;

#pragma warning disable IDE0130 // Namespace does not match folder structure.
namespace idunno.AtProto.Space;
#pragma warning restore IDE0130 // Namespace does not match folder structure.

/// <summary>
/// Configures a <see cref="ManagedSpaceAccessHandler"/>.
/// </summary>
[Experimental("BSKYSpaces", UrlFormat = "https://bluesky.idunno.dev/docs/spaces.html")]
public sealed class ManagedSpaceAccessHandlerOptions
{
    /// <summary>
    /// Gets or sets the managing application's service reference.
    /// </summary>
    /// <value>
    /// The exact <c>did#serviceId</c> value that space owners place in a managed policy's <c>managingApp</c> property.
    /// It must be set before a handler is created.
    /// </value>
    /// <remarks>
    /// <para>The PDS resolves this DID, reads the service entry with this fragment from the DID document, and calls
    /// <c>{serviceEndpoint}/xrpc/com.atproto.managedspace.checkAccess</c>. It also uses this value as the service token's
    /// audience, so the handler rejects tokens addressed to any other service.</para>
    /// </remarks>
    public AtProtoServiceReference? ManagingApp { get; set; }

    /// <summary>
    /// Gets or sets the maximum accepted service token lifetime.
    /// </summary>
    /// <value>The maximum lifetime, or <see langword="null"/> to use the service authentication default of 15 minutes.</value>
    public TimeSpan? MaximumTokenLifetime { get; set; }
}
