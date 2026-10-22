// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.Bluesky.Labeler;

/// <summary>
/// Represents a persisted signed label together with its permanent stream position, returned by queries and
/// subscription replay to preserve publication order and allow a subscriber to resume after the last received event.
/// </summary>
/// <param name="Sequence">The permanent event sequence.</param>
/// <param name="SignedLabel">The exact signed representation.</param>
/// <remarks>
/// <para>
/// The <paramref name="Sequence"/> identifies this event within its store's stream, not across different streams.
/// The signed representation preserves the original signed fields rather than reconstructing them from database projections.
/// </para>
/// </remarks>
public sealed record StoredLabel(long Sequence, SignedLabelSnapshot SignedLabel);
