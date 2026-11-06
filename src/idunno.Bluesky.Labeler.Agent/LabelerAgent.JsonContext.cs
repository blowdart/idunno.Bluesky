// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.Bluesky.Labeler.Agent;

/// <summary>Provides source-generated JSON metadata for the remote labeler administration protocol.</summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow)]
[JsonSerializable(typeof(AdministrationCommand))]
[JsonSerializable(typeof(AdministrationResponse))]
[JsonSerializable(typeof(AdministrationModeration))]
[JsonSerializable(typeof(AdministrationLabelChoice))]
internal sealed partial class LabelerAgentJsonContext : JsonSerializerContext;

/// <summary>Represents one bounded administration command sent to the labeler host.</summary>
/// <param name="OperationId">The durable operation identifier.</param>
/// <param name="Reason">The required administrative reason.</param>
/// <param name="Target">The account DID or record URI.</param>
/// <param name="Value">The label value.</param>
/// <param name="Cid">The optional record CID.</param>
/// <param name="ExpiresAt">The optional issue expiry.</param>
/// <param name="CorrelationId">The optional diagnostic correlation identifier.</param>
internal sealed record AdministrationCommand(
    string OperationId,
    string Reason,
    string Target = "",
    string Value = "",
    string? Cid = null,
    DateTimeOffset? ExpiresAt = null,
    string? CorrelationId = null);

/// <summary>Represents the successful result body from the labeler issuance protocol.</summary>
/// <param name="OperationId">The durable operation identifier, when present.</param>
/// <param name="Sequence">The resulting label stream sequence, when present.</param>
/// <param name="Changed">Whether a new label event was appended.</param>
/// <param name="Moderation">The optional moderation projection.</param>
/// <param name="Target">The optional target whose current label state was inspected.</param>
internal sealed record AdministrationResponse(
    string? OperationId = null,
    long? Sequence = null,
    bool? Changed = null,
    AdministrationModeration? Moderation = null,
    string? Target = null);

/// <summary>Contains the supported moderation fields required for available-label discovery.</summary>
/// <param name="LabelChoices">The label values declared by the current issuance policy.</param>
internal sealed record AdministrationModeration(AdministrationLabelChoice[]? LabelChoices = null);

/// <summary>Contains one label value returned by the current issuance policy.</summary>
/// <param name="Value">The issuable label value.</param>
/// <param name="Applied">Whether the label is currently applied to a requested target.</param>
/// <param name="Sequence">The published stream sequence associated with the label.</param>
/// <param name="OtherVersion">Whether another version of the label is also present.</param>
/// <param name="UriWide">Whether the label applies to the whole record URI.</param>
/// <param name="ExpiresAt">The optional expiry of the applied label.</param>
internal sealed record AdministrationLabelChoice(
    string Value,
    bool Applied,
    long Sequence,
    bool OtherVersion,
    bool UriWide,
    DateTimeOffset? ExpiresAt);
