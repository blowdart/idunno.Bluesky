// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;

namespace idunno.Bluesky;

public static partial class BlueskyServer
{
    /// <summary>
    /// Gets a shared, read only <see cref="JsonSerializerOptions"/> which includes the Bluesky record types.
    /// </summary>
    /// <remarks>
    /// <para>This is the same instance as <see cref="Bluesky.BlueskyJsonSerializerOptions.Default"/>. It cannot be changed; use
    /// <see cref="Bluesky.BlueskyJsonSerializerOptions.Options"/> to get a copy which can.</para>
    /// </remarks>
    public static JsonSerializerOptions BlueskyJsonSerializerOptions { get; } = global::idunno.Bluesky.BlueskyJsonSerializerOptions.Default;
}