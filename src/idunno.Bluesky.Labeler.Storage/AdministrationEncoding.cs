// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

using idunno.Bluesky.Labeler.Administration;

namespace idunno.Bluesky.Labeler.Storage;

/// <summary>Persists the bounded team aggregate under the existing issuer lock.</summary>
internal sealed class AdministrationState
{
    public bool Bootstrapped { get; set; }
    public string? OwnerDid { get; set; }
    public long Sequence { get; set; }
    public bool RemoteAdministrationEnabled { get; set; }
    public Dictionary<string, AdministrationMember> Members { get; set; } = [with(StringComparer.Ordinal)];
    public Dictionary<string, AdministrationBinding> Bindings { get; set; } = [with(StringComparer.Ordinal)];
    public bool PolicyVerificationFailureActive { get; set; }
    public PolicyFailureAlert? PendingPolicyAlert { get; set; }
    public long ChangeNotificationThrough { get; set; }
    public bool NotificationsInitialized { get; set; }
    public AdministrationChangeAlert? PendingChangeAlert { get; set; }
}

/// <summary>Retains generation tombstones so re-enrollment cannot revive old credentials.</summary>
internal sealed record AdministrationMember(
    LabelTeamRole Role,
    bool Active,
    bool Removed,
    long Generation,
    string? NotificationEmail = null,
    LabelMediaPreferences? MediaPreferences = null);

/// <summary>Identifies an explicit OS delegation and its unique binding generation.</summary>
internal sealed record AdministrationBinding(string Did, string Credential);

/// <summary>Retains one coalesced policy-verification alert and its recoverable SMTP lease.</summary>
internal sealed record PolicyFailureAlert(
    string Id,
    string Category,
    DateTimeOffset OccurredAt,
    string? LeaseId = null,
    DateTimeOffset? LeaseUntil = null);

/// <summary>Retains one audit-backed change notification and its recoverable SMTP lease.</summary>
internal sealed record AdministrationChangeAlert(
    long Sequence,
    string Description,
    DateTimeOffset OccurredAt,
    string? LeaseId = null,
    DateTimeOffset? LeaseUntil = null);

/// <summary>Stores audit metadata without serializing authentication secrets or public DID internals.</summary>
internal sealed record AdministrationAudit(
    long Sequence,
    string OperationId,
    string? Actor,
    string? LocalIdentity,
    string Action,
    string Reason,
    bool Succeeded)
{
    /// <summary>Gets the original database timestamp, absent from legacy persisted events.</summary>
    public DateTimeOffset? CreatedAt { get; init; }

    /// <summary>Gets the credential-free change description, present only for eligible notification events.</summary>
    public string? ChangeDescription { get; init; }

    /// <summary>Projects persisted primitive values into the typed public audit result.</summary>
    internal LabelAdministrationReceipt ToReceipt() => new(
        Sequence,
        OperationId,
        Actor is null ? null : new(Actor),
        LocalIdentity,
        Action,
        Reason,
        Succeeded)
    {
        CreatedAt = CreatedAt
    };
}

/// <summary>Supplies trimming-safe metadata for the provider's administration formats.</summary>
[JsonSerializable(typeof(AdministrationState))]
[JsonSerializable(typeof(AdministrationAudit))]
[JsonSerializable(typeof(PolicyFailureAlert))]
[JsonSerializable(typeof(AdministrationChangeAlert))]
[JsonSerializable(typeof(string[]))]
internal sealed partial class AdministrationJsonContext : JsonSerializerContext;
