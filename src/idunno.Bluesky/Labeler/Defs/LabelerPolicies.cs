// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

using idunno.AtProto.Labels;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace idunno.Bluesky.Labeler;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// The policies for a labeler
/// </summary>
public sealed record LabelerPolicies
{
    /// <summary>
    /// Gets the description of the labeler, if any.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Gets the label values which this labeler publishes. May include global or custom labels.
    /// </summary>
    /// <remarks>
    /// <para>Any <see langword="null"/> entries are removed when the property is set. A collection property annotated as
    /// non-nullable only guarantees the collection itself is not <see langword="null"/>: neither <see cref="JsonRequiredAttribute"/>
    /// nor <see cref="System.Text.Json.JsonSerializerOptions.RespectNullableAnnotations"/> applies to the element type, so a
    /// service which returns a <see langword="null"/> entry inside an otherwise well formed collection would otherwise hand
    /// that entry straight through to the caller.</para>
    /// </remarks>
    public required ICollection<string> LabelValues
    {
        get;
        init => field = [.. value.Where(labelValue => labelValue is not null)];
    }

    /// <summary>
    /// Gets the label values created by this labeler and scoped exclusively to it. Labels defined here will override global label definitions for this labeler.
    /// </summary>
    /// <remarks>
    /// <para>Any <see langword="null"/> entries are removed when the property is set, for the reasons given on <see cref="LabelValues"/>.</para>
    /// </remarks>
    public ICollection<LabelValueDefinition> LabelValueDefinitions
    {
        get;
        init => field = value is null ? [] : [.. value.Where(labelValueDefinition => labelValueDefinition is not null)];
    } = [];
}