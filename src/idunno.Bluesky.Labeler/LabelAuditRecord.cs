// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.Bluesky.Labeler;

/// <summary>Captures immutable evaluation and signing-key metadata for a committed operation outcome.</summary>
/// <param name="evaluatedAt">The time at which the operation was evaluated.</param>
/// <param name="authorizationVersion">The nonblank authorization decision version.</param>
/// <param name="policyVersion">The optional nonblank policy snapshot version.</param>
/// <param name="signingKeyMultibase">The optional public signing-key multibase representation.</param>
/// <exception cref="ArgumentException">A required or supplied optional text value is blank, contains U+0000 or is not valid UTF-8.</exception>
/// <exception cref="ArgumentOutOfRangeException">A version exceeds 256 UTF-8 bytes.</exception>
/// <remarks>
/// <para>The record contains public metadata only. It must never contain private signing-key material.</para>
/// <para>Version strings are nonblank and limited to 256 UTF-8 bytes; an absent policy version indicates that no policy snapshot was evaluated.</para>
/// </remarks>
public sealed class LabelAuditRecord(
    DateTimeOffset evaluatedAt,
    string authorizationVersion,
    string? policyVersion,
    string? signingKeyMultibase)
{
    /// <summary>Gets the evaluation timestamp normalized to UTC.</summary>
    /// <value>The UTC evaluation timestamp.</value>
    public DateTimeOffset EvaluatedAt { get; } = evaluatedAt.ToUniversalTime();

    /// <summary>Gets the version identifying the authorization decision.</summary>
    /// <value>The authorization version.</value>
    public string AuthorizationVersion { get; } = LabelInput.ValidateText(authorizationVersion, nameof(authorizationVersion), 256);

    /// <summary>Gets the version identifying the evaluated policy snapshot.</summary>
    /// <value>The policy version, or <see langword="null"/> when no policy was evaluated.</value>
    public string? PolicyVersion { get; } = policyVersion is null ? null : LabelInput.ValidateText(policyVersion, nameof(policyVersion), 256);

    /// <summary>Gets the public signing key used to create a new signature.</summary>
    /// <value>The public key's multibase representation, or <see langword="null"/> when no new signature was made.</value>
    public string? SigningKeyMultibase { get; } = signingKeyMultibase is null
        ? null
        : LabelInput.ValidateText(signingKeyMultibase, nameof(signingKeyMultibase), int.MaxValue);
}
