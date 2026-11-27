// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;

#pragma warning disable IDE0130 // Namespace does not match folder structure.
namespace idunno.AtProto.Space;
#pragma warning restore IDE0130 // Namespace does not match folder structure.

/// <summary>
/// Describes a verified <c>com.atproto.managedspace.checkAccess</c> request received by a managing application.
/// </summary>
[Experimental("BSKYSpaces", UrlFormat = "https://bluesky.idunno.dev/docs/spaces.html")]
public sealed class ManagedSpaceAccessCheck
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ManagedSpaceAccessCheck"/> class.
    /// </summary>
    /// <param name="space">The space being checked.</param>
    /// <param name="repo">The repository whose access is being checked, if any.</param>
    /// <param name="clientId">The OAuth client identifier being checked, if any.</param>
    /// <param name="authentication">The verified service token presented by the space authority's PDS.</param>
    internal ManagedSpaceAccessCheck(
        SpaceAtUri space,
        Did? repo,
        Uri? clientId,
        ServiceAuthenticationResult authentication)
    {
        Space = space;
        Repo = repo;
        ClientId = clientId;
        Authentication = authentication;
    }

    /// <summary>
    /// Gets the space being checked.
    /// </summary>
    /// <value>The space URI, whose authority issued the verified service token.</value>
    public SpaceAtUri Space { get; }

    /// <summary>
    /// Gets the repository whose access is being checked.
    /// </summary>
    /// <value>The repository DID, or <see langword="null"/> when the PDS is checking read access without a delegated user.</value>
    public Did? Repo { get; }

    /// <summary>
    /// Gets the OAuth client identifier whose access is being checked.
    /// </summary>
    /// <value>The client identifier, or <see langword="null"/> when no client is being checked, such as for write checks.</value>
    public Uri? ClientId { get; }

    /// <summary>
    /// Gets the verified service token presented with the request.
    /// </summary>
    /// <value>The verified token. Its <see cref="ServiceAuthenticationResult.Issuer"/> equals the space authority.</value>
    public ServiceAuthenticationResult Authentication { get; }
}
