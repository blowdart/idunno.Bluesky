// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

using idunno.AtProto;
using idunno.AtProto.Labels;
using idunno.Bluesky.Record;
using idunno.Bluesky.RichText;

namespace idunno.Bluesky.Graph;

/// <summary>
/// Record representing a list of accounts (actors). Scope includes moderation-oriented lists, curation-oriented lists and starter packs.
/// </summary>
[JsonPolymorphic(
    IgnoreUnrecognizedTypeDiscriminators = true,
    UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FallBackToBaseType)]
[JsonDerivedType(typeof(List), typeDiscriminator: RecordType.List)]
public partial record List : BlueskyTimestampedRecord
{
    string _name;
    string? _description;

    // Needed as deserialization should always contain the DateTimeOffset.
    [JsonConstructor]
    internal List(
        string name,
        ListPurpose purpose,
        string? purposeValue,
        DateTimeOffset createdAt,
        string? description = null,
        IEnumerable<Facet>? descriptionFacets = null,
        Blob? avatar = null,
        SelfLabels? labels = null) : base(createdAt)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(name.GetUtf8Length(), Maximum.ListNameLengthInBytes);

        if (description is not null)
        {
            ArgumentOutOfRangeException.ThrowIfGreaterThan(description.GetGraphemeLength(), Maximum.ListDescriptionLengthInGraphemes);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(description.GetUtf8Length(), Maximum.ListDescriptionLengthInBytes);
        }

        _name = name;
        PurposeValue = purposeValue ?? ListPurposeConverter.ToWireValue(purpose);
        _description = description;
        Avatar = avatar;
        Labels = labels;

        if (descriptionFacets is not null)
        {
            DescriptionFacets = descriptionFacets;
        }
    }

    /// <summary>
    /// Creates a new instance of <see cref="List"/>.
    /// </summary>
    /// <param name="name">The name of the list.</param>
    /// <param name="purpose">The list's purpose.</param>
    /// <param name="description">The list's description, if any.</param>
    /// <param name="descriptionFacets">Any rich text facets to apply to the description.</param>
    /// <param name="avatar">The list's avatar, if any.</param>
    /// <param name="labels">The list's self labels, if any.</param>
    /// <param name="createdAt">The <see cref="DateTimeOffset"/> the list was created at. Defaults to <see cref="DateTimeOffset.UtcNow"/>.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> is empty.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="name"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="purpose"/> is <see cref="ListPurpose.Unknown"/>, or <paramref name="name"/> is &gt; 64 UTF-8 bytes, or <paramref name="description"/> is &gt; 300 graphemes or &gt; 3000 UTF-8 bytes.</exception>
    /// <exception cref="System.Text.Json.JsonException">The purpose is not a defined enumeration value.</exception>
    public List(
        string name,
        ListPurpose purpose,
        string? description,
        IEnumerable<Facet>? descriptionFacets = null,
        Blob? avatar = null,
        SelfLabels? labels = null,
        DateTimeOffset? createdAt = null) : base(createdAt)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(name.GetUtf8Length(), Maximum.ListNameLengthInBytes);

        if (purpose == ListPurpose.Unknown)
        {
            throw new ArgumentOutOfRangeException(nameof(purpose), $"{nameof(ListPurpose)}.{nameof(ListPurpose.Unknown)} is not a purpose a list can be created with.");
        }

        if (description is not null)
        {
            ArgumentOutOfRangeException.ThrowIfGreaterThan(description.GetGraphemeLength(), Maximum.ListDescriptionLengthInGraphemes);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(description.GetUtf8Length(), Maximum.ListDescriptionLengthInBytes);
        }

        _name = name;
        PurposeValue = ListPurposeConverter.ToWireValue(purpose);
        _description = description;

        if (descriptionFacets is not null)
        {
            DescriptionFacets = descriptionFacets;
        }

        Avatar = avatar;
        Labels = labels;
    }

    /// <summary>
    /// Gets the purpose for the list.
    /// </summary>
    /// <exception cref="System.Text.Json.JsonException">The value is not recognized. Set <see cref="PurposeValue"/> to retain a service-defined value.</exception>
    [JsonIgnore]
    // .NET 8/9 source generation still binds ignored init-only members as constructor parameters.
    [JsonPropertyName("purposeProjection")]
    public ListPurpose Purpose
    {
        get => ListPurposeConverter.FromWireValue(PurposeValue);
        init => PurposeValue = ListPurposeConverter.ToWireValue(value);
    }

    /// <summary>
    /// Gets the service-defined list purpose, retaining unrecognized values.
    /// </summary>
    /// <exception cref="ArgumentNullException">The value is <see langword="null"/>.</exception>
    [JsonRequired]
    [JsonPropertyName("purpose")]
    public string PurposeValue
    {
        get;
        init
        {
            ArgumentNullException.ThrowIfNull(value);
            field = value;
        }
    }

    /// <summary>
    /// Gets or sets the display name for the list.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when <paramref name="value"/> is <see langword="null"/> or empty.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="value"/> exceeds 64 UTF-8 bytes.</exception>
    [JsonInclude]
    [JsonRequired]
    public string Name
    {
        get
        {
            return _name;
        }

        set
        {
            ArgumentException.ThrowIfNullOrEmpty(value);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(value.GetUtf8Length(), Maximum.ListNameLengthInBytes);

            _name = value;
        }
    }

    /// <summary>
    /// Gets or sets the description of the list, if any.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="value"/> exceeds 300 graphemes or 3000 UTF-8 bytes.</exception>
    [JsonInclude]
    public string? Description
    {
        get
        {
            return _description;
        }

        set
        {
            if (value is not null)
            {
                ArgumentOutOfRangeException.ThrowIfGreaterThan(value.GetGraphemeLength(), Maximum.ListDescriptionLengthInGraphemes);
                ArgumentOutOfRangeException.ThrowIfGreaterThan(value.GetUtf8Length(), Maximum.ListDescriptionLengthInBytes);
            }

            _description = value;
        }
    }

    /// <summary>
    /// Gets the rich text facets for the list, if any.
    /// </summary>
    [JsonInclude]
    [NotNull]
    public IEnumerable<Facet>? DescriptionFacets { get; set; } = [];

    /// <summary>
    /// Gets the avatar for the list, if any.
    /// </summary>
    [JsonInclude]
    public Blob? Avatar { get; set; }

    /// <summary>
    /// Gets self labels for the list, if any.
    /// </summary>
    [JsonInclude]
    public SelfLabels? Labels { get; set; }
}