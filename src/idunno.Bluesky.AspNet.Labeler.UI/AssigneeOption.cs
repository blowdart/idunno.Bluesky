// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.Bluesky.AspNet.Labeler.UI;

/// <summary>Preserves the canonical assignment value while displaying a verified handle in the dropdown.</summary>
internal sealed record AssigneeOption(string Did, string Label);
