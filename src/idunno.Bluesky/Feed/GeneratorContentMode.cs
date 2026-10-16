// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.Bluesky.Feed;

/// <summary>
/// The content modes a feed generator can declare.
/// </summary>
[JsonConverter(typeof(GeneratorContentModeConverter))]
public enum GeneratorContentMode
{
    /// <summary>
    /// The generator does not specify a content mode.
    /// </summary>
    Unspecified,

    /// <summary>
    /// The generator serves video content.
    /// </summary>
    Video,

    /// <summary>
    /// The content mode is not one this library recognizes.
    /// </summary>
    Unknown
}
