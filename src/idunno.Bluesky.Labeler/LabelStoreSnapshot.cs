// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.Bluesky.Labeler;

/// <summary>
/// Represents a consistent sample of durable stream metadata and unpublished-label backlog returned by
/// <see cref="ILabelStore.GetSnapshotAsync"/> for monitoring publisher progress and diagnosing publication delays.
/// </summary>
/// <param name="State">The sequence and lifecycle state.</param>
/// <param name="PendingLabels">The number of unpublished labels.</param>
/// <param name="OldestPendingAge">The age of the oldest pending label according to the database clock.</param>
/// <remarks>
/// <para>
/// The backlog describes shared database contents, not an individual process's queue.
/// The oldest pending age is zero when no labels await publication.
/// Collecting this snapshot also records backlog measurements; it does not publish labels or start background monitoring.
/// </para>
/// </remarks>
public sealed record LabelStoreSnapshot(LabelStoreState State, long PendingLabels, TimeSpan OldestPendingAge);
