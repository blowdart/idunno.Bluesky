// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;

namespace idunno.Bluesky.Labeler.Administration;

/// <summary>Describes a durable administration receipt or audit event.</summary>
/// <param name="Sequence">The issuer-local audit sequence.</param>
/// <param name="OperationId">The operation identity, or an empty value for an authorization event.</param>
/// <param name="Actor">The authenticated or delegated actor DID, or <see langword="null"/> for an unbound OS identity.</param>
/// <param name="LocalIdentity">The authenticated OS identity, when applicable.</param>
/// <param name="Action">The operation or permission.</param>
/// <param name="Reason">The supplied reason.</param>
/// <param name="Succeeded">Whether the operation was authorized and committed.</param>
public sealed record LabelAdministrationReceipt(
    long Sequence,
    string OperationId,
    Did? Actor,
    string? LocalIdentity,
    string Action,
    string Reason,
    bool Succeeded);
