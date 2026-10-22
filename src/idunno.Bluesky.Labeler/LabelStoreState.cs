// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.Bluesky.Labeler;

/// <summary>
/// Represents the persisted stream identity, sequence boundaries and lifecycle checkpoints used to coordinate
/// label issuance, publication, subscription replay, retirement and purge.
/// </summary>
/// <param name="StreamId">The persistent stream identity.</param>
/// <param name="Issuer">The bound issuer DID text.</param>
/// <param name="Revision">The mutation revision supplied to operations that require an optimistic concurrency check.</param>
/// <param name="IssuedThrough">The greatest committed issued sequence.</param>
/// <param name="PublishedThrough">The greatest published sequence.</param>
/// <param name="ReplayFloor">The greatest purged sequence, below which replay cannot resume.</param>
/// <param name="Phase">The lifecycle phase.</param>
/// <param name="RetirementUpperBound">The frozen retirement enumeration boundary.</param>
/// <param name="RetirementThrough">The completed retirement enumeration checkpoint.</param>
/// <param name="GraceDeadline">
/// The earliest database-clock timestamp at which retirement may complete, or <see langword="null"/> before a grace period begins.
/// </param>
/// <remarks>
/// <para>
/// Read this state through <see cref="ILabelStore.GetStateAsync"/> or from a mutation result before making a
/// decision that depends on <paramref name="Revision"/>. A stale <paramref name="Revision"/> causes the corresponding mutation
/// to fail rather than overwrite newer state.
/// </para>
/// <para>
/// Issued labels remain pending until publication advances <see cref="PublishedThrough"/>.
/// Retirement checkpoints and the grace deadline survive provider recreation; the deadline does not acknowledge
/// downstream delivery or automatically trigger retirement or deletion.
/// </para>
/// </remarks>
public sealed record LabelStoreState(Guid StreamId, string Issuer, long Revision, long IssuedThrough,
    long PublishedThrough, long ReplayFloor, LabelerLifecyclePhase Phase, long RetirementUpperBound,
    long RetirementThrough, DateTimeOffset? GraceDeadline);
