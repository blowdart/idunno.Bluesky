// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

using idunno.AtProto;

namespace idunno.Bluesky.Unspecced;

/// <summary>
/// Represents a thread item in a post thread.
/// </summary>
public sealed record ThreadItem : View
{
    [JsonConstructor]
    internal ThreadItem(AtUri uri, int depth, ThreadItemValue value)
    {
        Uri = uri ?? throw new ArgumentNullException(nameof(uri));
        Depth = depth;
        Value = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>
    /// Gets the <see cref="AtUri"/> of the thread item.
    /// </summary>
    [JsonRequired]
    public AtUri Uri { get; init; }

    /// <summary>
    /// Gets the depth of the thread item in the thread.
    /// </summary>
    [JsonRequired]
    public int Depth { get; init; }

    /// <summary>
    /// Gets thread item.
    /// </summary>
    [JsonRequired]
    public ThreadItemValue Value { get; init; }
}
