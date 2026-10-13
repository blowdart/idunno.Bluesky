// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;
using idunno.AtProto.Jetstream;

namespace idunno.Bluesky.BotSdk;

/// <summary>Chooses actions to durably save before the sender performs remote writes.</summary>
/// <remarks>
/// <para>Implementations must not publish records. Choose each complete payload, record key, and creation time once;
/// the runtime persists them and reconciles uncertain writes at that fixed key.</para>
/// <para>Implementations decide eligibility, including exclusions, event operations, backfills, and witnessed time.</para>
/// </remarks>
public interface IBotPolicy
{
    /// <summary>Evaluates a replay event and creates a batch of saved actions.</summary>
    /// <param name="item">The replay event to evaluate.</param>
    /// <param name="startedAt">The durable start time used to exclude earlier events.</param>
    /// <returns>The complete actions to save atomically, or an empty collection when no action is needed.</returns>
    /// <remarks>
    /// <para>A <see cref="System.Text.Json.JsonException"/> signals malformed source JSON: the reader logs and audits
    /// the skipped event before committing progress. Other exceptions, including argument validation failures,
    /// stop processing without committing the event. Do not use <c>JsonException</c> for policy configuration or programming errors.</para>
    /// </remarks>
    IReadOnlyList<BotAction> Evaluate(JetstreamEvent item, DateTimeOffset startedAt);
}

/// <summary>Stores a complete, versioned action and its durable delivery state.</summary>
/// <param name="SourceUri">The source post's AT URI.</param>
/// <param name="SourceSequence">The source event's sequence.</param>
/// <param name="SourceDid">The source author's DID.</param>
/// <param name="RecordKey">The fixed TID key used for creation and reconciliation.</param>
/// <param name="RecordJson">The complete serialized action payload, including any fixed creation time.</param>
/// <param name="Status">The delivery status; new work uses <c>pending</c>.</param>
/// <param name="Attempts">The number of recorded send attempts; new work uses zero.</param>
/// <param name="TargetDeleted"><see langword="true"/> when the target has been deleted; otherwise, <see langword="false"/>.</param>
/// <param name="ActionKind">The stable handler name, such as <c>reply</c> or <c>like</c>.</param>
/// <param name="ActionKey">The policy-defined identity within this action kind and source; the default is <c>default</c>.</param>
/// <param name="PayloadVersion">The handler's persisted payload format version; the default is one.</param>
/// <remarks>
/// <para>Source URI, action kind, and action key identify one intent. Distinct kinds or keys allow several
/// actions on the same source. Replayed events must use the same identities.</para>
/// <para>Uncertain work may already exist remotely and must be reconciled before another write.
/// Do not regenerate payloads or keys during retries.</para>
/// </remarks>
public sealed record BotAction(
    AtUri SourceUri,
    long SourceSequence,
    string SourceDid,
    string RecordKey,
    string RecordJson,
    string Status,
    int Attempts,
    bool TargetDeleted = false,
    string ActionKind = "reply",
    string ActionKey = "default",
    int PayloadVersion = 1);

/// <summary>Stores the complete durable replay configuration, checkpoint, and handling boundary.</summary>
/// <param name="StartedAt">The saved witnessed-time eligibility boundary.</param>
/// <param name="Service">The original Jetstream service.</param>
/// <param name="BotDid">The bot identity bound to this state.</param>
/// <param name="DeveloperMode">The mode bound to this state.</param>
/// <param name="RequestJson">The complete original request JSON, or <see langword="null"/> before initialization.</param>
/// <param name="CheckpointJson">The complete checkpoint JSON, or <see langword="null"/> before the first save.</param>
/// <param name="ProcessedThrough">The sequence through which event effects are durably committed.</param>
public sealed record ReplayState(
    DateTimeOffset StartedAt,
    string Service,
    string BotDid,
    bool DeveloperMode,
    string? RequestJson,
    string? CheckpointJson,
    long ProcessedThrough);
