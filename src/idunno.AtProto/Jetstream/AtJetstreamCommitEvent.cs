// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.AtProto.Jetstream;

/// <summary>
/// Encapsulates the properties of a Jetstream commit event.
/// </summary>
public sealed record AtJetstreamCommitEvent : AtJetstreamEvent
{
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
