// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

using idunno.Bluesky.Labeler.Maintenance;

namespace idunno.Bluesky.Labeler.Storage;

/// <summary>Persists workflow checkpoints and revocable authority separately from public progress.</summary>
internal sealed record RetirementData(
    string OperationId,
    long Generation,
    bool Running,
    string Stage,
    long ScannedThrough,
    DateTimeOffset CreatedAt,
    string Actor,
    long AuthorityGeneration,
    string Credential,
    string? LocalIdentity,
    DateTimeOffset ExpiresAt,
    string Reason,
    string? PauseReason = null,
    DateTimeOffset? LastActivityAt = null,
    DateTimeOffset? LastProgressAt = null,
    string KeyRemoval = "None",
    string? Inventory = null,
    string? AvailabilityAttestation = null,
    string? RemovalAttestation = null,
    string? SigningPublicKey = null);

/// <summary>Binds a confirmation to immutable intent and the relevant observed state.</summary>
internal sealed record RetirementConfirmation(string Intent, string Fingerprint, long ExpiresAt);

/// <summary>Retains command outcomes after signed history has been purged.</summary>
internal sealed record RetirementReceipt(
    string OperationId,
    string Intent,
    LabelRetirementView View,
    Guid? HostInstanceId = null,
    string? Attestation = null);

/// <summary>Supplies reflection-free persistence metadata for retirement.</summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, RespectNullableAnnotations = true)]
[JsonSerializable(typeof(RetirementData))]
[JsonSerializable(typeof(RetirementConfirmation))]
[JsonSerializable(typeof(RetirementReceipt))]
internal sealed partial class RetirementJsonContext : JsonSerializerContext;
