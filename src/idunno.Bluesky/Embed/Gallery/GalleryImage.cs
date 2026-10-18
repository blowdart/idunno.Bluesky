// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

using idunno.AtProto;

namespace idunno.Bluesky.Embed.Gallery;

/// <summary>
/// Encapsulates information about an image embedded in a gallery.
/// </summary>
[JsonPolymorphic(IgnoreUnrecognizedTypeDiscriminators = false, UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization)]
[JsonDerivedType(typeof(GalleryImage), typeDiscriminator: "app.bsky.embed.gallery#image")]
public record GalleryImage : IValidatableObject, IRecordValidatable
{
    /// <summary>
    /// Creates a new instance of <see cref="Image"/>
    /// </summary>
    /// <param name="image">A <see cref="Blob"/> containing details of the uploaded image.</param>
    /// <param name="altText">The AltText (for accessibility) for the image.</param>
    /// <param name="aspectRatio">The image's aspect ratio.</param>
    /// <exception cref="ArgumentNullException">Thrown when any of the parameters are <see langword="null" />.</exception>
    [JsonConstructor]
    public GalleryImage(Blob image, string altText, AspectRatio aspectRatio)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(altText);
        ArgumentNullException.ThrowIfNull(aspectRatio);

        Image = image;
        AltText = altText;
        AspectRatio = aspectRatio;
    }

    /// <summary>
    /// Creates a new instance of <see cref="Image"/> from the specified <paramref name="image"/>.
    /// </summary>
    /// <param name="image">The <see cref="EmbeddedImage"/> to create the gallery image from.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="image"/> or any of its properties are <see langword="null"/>.</exception>
    public GalleryImage(EmbeddedImage image)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(image.Image);
        ArgumentNullException.ThrowIfNull(image.AltText);
        ArgumentNullException.ThrowIfNull(image.AspectRatio);

        Image = image.Image;
        AltText = image.AltText;
        AspectRatio = image.AspectRatio;
    }

    /// <summary>
    /// Gets a <see cref="Blob"/> containing details of the uploaded image.
    /// </summary>
    [JsonInclude]
    [JsonRequired]
    public Blob Image { get; init; }

    /// <summary>
    /// Gets AltText (for accessibility) for the image.
    /// </summary>
    [JsonInclude]
    [JsonRequired]
    [JsonPropertyName("alt")]
    public string AltText { get; init; }

    /// <summary>
    /// The image's aspect ratio.
    /// </summary>
    [JsonInclude]
    [JsonRequired]
    public AspectRatio AspectRatio { get; init; }

    /// <summary>
    /// Validates the gallery image blob, alternative text and required aspect ratio.
    /// </summary>
    /// <param name="validationContext">The context in which the validation is performed.</param>
    /// <returns>A collection of validation errors, or an empty collection when the image is valid.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="validationContext"/> is <see langword="null"/>.</exception>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        ArgumentNullException.ThrowIfNull(validationContext);

        return ValidateImage();
    }

    IEnumerable<ValidationResult> IRecordValidatable.ValidateRecord() => ValidateImage();

    private IEnumerable<ValidationResult> ValidateImage()
    {
        foreach (ValidationResult result in RecordValidation.Blob(Image, nameof(Image), 2_000_000, "image/*"))
        {
            yield return result;
        }

        if (AltText is null)
        {
            yield return new ValidationResult("Alternative text is required.", [nameof(AltText)]);
        }

        foreach (ValidationResult result in RecordValidation.Nested(AspectRatio, nameof(AspectRatio)))
        {
            yield return result;
        }
    }
}