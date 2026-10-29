// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.Bluesky.Labeler.Administration;

namespace idunno.Bluesky.AspNet.Labeler.UI;

/// <summary>Shares accessible sortable column headings across all administrative list tables.</summary>
internal sealed record TableSortHeader(TableNavigation Table, LabelTableSort Sort, string Label, bool Numeric = false);
