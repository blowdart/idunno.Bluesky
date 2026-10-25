// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.Bluesky.Labeler.Issuance;

/// <summary>Supplies the current immutable issuance-policy snapshot.</summary>
/// <remarks>
/// <para>The application service requires this extension point and does not provide permissive or implicit policy defaults.</para>
/// </remarks>
public interface ILabelIssuancePolicyProvider
{
    /// <summary>Gets the current policy snapshot.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A validated policy snapshot or a classified policy error.</returns>
    /// <exception cref="OperationCanceledException">The operation is canceled.</exception>
    Task<LabelServiceResult<LabelIssuancePolicy>> GetPolicyAsync(
        CancellationToken cancellationToken = default);
}
