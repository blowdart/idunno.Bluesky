// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.Bluesky.Labeler.Moderation;

/// <summary>Retains an independent PDS observation without changing submitted evidence or native label scope.</summary>
internal sealed record LabelRecordResolution(string? Cid, string Status, string? Pds, DateTimeOffset ObservedAt);
