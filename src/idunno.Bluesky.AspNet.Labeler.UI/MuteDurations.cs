// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.Bluesky.AspNet.Labeler.UI;

/// <summary>Shares the finite Ozone mute durations between browser forms and submission validation.</summary>
internal static class MuteDurations
{
    /// <summary>Gets the supported mute durations in hours, without a permanent option.</summary>
    internal static IReadOnlyList<int> Hours { get; } = [6, 12, 24, 48, 72, 168, 720, 2160];
}
