// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Standard.Site;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Platform-specific preferences for the publication, including discovery and visibility settings.
/// </summary>
public record Preferences
{
    /// <summary>
    /// Creates a new instance of the <see cref="Preferences"/> record.
    /// </summary>
    /// <param name="showInDiscover">Flag indicating whether the publication should appear in the discover feed.</param>
    [JsonConstructor]
    public Preferences(bool showInDiscover) => ShowInDiscover = showInDiscover;

    /// <summary>
    /// Flag indicating whether the publication should appear in the discover feed.
    /// </summary>
    [JsonRequired]
    public bool ShowInDiscover { get; set; }

    /// <summary>
    /// A list of keys and element data that do not map to any strongly typed properties.
    /// </summary>
    [NotNull]
    [ExcludeFromCodeCoverage]
    [JsonExtensionData]
    [SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "Needs to be settable for json deserialization")]
    public IDictionary<string, JsonElement>? ExtensionData { get; set; } = new Dictionary<string, JsonElement>();
}