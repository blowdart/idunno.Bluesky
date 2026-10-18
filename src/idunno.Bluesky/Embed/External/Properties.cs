// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

using idunno.AtProto;
using idunno.AtProto.Repo;

namespace idunno.Bluesky.Embed.External;

/// <summary>
/// The properties for an embedded external media record.
/// </summary>
[JsonPolymorphic(IgnoreUnrecognizedTypeDiscriminators = true,
                 UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FallBackToBaseType)]
[JsonDerivedType(typeof(Properties), typeDiscriminator: "app.bsky.embed.external#external")]
public record Properties : IValidatableObject, IRecordValidatable
{
    private string _uri;

    internal Properties(string uri, string title, string description, Blob? thumbnail, IReadOnlyCollection<StrongReference>? associatedRefs, bool validate)
    {
        if (validate)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(uri);
        }

        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(description);
        _uri = uri;
        Title = title;
        Description = description;
        Thumbnail = thumbnail;
        AssociatedRefs = associatedRefs;
    }

    /// <summary>
    /// Creates a new instance of <see cref="Properties"/>.
    /// </summary>
    /// <param name="uri">The external uri, as a string, for the link.</param>
    /// <param name="title">The title for the external link.</param>
    /// <param name="description">The description of the external link, if any.</param>
    /// <param name="thumbnail">The <see cref="Blob"/> for the thumbnail of the link, if any.</param>
    /// <param name="associatedRefs">The collection of <see cref="StrongReference"/> representing the Atmosphere records for this external content, if any.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="uri"/> is <see langword="null"/> or whitespace.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="title"/> or <paramref name="description"/> is <see langword="null"/>.</exception>
    [JsonConstructor]
    [SuppressMessage("Design", "CA1054:URI-like parameters should not be strings", Justification = "The Bluesky web app can create facets with illegal URIs, so a string is used to accommodate them.")]
    public Properties(string uri, string title, string description, Blob? thumbnail = null, IReadOnlyCollection<StrongReference>? associatedRefs = null)
        : this(uri, title, description, thumbnail, associatedRefs, validate: true)
    {
    }

    /// <summary>
    /// Gets or sets the external uri, as a string.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when the value is <see langword="null"/> or whitespace.</exception>
    /// <remarks><para>This property is a string to accommodate illegal URIs that the Bluesky web app can create. Validate the URI before using it.</para></remarks>
    [JsonInclude]
    [JsonRequired]
    [SuppressMessage("Design", "CA1056:URI-like properties should not be strings", Justification = "The official Bluesky can create facets with illegal URIs, so this property is a string to accommodate those cases.")]
    public string Uri {
        get => _uri;

        set
        {
            // Even though we are using a string for the URI, we still want to ensure that it is not null or whitespace, as its marked as required in the lexicon and this a bare minimum validation.
            ArgumentException.ThrowIfNullOrWhiteSpace(value);
            _uri = value;
        }
    }

    /// <summary>
    /// Gets or sets the title for the external link.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown when the value is <see langword="null"/>.</exception>
    [JsonInclude]
    [JsonRequired]
    public string Title
    {
        get;

        set
        {
            ArgumentNullException.ThrowIfNull(value);
            field = value;
        }
    }

    /// <summary>
    /// Gets or sets the description of the external link.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown when the value is <see langword="null"/>.</exception>
    [JsonInclude]
    [JsonRequired]
    public string Description
    {
        get;

        set
        {
            ArgumentNullException.ThrowIfNull(value);
            field = value;
        }
    }

    /// <summary>
    /// Gets or sets the <see cref="Blob"/> to a thumbnail image for the external link.
    /// </summary>
    [JsonInclude]
    [JsonPropertyName("thumb")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Blob? Thumbnail { get; set; }

    /// <summary>
    /// Gets or sets a collection of <see cref="StrongReference"/>s to the Atmosphere records representing this external content, if they exist.
    /// </summary>
    [JsonInclude]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyCollection<StrongReference>? AssociatedRefs
    {
        get;
        set => field = value is null ? null : [.. value];
    }

    /// <summary>
    /// Validates the external card URI, text, thumbnail and associated references.
    /// </summary>
    /// <param name="validationContext">The context in which the validation is performed.</param>
    /// <returns>A collection of validation errors, or an empty collection when the external card is valid.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="validationContext"/> is <see langword="null"/>.</exception>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        ArgumentNullException.ThrowIfNull(validationContext);

        return ValidateProperties();
    }

    IEnumerable<ValidationResult> IRecordValidatable.ValidateRecord() => ValidateProperties();

    private IEnumerable<ValidationResult> ValidateProperties()
    {
        if (!System.Uri.TryCreate(Uri, UriKind.Absolute, out _))
        {
            yield return new ValidationResult("An absolute external URI is required.", [nameof(Uri)]);
        }

        if (Title is null)
        {
            yield return new ValidationResult("A title is required.", [nameof(Title)]);
        }

        if (Description is null)
        {
            yield return new ValidationResult("A description is required.", [nameof(Description)]);
        }

        if (Thumbnail is not null)
        {
            foreach (ValidationResult result in RecordValidation.Blob(Thumbnail, nameof(Thumbnail), 1_000_000, "image/*"))
            {
                yield return result;
            }
        }

        if (AssociatedRefs is not null && AssociatedRefs.Any(reference => reference is null))
        {
            yield return new ValidationResult("Associated references cannot contain null.", [nameof(AssociatedRefs)]);
        }
    }

    /// <summary>
    /// Determines whether the specified <see cref="Properties"/> is equal to the current instance.
    /// </summary>
    /// <param name="other">The <see cref="Properties"/> to compare against the current instance.</param>
    /// <returns><see langword="true" /> if <paramref name="other"/> is equal to the current instance, otherwise <see langword="false" />.</returns>
    /// <remarks>
    /// <para><see cref="AssociatedRefs"/> is compared by its contents rather than by reference.</para>
    /// </remarks>
    public virtual bool Equals(Properties? other)
    {
        if (ReferenceEquals(this, other))
        {
            return true;
        }

        if (other is null || EqualityContract != other.EqualityContract)
        {
            return false;
        }

        return Uri == other.Uri &&
            Title == other.Title &&
            Description == other.Description &&
            Thumbnail == other.Thumbnail &&
            CollectionComparison.SequenceEquals(AssociatedRefs, other.AssociatedRefs);
    }

    /// <summary>
    /// Returns the hash code for the current instance.
    /// </summary>
    /// <returns>The hash code for the current instance.</returns>
    /// <remarks>
    /// <para>
    ///   The hash code is derived from the current property values, so it changes if the instance is mutated.
    ///   Do not mutate an instance while it is being used as a key in a hashed collection.
    /// </para>
    /// </remarks>
    [SuppressMessage("Major Code Smell", "S2328:\"GetHashCode\" should not reference mutable fields", Justification = "Value equality over mutable properties requires the hash code to be derived from the same state, which is documented on the member.")]
    public override int GetHashCode()
    {
        HashCode hashCode = new();

        hashCode.Add(EqualityContract);
        hashCode.Add(Uri, StringComparer.Ordinal);
        hashCode.Add(Title, StringComparer.Ordinal);
        hashCode.Add(Description, StringComparer.Ordinal);
        hashCode.Add(Thumbnail);
        CollectionComparison.AddSequence(ref hashCode, AssociatedRefs);

        return hashCode.ToHashCode();
    }
}