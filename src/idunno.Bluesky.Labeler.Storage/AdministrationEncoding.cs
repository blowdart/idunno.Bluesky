// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.Bluesky.Labeler.Storage;

/// <summary>Persists the bounded team aggregate under the existing issuer lock.</summary>
internal sealed class AdministrationState
{
    public bool Bootstrapped { get; set; }
    public long Sequence { get; set; }
    public Dictionary<string, AdministrationMember> Members { get; set; } = new(StringComparer.Ordinal);
    public Dictionary<string, AdministrationBinding> Bindings { get; set; } = new(StringComparer.Ordinal);
    public Dictionary<string, long> RevokedTokens { get; set; } = new(StringComparer.Ordinal);
}

/// <summary>Retains generation tombstones so re-enrollment cannot revive old credentials.</summary>
internal sealed record AdministrationMember(LabelTeamRole Role, bool Active, bool Removed, long Generation);

/// <summary>Identifies an explicit OS delegation and its unique binding generation.</summary>
internal sealed record AdministrationBinding(string Did, string Credential);

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
    /// <summary>Projects persisted primitive values into the typed public audit result.</summary>
    internal LabelAdministrationReceipt ToReceipt() => new(
        Sequence,
        OperationId,
        Actor is null ? null : new(Actor),
        LocalIdentity,
        Action,
        Reason,
        Succeeded);
}

/// <summary>Supplies trimming-safe metadata for the provider's administration formats.</summary>
[JsonSerializable(typeof(AdministrationState))]
[JsonSerializable(typeof(AdministrationAudit))]
[JsonSerializable(typeof(string[]))]
internal sealed partial class AdministrationJsonContext : JsonSerializerContext;
