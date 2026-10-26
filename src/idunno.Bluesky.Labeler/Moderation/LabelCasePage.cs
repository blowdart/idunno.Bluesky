// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.Bluesky.Labeler.Moderation;

/// <summary>Retains a bounded live case page and its filter-bound continuation.</summary>
/// <remarks><para>Pages are not a frozen queue snapshot. Concurrent assignment, status changes or report
/// intake can change membership between pages. Every page rechecks live inspection authority.</para></remarks>
public sealed class LabelCasePage
{
    /// <summary>Initializes a new instance of the <see cref="LabelCasePage"/> class from an authorized provider read.</summary>
    internal LabelCasePage(IReadOnlyCollection<LabelModerationCase> cases, string? cursor)
    {
        Cases = Array.AsReadOnly(cases.ToArray());
        Cursor = cursor;
    }

    /// <summary>Gets the immutable ascending case page.</summary>
    /// <value>The bounded case snapshots.</value>
    public IReadOnlyList<LabelModerationCase> Cases { get; }
    /// <summary>Gets the optional filter-bound continuation.</summary>
    /// <value>The opaque cursor, or <see langword="null"/> when no further result was observed.</value>
    public string? Cursor { get; }
}
