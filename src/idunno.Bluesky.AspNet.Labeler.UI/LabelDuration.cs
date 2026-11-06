// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Globalization;

using Microsoft.AspNetCore.Http;

namespace idunno.Bluesky.AspNet.Labeler.UI;

/// <summary>Validates finite browser label durations without accepting arbitrary expiry timestamps.</summary>
internal static class LabelDuration
{
    /// <summary>Gets the permitted durations; zero retains permanent issuance.</summary>
    internal static readonly int[] s_hours = [0, 1, 6, 12, 24, 72, 168, 720];

    /// <summary>Reads one optional duration, rejecting duplicate, malformed or unsupported choices.</summary>
    internal static bool TryRead(IFormCollection form, out int hours)
    {
        hours = 0;
        return !form.ContainsKey("labelDurationHours") ||
            form["labelDurationHours"].Count == 1 &&
            int.TryParse(form["labelDurationHours"], NumberStyles.None, CultureInfo.InvariantCulture, out hours) &&
            s_hours.Contains(hours);
    }
}

/// <summary>Displays a chosen duration and preserves frozen retry intent without editable controls.</summary>
internal sealed record LabelDurationDisplay(int Hours = 0, bool Frozen = false);
