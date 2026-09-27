// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.AtProto.Jetstream;

/// <summary>
/// Encapsulates the properties of a Jetstream account event.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Major Code Smell", "S1133", Justification = "Retained for source compatibility with v1 consumers.")]
[Obsolete("Use JetstreamAccountEvent for Jetstream v2 events.")]
public record AtJetstreamAccountEvent : JetstreamEvent
{
    /// <summary>
    /// Gets the account state change that triggered the event.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown when the value being set is <see langword="null"/>.</exception>
    [JsonInclude]
    [JsonRequired]
    public required AtJetstreamAccount Account
    {
        get;

        init
        {
            ArgumentNullException.ThrowIfNull(value);

            field = value;
        }
    }
}
