// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

using idunno.AtProto;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace idunno.Bluesky.Actor;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// User preferences for a content label.
/// </summary>
public sealed record ContentLabelPreference : Preference
{
    /// <summary>
    /// Creates a new instance of <see cref="ContentLabelPreference"/>.
    /// </summary>
    /// <param name="label">The label name.</param>
    /// <param name="labelerDid">The <see cref="Did"/> of the labeler. If <see langword="null"/> this preference applies globally.</param>
    /// <param name="visibility">How the label should be treated in a UI.</param>
    /// <exception cref="ArgumentException">Thrown if <paramref name="label"/> is <see langword="null"/> or white space.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="visibility"/> is <see cref="LabelVisibility.Unknown"/>.</exception>
    public ContentLabelPreference(string label, Did? labelerDid, LabelVisibility visibility)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(label);

        Label = label;
        LabelerDid = labelerDid;
        VisibilityValue = ActorWireValues.FromLabelVisibility(visibility, nameof(visibility));
    }

    /// <summary>
    /// Creates a new instance of <see cref="ContentLabelPreference"/> from the values the service sent.
    /// </summary>
    /// <param name="label">The label name.</param>
    /// <param name="labelerDid">The <see cref="Did"/> of the labeler. If <see langword="null"/> this preference applies globally.</param>
    /// <param name="visibilityValue">How the label should be treated in a UI, as the service expresses it.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="label"/> or <paramref name="visibilityValue"/> is <see langword="null"/>.</exception>
    [JsonConstructor]
    internal ContentLabelPreference(string label, Did? labelerDid, string visibilityValue)
    {
        ArgumentNullException.ThrowIfNull(label);
        ArgumentNullException.ThrowIfNull(visibilityValue);

        Label = label;
        LabelerDid = labelerDid;
        VisibilityValue = visibilityValue;
    }

    /// <summary>
    /// The label the preference applies to.
    /// </summary>
    /// <remarks>
    /// <para>Labels are not globally unique, i.e. the same label name can be used by multiple labelers.</para>
    /// </remarks>
    [JsonRequired]
    public string Label { get; init; }

    /// <summary>
    /// The labeler this preference applies.
    /// If <see langword="null"/> the preference applies globally.
    /// </summary>
    public Did? LabelerDid { get; init; }

    /// <summary>
    /// Gets the desired visibility for the label, as the service expresses it.
    /// </summary>
    /// <remarks>
    /// <para>Label visibility is an open union, so the value the service sent is kept verbatim and
    /// <see cref="Visibility"/> is projected from it. A visibility this library does not recognize survives a read,
    /// modify and write cycle rather than being discarded.</para>
    /// </remarks>
    [JsonInclude]
    [JsonRequired]
    [JsonPropertyName("visibility")]
    internal string VisibilityValue { get; init; }

    /// <summary>
    /// The desired visibility for the label.
    /// </summary>
    /// <remarks>
    /// <para>A visibility this library does not recognize is reported as <see cref="LabelVisibility.Unknown"/>.</para>
    /// </remarks>
    [JsonIgnore]
    public LabelVisibility Visibility => ActorWireValues.ToLabelVisibility(VisibilityValue);
}

/// <summary>
/// Label effect values.
/// </summary>
public enum LabelVisibility
{
    /// <summary>
    /// The label should be ignored and not shown to the user
    /// </summary>
    Ignore = 0,

    /// <summary>
    /// The label should be shown to the user, but it should not affect the labelled content.
    /// </summary>
    Show,

    /// <summary>
    /// The user should be warned about the labelled content, and presented a way to display the content.
    /// </summary>
    Warn,

    /// <summary>
    /// The content should be hidden from the user.
    /// </summary>
    Hide,

    /// <summary>
    /// The visibility is one this library does not recognize.
    /// </summary>
    /// <remarks>
    /// <para>This value only ever comes from the service. It cannot be used to build a
    /// <see cref="ContentLabelPreference"/>, as it carries no visibility the service would understand.</para>
    /// </remarks>
    Unknown
}