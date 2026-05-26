// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.Bluesky.Embed;

/// <summary>
/// Represents a view over an <see cref="EmbeddedExternal"/> record.
/// </summary>
public sealed record EmbeddedExternalView : EmbeddedView
{
    /// <summary>
    /// Creates a new instance of <see cref="EmbeddedExternalView"/>.
    /// </summary>
    /// <param name="external">The external view properties for the embedded media.</param>
    [JsonConstructor]
    internal EmbeddedExternalView(External.View external) : base()
    {
        External = external;
    }

    /// <summary>
    /// Gets the properties for this instance.
    /// </summary>
    [JsonInclude]
    [JsonRequired]
    public External.View External { get; init; }
}
