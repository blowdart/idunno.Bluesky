// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.Bluesky.Labeler.Issuance;

/// <summary>Provides mandatory host-defined authorization decisions for label-service operations.</summary>
/// <remarks>
/// <para>Implementations must make decisions from trusted host context. Authentication, role checks and live permission fencing are outside this contract.</para>
/// <para>Authorization failures should be returned explicitly; the service does not install a permissive default.</para>
/// </remarks>
public interface ILabelIssuanceAuthorizer
{
    /// <summary>Authorizes a service operation for the current caller.</summary>
    /// <param name="request">The service-created authorization request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A grant on success or a classified authorization error.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is <see langword="null"/>.</exception>
    /// <exception cref="OperationCanceledException">The operation is canceled.</exception>
    Task<LabelServiceResult<LabelAuthorizationGrant>> AuthorizeAsync(
        LabelAuthorizationRequest request,
        CancellationToken cancellationToken = default);
}
