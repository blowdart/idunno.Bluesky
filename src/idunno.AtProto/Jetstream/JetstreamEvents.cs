// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

#pragma warning disable CS0618 // New event names derive from deprecated names to preserve old type patterns.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

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
public sealed record JetstreamCommit
{
    /// <summary>Gets the commit operation.</summary>
    [JsonInclude]
    [JsonRequired]
    public required JetstreamCommitOperation Operation { get; init; }

    /// <summary>Gets the collection of the committed record.</summary>
    /// <exception cref="ArgumentNullException">The value is <see langword="null"/>.</exception>
    [JsonInclude]
    [JsonRequired]
    public required Nsid Collection
    {
        get;
        init
        {
            ArgumentNullException.ThrowIfNull(value);
            field = value;
        }
    }

    /// <summary>Gets the commit revision.</summary>
    /// <exception cref="ArgumentNullException">The value is <see langword="null"/>.</exception>
    [JsonInclude]
    [JsonRequired]
    public required string Rev
    {
        get;
        init
        {
            ArgumentNullException.ThrowIfNull(value);
            field = value;
        }
    }

    /// <summary>Gets the record key.</summary>
    /// <exception cref="ArgumentNullException">The value is <see langword="null"/>.</exception>
    [JsonPropertyName("rkey")]
    [JsonInclude]
    [JsonRequired]
    public required RecordKey RKey
    {
        get;
        init
        {
            ArgumentNullException.ThrowIfNull(value);
            field = value;
        }
    }

    /// <summary>Gets the record value, if available.</summary>
    public JsonElement? Record { get; init; }

    /// <summary>Gets the record CID, if available.</summary>
    public Cid? Cid
    {
        get => ExplicitCid ?? DeferredCid?.Value;
        init => ExplicitCid = value;
    }

    internal Cid? ExplicitCid { get; init; }

    internal Lazy<Cid>? DeferredCid { get; set; }

    internal void SetDagCborPayload(byte[] payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        byte[] ownedPayload = (byte[])payload.Clone();
        DeferredCid = new Lazy<Cid>(() => Cid.FromDagCbor(ownedPayload));
    }

    /// <summary>
    /// Converts a legacy commit into the preferred Jetstream commit type.
    /// </summary>
    /// <param name="commit">The legacy commit to convert.</param>
    /// <returns>A commit with the same record data and deferred CID.</returns>
    /// <exception cref="ArgumentNullException">The legacy commit is <see langword="null"/>.</exception>
    public static implicit operator JetstreamCommit(AtJetstreamCommit commit)
    {
        ArgumentNullException.ThrowIfNull(commit);

        return new JetstreamCommit
        {
            Operation = commit.Operation,
            Collection = commit.Collection,
            Rev = commit.Rev,
            RKey = commit.RKey,
            Record = commit.Record,
            Cid = commit.ExplicitCid,
            DeferredCid = commit.DeferredCid
        };
    }

    /// <summary>
    /// Converts a legacy commit into a Jetstream commit.
    /// </summary>
    /// <param name="commit">The legacy commit to convert.</param>
    /// <returns>A commit with the same record data and deferred CID.</returns>
    /// <exception cref="ArgumentNullException">The legacy commit is <see langword="null"/>.</exception>
    public static JetstreamCommit FromAtJetstreamCommit(AtJetstreamCommit commit) => commit;

    /// <summary>
    /// Converts a Jetstream commit into a legacy commit for compatibility with legacy event properties.
    /// </summary>
    /// <param name="commit">The commit to convert.</param>
    /// <returns>A legacy commit with the same record data and deferred CID.</returns>
    /// <exception cref="ArgumentNullException">The commit is <see langword="null"/>.</exception>
    public static implicit operator AtJetstreamCommit(JetstreamCommit commit)
    {
        ArgumentNullException.ThrowIfNull(commit);

        return new AtJetstreamCommit
        {
            Operation = commit.Operation,
            Collection = commit.Collection,
            Rev = commit.Rev,
            RKey = commit.RKey,
            Record = commit.Record,
            Cid = commit.ExplicitCid,
            DeferredCid = commit.DeferredCid
        };
    }

    /// <summary>
    /// Converts a Jetstream commit into a legacy commit.
    /// </summary>
    /// <returns>A legacy commit with the same record data and deferred CID.</returns>
    public AtJetstreamCommit ToAtJetstreamCommit() => this;
}

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
