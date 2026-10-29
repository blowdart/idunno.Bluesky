// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.Bluesky.AspNet.Labeler.UI;

/// <summary>Separates read-only account context from labels on the exact subject being reviewed.</summary>
internal sealed record ReviewLabelDisplay(bool IsRecord, string[] AccountLabels, string[] SubjectLabels);

/// <summary>Retains the current review route and report context while paging the shared case timeline.</summary>
internal sealed record ReviewHistoryDisplay(ModerationActivityPageView? Activity, long CaseId, long RouteId, long? ReportId);
