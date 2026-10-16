// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.AtProto.Jetstream;

/// <summary>
/// Encapsulates the properties of a Jetstream commit event.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Major Code Smell", "S1133", Justification = "Retained for source compatibility with v1 consumers.")]
[Obsolete("Use JetstreamCommitEvent for Jetstream v2 events.")]
public record AtJetstreamCommitEvent : JetstreamEvent
{
    /// <summary>
    /// Gets a value that indicates whether this record is a sync backfill assertion rather than a live create.
    /// </summary>
    public bool IsSyncBackfill { get; init; }

    /// <summary>
    /// Gets the commit that triggered the event.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown when the value being set is <see langword="null"/>.</exception>
    [JsonInclude]
    [JsonRequired]
    public required AtJetstreamCommit Commit
    {
        get;

        init
        {
            ArgumentNullException.ThrowIfNull(value);

            field = value;
        }
    }
}
