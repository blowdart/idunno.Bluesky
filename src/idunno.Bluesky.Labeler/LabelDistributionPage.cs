// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.Bluesky.Labeler;

/// <summary>Represents bounded progress through a published retained-slot snapshot.</summary>
/// <param name="ScannedThrough">The exclusive position for the next read, even when <paramref name="Labels"/> is empty.</param>
/// <param name="Labels">The ordered retained signed labels.</param>
public sealed record LabelDistributionPage(long ScannedThrough, IReadOnlyList<StoredLabel> Labels);
