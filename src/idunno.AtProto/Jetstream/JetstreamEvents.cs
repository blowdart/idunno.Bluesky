// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

#pragma warning disable CS0618 // New event names derive from deprecated names to preserve old type patterns.

using System.Diagnostics.CodeAnalysis;

namespace idunno.AtProto.Jetstream;

/// <summary>
/// Encapsulates a decoded Jetstream event.
/// </summary>
public record JetstreamEvent : AtJetstreamEvent;

/// <summary>
/// Encapsulates a decoded record change or sync backfill assertion.
/// </summary>
public sealed record JetstreamCommitEvent : AtJetstreamCommitEvent;

/// <summary>
/// Encapsulates an account state change.
/// </summary>
public sealed record JetstreamAccountEvent : AtJetstreamAccountEvent;

/// <summary>
/// Encapsulates an identity change.
/// </summary>
public sealed record JetstreamIdentityEvent : AtJetstreamIdentityEvent;

/// <summary>
/// Encapsulates a repository sync marker.
/// </summary>
public sealed record JetstreamSyncEvent : AtJetstreamSyncEvent;

/// <summary>
/// Encapsulates a record operation in a commit event.
/// </summary>
[SuppressMessage("Major Code Smell", "S2094", Justification = "Compatibility inheritance shares the existing record fields.")]
public sealed record JetstreamCommit : AtJetstreamCommit;

/// <summary>
/// Encapsulates an account operation.
/// </summary>
[SuppressMessage("Major Code Smell", "S2094", Justification = "Compatibility inheritance shares the existing record fields.")]
public sealed record JetstreamAccount : AtJetstreamAccount;

/// <summary>
/// Encapsulates an identity operation.
/// </summary>
[SuppressMessage("Major Code Smell", "S2094", Justification = "Compatibility inheritance shares the existing record fields.")]
public sealed record JetstreamIdentity : AtJetStreamIdentity;

/// <summary>
/// Encapsulates a repository sync operation.
/// </summary>
[SuppressMessage("Major Code Smell", "S2094", Justification = "Compatibility inheritance shares the existing record fields.")]
public sealed record JetstreamSync : AtJetstreamSync;

#pragma warning restore CS0618
