// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;

namespace idunno.Bluesky.Labeler.Agent.Errors;

/// <summary>Provides the base class and mappings for typed labeler-agent XRPC errors.</summary>
public abstract class LabelerAgentError : AtProtoError
{
    /// <summary>Initializes a new instance of the <see cref="LabelerAgentError"/> class.</summary>
    /// <param name="errorDetail">The AT Protocol error details.</param>
    /// <param name="expectedError">The error identifier expected by the derived type.</param>
    /// <exception cref="ArgumentNullException"><paramref name="errorDetail"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="errorDetail"/> has a different error identifier.</exception>
    protected LabelerAgentError(AtErrorDetail errorDetail, string expectedError) : base(errorDetail)
    {
        ArgumentNullException.ThrowIfNull(errorDetail);
        if (!string.Equals(errorDetail.Error, expectedError, StringComparison.Ordinal))
        {
            throw new ArgumentException($"The provided error detail does not have the expected error '{expectedError}'.", nameof(errorDetail));
        }
    }

    /// <summary>Maps a known labeler XRPC error code to its corresponding typed error.</summary>
    /// <param name="errorDetail">The error detail to map.</param>
    /// <returns>The matching labeler error, or <paramref name="errorDetail"/> when no labeler mapping applies.</returns>
    internal static AtErrorDetail? MapLabelerError(AtErrorDetail? errorDetail)
    {
        if (errorDetail is null)
        {
            return null;
        }

        return errorDetail.Error switch
        {
            CidMismatch.ErrorTitle => new CidMismatch(errorDetail),
            IdempotencyConflict.ErrorTitle => new IdempotencyConflict(errorDetail),
            InvalidExpiry.ErrorTitle => new InvalidExpiry(errorDetail),
            InvalidTarget.ErrorTitle => new InvalidTarget(errorDetail),
            IssuanceClosed.ErrorTitle => new IssuanceClosed(errorDetail),
            MaintenanceActive.ErrorTitle => new MaintenanceActive(errorDetail),
            OperationNotFound.ErrorTitle => new OperationNotFound(errorDetail),
            OutcomeUnknown.ErrorTitle => new OutcomeUnknown(errorDetail),
            PolicyUnavailable.ErrorTitle => new PolicyUnavailable(errorDetail),
            ServiceUnavailable.ErrorTitle => new ServiceUnavailable(errorDetail),
            SigningFailed.ErrorTitle => new SigningFailed(errorDetail),
            StaleDecision.ErrorTitle => new StaleDecision(errorDetail),
            StorageUnavailable.ErrorTitle => new StorageUnavailable(errorDetail),
            TargetNotAllowed.ErrorTitle => new TargetNotAllowed(errorDetail),
            TargetNotFound.ErrorTitle => new TargetNotFound(errorDetail),
            TargetResolutionFailed.ErrorTitle => new TargetResolutionFailed(errorDetail),
            TimestampExhausted.ErrorTitle => new TimestampExhausted(errorDetail),
            ValueNotDeclared.ErrorTitle => new ValueNotDeclared(errorDetail),
            _ => errorDetail
        };
    }
}

/// <summary>Represents a mismatch between the requested and current content version.</summary>
/// <param name="errorDetail">The AT Protocol error details.</param>
public sealed class CidMismatch(AtErrorDetail errorDetail) : LabelerAgentError(errorDetail, ErrorTitle)
{
    internal const string ErrorTitle = "CidMismatch";
}

/// <summary>Represents reuse of an operation identifier for a different request.</summary>
/// <param name="errorDetail">The AT Protocol error details.</param>
public sealed class IdempotencyConflict(AtErrorDetail errorDetail) : LabelerAgentError(errorDetail, ErrorTitle)
{
    internal const string ErrorTitle = "IdempotencyConflict";
}

/// <summary>Represents an invalid label expiry.</summary>
/// <param name="errorDetail">The AT Protocol error details.</param>
public sealed class InvalidExpiry(AtErrorDetail errorDetail) : LabelerAgentError(errorDetail, ErrorTitle)
{
    internal const string ErrorTitle = "InvalidExpiry";
}

/// <summary>Represents an invalid label target.</summary>
/// <param name="errorDetail">The AT Protocol error details.</param>
public sealed class InvalidTarget(AtErrorDetail errorDetail) : LabelerAgentError(errorDetail, ErrorTitle)
{
    internal const string ErrorTitle = "InvalidTarget";
}

/// <summary>Represents an issuance service that is no longer accepting label changes.</summary>
/// <param name="errorDetail">The AT Protocol error details.</param>
public sealed class IssuanceClosed(AtErrorDetail errorDetail) : LabelerAgentError(errorDetail, ErrorTitle)
{
    internal const string ErrorTitle = "IssuanceClosed";
}

/// <summary>Represents label issuance paused during signing-key maintenance.</summary>
/// <param name="errorDetail">The AT Protocol error details.</param>
public sealed class MaintenanceActive(AtErrorDetail errorDetail) : LabelerAgentError(errorDetail, ErrorTitle)
{
    internal const string ErrorTitle = "MaintenanceActive";
}

/// <summary>Represents a missing durable operation receipt.</summary>
/// <param name="errorDetail">The AT Protocol error details.</param>
public sealed class OperationNotFound(AtErrorDetail errorDetail) : LabelerAgentError(errorDetail, ErrorTitle)
{
    internal const string ErrorTitle = "OperationNotFound";
}

/// <summary>Represents an uncertain operation outcome that must be recovered by operation identifier.</summary>
/// <param name="errorDetail">The AT Protocol error details.</param>
public sealed class OutcomeUnknown(AtErrorDetail errorDetail) : LabelerAgentError(errorDetail, ErrorTitle)
{
    internal const string ErrorTitle = "OutcomeUnknown";
}

/// <summary>Represents an unavailable label issuance policy.</summary>
/// <param name="errorDetail">The AT Protocol error details.</param>
public sealed class PolicyUnavailable(AtErrorDetail errorDetail) : LabelerAgentError(errorDetail, ErrorTitle)
{
    internal const string ErrorTitle = "PolicyUnavailable";
}

/// <summary>Represents a temporarily unavailable labeler service.</summary>
/// <param name="errorDetail">The AT Protocol error details.</param>
public sealed class ServiceUnavailable(AtErrorDetail errorDetail) : LabelerAgentError(errorDetail, ErrorTitle)
{
    internal const string ErrorTitle = "ServiceUnavailable";
}

/// <summary>Represents a failure to sign a label operation.</summary>
/// <param name="errorDetail">The AT Protocol error details.</param>
public sealed class SigningFailed(AtErrorDetail errorDetail) : LabelerAgentError(errorDetail, ErrorTitle)
{
    internal const string ErrorTitle = "SigningFailed";
}

/// <summary>Represents a label decision that became stale during the operation.</summary>
/// <param name="errorDetail">The AT Protocol error details.</param>
public sealed class StaleDecision(AtErrorDetail errorDetail) : LabelerAgentError(errorDetail, ErrorTitle)
{
    internal const string ErrorTitle = "StaleDecision";
}

/// <summary>Represents a label store failure.</summary>
/// <param name="errorDetail">The AT Protocol error details.</param>
public sealed class StorageUnavailable(AtErrorDetail errorDetail) : LabelerAgentError(errorDetail, ErrorTitle)
{
    internal const string ErrorTitle = "StorageUnavailable";
}

/// <summary>Represents a policy that does not permit the requested label target.</summary>
/// <param name="errorDetail">The AT Protocol error details.</param>
public sealed class TargetNotAllowed(AtErrorDetail errorDetail) : LabelerAgentError(errorDetail, ErrorTitle)
{
    internal const string ErrorTitle = "TargetNotAllowed";
}

/// <summary>Represents a label target that could not be found or verified.</summary>
/// <param name="errorDetail">The AT Protocol error details.</param>
public sealed class TargetNotFound(AtErrorDetail errorDetail) : LabelerAgentError(errorDetail, ErrorTitle)
{
    internal const string ErrorTitle = "TargetNotFound";
}

/// <summary>Represents failure to resolve or verify a label target.</summary>
/// <param name="errorDetail">The AT Protocol error details.</param>
public sealed class TargetResolutionFailed(AtErrorDetail errorDetail) : LabelerAgentError(errorDetail, ErrorTitle)
{
    internal const string ErrorTitle = "TargetResolutionFailed";
}

/// <summary>Represents exhaustion of representable label creation timestamps.</summary>
/// <param name="errorDetail">The AT Protocol error details.</param>
public sealed class TimestampExhausted(AtErrorDetail errorDetail) : LabelerAgentError(errorDetail, ErrorTitle)
{
    internal const string ErrorTitle = "TimestampExhausted";
}

/// <summary>Represents a label value that is not declared by the active policy.</summary>
/// <param name="errorDetail">The AT Protocol error details.</param>
public sealed class ValueNotDeclared(AtErrorDetail errorDetail) : LabelerAgentError(errorDetail, ErrorTitle)
{
    internal const string ErrorTitle = "ValueNotDeclared";
}
