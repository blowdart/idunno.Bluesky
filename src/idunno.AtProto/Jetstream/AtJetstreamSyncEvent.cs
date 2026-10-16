// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.AtProto.Jetstream;

/// <summary>
/// Encapsulates the properties of a Jetstream sync event.
/// </summary>
/// <remarks>
/// <para>Only sent by <see cref="JetstreamProtocolVersion.V2"/> servers.</para>
/// </remarks>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Major Code Smell", "S1133", Justification = "Retained for source compatibility with v1 consumers.")]
[Obsolete("Use JetstreamSyncEvent for Jetstream v2 events.")]
public record AtJetstreamSyncEvent : JetstreamEvent
{
    /// <summary>
    /// Gets the sync operation that triggered the event.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown when the value being set is <see langword="null"/>.</exception>
    [JsonInclude]
    [JsonRequired]
    public required AtJetstreamSync Sync
    {
        get;

        init
        {
            ArgumentNullException.ThrowIfNull(value);

            field = value;
        }
    }
}