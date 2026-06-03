// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;

namespace idunno.Bluesky;

public partial class BlueskyServer
{
    /// <summary>
    /// Gets a <see cref="JsonSerializerOptions"/> which includes the Bluesky record types.
    /// </summary>
    public static JsonSerializerOptions BlueskyJsonSerializerOptions { get; } = global::idunno.Bluesky.BlueskyJsonSerializerOptions.Options;
}
