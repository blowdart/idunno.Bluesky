// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

#pragma warning disable IDE0130 // Authentication features share the protocol's root namespace.
namespace idunno.AtProto;
#pragma warning restore IDE0130

/// <summary>
/// Contains verified reporter identity without granting administrative authority.
/// </summary>
/// <remarks>
/// <para>Instances are created only by service-token verification. Report adapters must persist replay
/// protection using both the reporter/token identifier and token hash before accepting a submission.
/// The audience retains the original signed value, including a legacy bare DID when supplied.</para>
/// </remarks>
public sealed class ModerationReportAuthenticationResult
{
    /// <summary>
    /// Initializes report evidence from the common cryptographic verifier.
    /// </summary>
    internal ModerationReportAuthenticationResult(ServiceAuthenticationResult authentication)
    {
        Reporter = authentication.Issuer;
        Labeler = authentication.Audience.Did;
        Audience = authentication.TokenAudience;
        TokenId = authentication.TokenId;
        TokenHash = authentication.TokenHash;
        ExpiresAt = authentication.ExpiresAt;
    }

    /// <summary>Gets the authenticated reporter DID.</summary>
    public Did Reporter { get; }

    /// <summary>Gets the destination labeler DID.</summary>
    public Did Labeler { get; }

    /// <summary>Gets the original signed audience.</summary>
    public string Audience { get; }

    /// <summary>Gets the signed token identifier used for replay protection.</summary>
    public string TokenId { get; }

    /// <summary>Gets the uppercase hexadecimal SHA-256 hash of the compact token.</summary>
    public string TokenHash { get; }

    /// <summary>Gets the signed token expiry without clock skew.</summary>
    public DateTimeOffset ExpiresAt { get; }
}
