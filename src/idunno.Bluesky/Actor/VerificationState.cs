// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.Bluesky.Actor;

/// <summary>
/// Represents the verification information about the user this record is attached to.
/// </summary>
public sealed record VerificationState
{
    [JsonConstructor]
    internal VerificationState(ICollection<VerificationView> verifications, string verifiedStatusString, string trustedVerifierStatusString)
    {
        ArgumentNullException.ThrowIfNull(verifications);
        ArgumentNullException.ThrowIfNull(verifiedStatusString);
        ArgumentNullException.ThrowIfNull(trustedVerifierStatusString);

        Verifications = verifications;
        VerifiedStatusString = verifiedStatusString;
        TrustedVerifierStatusString = trustedVerifierStatusString;
    }

    /// <summary>
    /// All verifications issued by trusted verifiers on behalf of this user. Verifications by untrusted verifiers are not included.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown when the value set is <see langword="null"/>.</exception>
    [JsonInclude]
    [JsonRequired]
    public ICollection<VerificationView> Verifications
    {
        get;

        init
        {
            ArgumentNullException.ThrowIfNull(value);

            field = new List<VerificationView>(value).AsReadOnly();
        }
    }

    [JsonInclude]
    [JsonPropertyName("verifiedStatus")]
    [JsonRequired]
    internal string VerifiedStatusString { get; init; }

    [JsonInclude]
    [JsonPropertyName("trustedVerifierStatus")]
    [JsonRequired]
    internal string TrustedVerifierStatusString { get; init; }

    /// <summary>
    /// Gets the user's status as a verified account.
    /// </summary>
    /// <remarks>
    /// <para>A status this library does not recognize is reported as <see cref="VerificationStatus.Unknown"/>.</para>
    /// </remarks>
    [JsonIgnore]
    public VerificationStatus VerifiedStatus => ToVerificationStatus(VerifiedStatusString);

    /// <summary>
    /// Gets the user's status as a trusted verifier.
    /// </summary>
    /// <remarks>
    /// <para>A status this library does not recognize is reported as <see cref="VerificationStatus.Unknown"/>.</para>
    /// </remarks>
    [JsonIgnore]
    public VerificationStatus TrustedVerifierStatus => ToVerificationStatus(TrustedVerifierStatusString);

    private static VerificationStatus ToVerificationStatus(string status) => status switch
    {
        "valid" => VerificationStatus.Valid,
        "invalid" => VerificationStatus.Invalid,
        "none" => VerificationStatus.None,
        _ => VerificationStatus.Unknown
    };
}

/// <summary>
/// Values for VerifiedStatus and TrustedVerifierStatus in a <see cref="VerificationState"/>.
/// </summary>
public enum VerificationStatus
{
    /// <summary>
    /// The verification status is unknown, or the status cannot be mapped to a <see cref="VerificationStatus"/> value.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// The status is valid.
    /// </summary>
    Valid,

    /// <summary>
    /// The status is invalid.
    /// </summary>
    Invalid,

    /// <summary>
    /// There is no status.
    /// </summary>
    None
}