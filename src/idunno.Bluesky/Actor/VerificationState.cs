// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.Bluesky.Actor
{
    /// <summary>
    /// Represents the verification information about the user this record is attached to.
    /// </summary>
    public sealed record VerificationState
    {
        [JsonConstructor]
        internal VerificationState(ICollection<VerificationView> verifications, string verifiedStatusString, string trustedVerifierStatusString)
        {
            Verifications = verifications;
            VerifiedStatusString = verifiedStatusString;
            TrustedVerifierStatusString = trustedVerifierStatusString;

            VerifiedStatus = verifiedStatusString.ToUpperInvariant() switch
            {
                "VALID" => VerificationStatus.Valid,
                "INVALID" => VerificationStatus.Invalid,
                "NONE" => VerificationStatus.None,
                _ => VerificationStatus.Unknown
            };

            TrustedVerifierStatus = trustedVerifierStatusString.ToUpperInvariant() switch
            {
                "VALID" => VerificationStatus.Valid,
                "INVALID" => VerificationStatus.Invalid,
                "NONE" => VerificationStatus.None,
                _ => VerificationStatus.Unknown
            };
        }

        /// <summary>
        /// All verifications issued by trusted verifiers on behalf of this user. Verifications by untrusted verifiers are not included.
        /// </summary>
        [JsonInclude]
        [JsonRequired]
        public ICollection<VerificationView> Verifications { get; init; }

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
        [JsonPropertyName("verifiedStatusEnum")]
        [JsonIgnore]
        public VerificationStatus VerifiedStatus { get; }

        /// <summary>
        /// Gets the user's status as a trusted verifier.
        /// </summary>
        [JsonPropertyName("trustedVerifierStatusEnum")]
        [JsonIgnore]
        public VerificationStatus TrustedVerifierStatus { get; }
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
}
