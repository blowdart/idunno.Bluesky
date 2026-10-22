// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.Bluesky.Labeler;

/// <summary>Identifies a classified failure returned by the label issuance service.</summary>
/// <remarks>
/// <para>Callers should branch on these values rather than parsing message text or retained exception details.</para>
/// </remarks>
public enum LabelServiceErrorCode
{
    /// <summary>The operation target does not have a usable account or complete record identity.</summary>
    InvalidTarget,

    /// <summary>Issuance could not establish a verified identity or trustworthy target response.</summary>
    TargetResolutionFailed,

    /// <summary>An authoritative lookup explicitly established that the requested target does not exist.</summary>
    TargetNotFound,

    /// <summary>The requested record CID differs from the authoritative target version.</summary>
    CidMismatch,

    /// <summary>The current caller is not authorized to perform or inspect the requested operation.</summary>
    Unauthorized,

    /// <summary>A usable current issuance-policy snapshot is unavailable or invalid.</summary>
    PolicyUnavailable,

    /// <summary>A new issuance requests a label value absent from the active policy.</summary>
    ValueNotDeclared,

    /// <summary>The active policy does not permit issuance for the target kind or record collection.</summary>
    TargetNotAllowed,

    /// <summary>The requested expiry is not later than the final service-assigned creation timestamp.</summary>
    InvalidExpiry,

    /// <summary>No representable creation timestamp can be chosen strictly after the predecessor decision.</summary>
    TimestampExhausted,

    /// <summary>The optimistic store revision changed or an explicit withdrawal CID no longer matches current state.</summary>
    StaleDecision,

    /// <summary>The operation identifier is already committed with different semantic input.</summary>
    IdempotencyConflict,

    /// <summary>An authorized service-operation lookup found no retained receipt.</summary>
    OperationNotFound,

    /// <summary>Existing signed history has incompatible labels tied at the latest creation instant.</summary>
    AmbiguousHistory,

    /// <summary>The store lifecycle does not permit a new ordinary issue or negate decision.</summary>
    IssuanceClosed,

    /// <summary>The configured signing key could not produce the required signature.</summary>
    SigningFailed,

    /// <summary>A classified storage failure prevented a read or a mutation known not to have committed.</summary>
    StorageUnavailable,

    /// <summary>A commit was submitted but its durable outcome could not be established.</summary>
    OutcomeUnknown
}
