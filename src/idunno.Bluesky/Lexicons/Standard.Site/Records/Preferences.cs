// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace idunno.Standard.Site;
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
    /// Gets the optional lexicon type identifier for the preferences record.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Customize this property in derived classes to provide a specific lexicon type identifier.
    /// This is used when serializing the record to include the $type property required by your lexicon.
    /// </para>
    /// </remarks>
    [JsonInclude]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("$type")]
    public virtual string? Type { get; } = null;

    /// <summary>
    /// Flag indicating whether the publication should appear in the discover feed.
    /// </summary>
    [JsonRequired]
    public bool ShowInDiscover { get; set; }
}
