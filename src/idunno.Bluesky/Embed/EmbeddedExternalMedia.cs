// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.Bluesky.Embed;

/// <summary>
/// Represents an external card used as media in an embedded record with media.
/// </summary>
public sealed record EmbeddedExternalMedia : EmbeddedMediaBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="EmbeddedExternalMedia"/> class.
    /// </summary>
    /// <param name="external">The properties of the external card.</param>
    /// <exception cref="ArgumentNullException"><paramref name="external"/> is <see langword="null"/>.</exception>
    [JsonConstructor]
    public EmbeddedExternalMedia(External.Properties external)
    {
        ArgumentNullException.ThrowIfNull(external);

        External = external;
    }

    /// <summary>
    /// Gets the properties of the external card.
    /// </summary>
    [JsonInclude]
    [JsonRequired]
    public External.Properties External { get; init; }
}
