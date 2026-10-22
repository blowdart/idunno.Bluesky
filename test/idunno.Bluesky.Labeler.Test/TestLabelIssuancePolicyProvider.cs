// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.Bluesky.Labeler.Test;

/// <summary>Supplies a mutable test policy or a classified policy-unavailable result.</summary>
/// <remarks>
/// <para>Tests mutate the current snapshot between operations to verify that issuance policy changes do not
/// silently rewrite already committed decisions or prevent their authorized withdrawal.</para>
/// </remarks>
internal sealed class TestLabelIssuancePolicyProvider(LabelIssuancePolicy policy) : ILabelIssuancePolicyProvider
{
    internal LabelIssuancePolicy Policy { get; set; } = policy;

    internal bool Unavailable { get; set; }

    /// <summary>Returns the current test snapshot or an explicit unavailable error without introducing fallback policy.</summary>
    public Task<LabelServiceResult<LabelIssuancePolicy>> GetPolicyAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        LabelServiceResult<LabelIssuancePolicy> result = Unavailable
            ? LabelServiceResult<LabelIssuancePolicy>.Failure(
                new(
                    code: LabelServiceErrorCode.PolicyUnavailable,
                    message: "The test policy is unavailable."))
            : LabelServiceResult<LabelIssuancePolicy>.Success(Policy);
        return Task.FromResult(result);
    }
}
