// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.Bluesky.Labeler.Storage;

/// <summary>
/// Represents an ordered page of published signed entries returned by <see cref="ILabelStore.QueryAsync"/> or
/// <see cref="ILabelStore.ReplayAsync"/>, with a fixed stream boundary used to continue a query or drain subscription history
/// without including entries published after that boundary was captured.
/// </summary>
/// <param name="StreamId">The persistent stream identity.</param>
/// <param name="Through">The captured inclusive published sequence boundary, which can exceed the last sequence in this page.</param>
/// <param name="Labels">The ordered immutable events.</param>
/// <param name="Cursor">
/// The continuation token for the next query page, or <see langword="null"/> when the query is complete or the page is a replay result.
/// </param>
/// <remarks>
/// <para>
/// For a query, pass <see cref="Cursor"/> back to <see cref="ILabelStore.QueryAsync"/> with the same filters.
/// Later publication does not change the captured <see cref="Through"/> boundary of that query.
/// </para>
/// <para>
/// Replay results always have a <see langword="null"/> <paramref name="Cursor"/>. Continue replay using the last returned
/// <see cref="StoredLabel.Sequence"/> as the exclusive starting boundary, retaining the same <see cref="StreamId"/>
/// and <see cref="Through"/> until that boundary has been drained.
/// This is a persistence result, not an AT Protocol wire-response model.
/// </para>
/// </remarks>
public sealed record LabelPage(Guid StreamId, long Through, IReadOnlyList<StoredLabel> Labels, string? Cursor);
