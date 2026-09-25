// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace idunno.Bluesky.Actor;

/// <summary>
/// Base record for actor preferences.
/// </summary>
/// <remarks>
/// <para>
/// Polymorphic serialization for preferences is handled by <c>PreferenceConverter</c> rather than by
/// <see cref="JsonPolymorphicAttribute"/>. System.Text.Json discards the <c>$type</c> property of a preference
/// whose discriminator it does not recognize, which loses data when the preference set is written back. The
/// converter maps discriminators itself so unrecognized preferences survive a round trip intact, and a custom
/// converter on a base type cannot be combined with the polymorphism attributes.
/// </para>
/// </remarks>
public record Preference
{
    /// <summary>
    /// Creates a new instance of <see cref="Preference"/>.
    /// </summary>
    [JsonConstructor]
    public Preference()
    {
    }

    /// <summary>
    /// Json overflow data for properties and elements that cannot be mapped to a strongly typed class.
    /// </summary>
    [JsonExtensionData]
    [SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "Standard pattern for JSON Extension Data")]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}