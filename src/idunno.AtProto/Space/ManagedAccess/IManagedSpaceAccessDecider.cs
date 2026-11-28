// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;

#pragma warning disable IDE0130 // Namespace does not match folder structure.
namespace idunno.AtProto.Space;
#pragma warning restore IDE0130 // Namespace does not match folder structure.

/// <summary>
/// Decides the access a managing application grants for an authenticated <c>com.atproto.managedspace.checkAccess</c> request.
/// </summary>
/// <remarks>
/// <para>Implementations contain the managing application's own access rules. They are called only after
/// <see cref="ManagedSpaceAccessHandler"/> has verified the caller's service token, confirmed that the token was issued by
/// the space authority, and reserved the token against replay.</para>
/// </remarks>
[Experimental("BSKYSpaces", UrlFormat = "https://bluesky.idunno.dev/docs/spaces.html")]
public interface IManagedSpaceAccessDecider
{
    /// <summary>
    /// Returns the read and write access granted for an authenticated access check.
    /// </summary>
    /// <param name="check">The verified access check.</param>
    /// <param name="cancellationToken">A cancellation token for the operation.</param>
    /// <returns>The access granted to the requested repository and client.</returns>
    ValueTask<ManagedSpaceAccess> CheckAccess(ManagedSpaceAccessCheck check, CancellationToken cancellationToken);
}
