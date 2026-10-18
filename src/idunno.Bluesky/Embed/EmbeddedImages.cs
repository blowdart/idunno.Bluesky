// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace idunno.Bluesky.Embed;

/// <summary>
/// Represents embedded images in a post for.
/// </summary>
public record EmbeddedImages : EmbeddedMediaBase, IValidatableObject, IRecordValidatable
{
    /// <summary>
    /// Creates a new instance of <see cref="EmbeddedImages"/>.
    /// </summary>
    /// <param name="images">The images to embed in a post.</param>
    /// <exception cref="ArgumentNullException">Thrown when images is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the images contains more than the maximum number of images for a post, or does not contain any images.</exception>
    public EmbeddedImages(ICollection<EmbeddedImage> images)
    {
        ArgumentNullException.ThrowIfNull(images);

        ArgumentOutOfRangeException.ThrowIfLessThan(images.Count, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(images.Count, Maximum.ImagesInPost);

        Images = [.. images];
    }

    /// <summary>
    /// Creates a new instance of <see cref="EmbeddedImages"/> when deserializing.
    /// </summary>
    /// <remarks>
    /// <para><see href="https://github.com/bluesky-social/atproto/blob/main/lexicons/app/bsky/embed/images.json">app.bsky.embed.images</see>
    /// places no lower bound on the number of images, so an empty collection is a valid payload and must not be rejected when reading.
    /// The stricter validation in the public constructor applies only to images being embedded in a post by this library.</para>
    /// </remarks>
    [JsonConstructor]
    internal EmbeddedImages()
    {
        Images = [];
    }

    /// <summary>
    /// Gets the collection of images to embed.
    /// </summary>
    [JsonInclude]
    [JsonRequired]
    public ICollection<EmbeddedImage> Images { get; init; }

    /// <summary>
    /// Validates the embedded images and their count.
    /// </summary>
    /// <param name="validationContext">The context in which the validation is performed.</param>
    /// <returns>A collection of validation errors, or an empty collection when the images are valid.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="validationContext"/> is <see langword="null"/>.</exception>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        ArgumentNullException.ThrowIfNull(validationContext);

        return ValidateImages();
    }

    IEnumerable<ValidationResult> IRecordValidatable.ValidateRecord()
    {
        return ValidateImages();
    }

    private IEnumerable<ValidationResult> ValidateImages()
    {
        if (Images is null)
        {
            yield return new ValidationResult("Images are required.", [nameof(Images)]);
            yield break;
        }

        if (Images.Count > Maximum.ImagesInPost)
        {
            yield return new ValidationResult(
                $"An embedded image set cannot contain more than {Maximum.ImagesInPost} images.",
                [nameof(Images)]);
        }

        int index = 0;
        foreach (EmbeddedImage? image in Images)
        {
            foreach (ValidationResult result in RecordValidation.Nested(image, $"{nameof(Images)}[{index}]"))
            {
                yield return result;
            }

            index++;
        }
    }

    /// <summary>
    /// Determines whether the specified <see cref="EmbeddedImages"/> is equal to the current instance.
    /// </summary>
    /// <param name="other">The <see cref="EmbeddedImages"/> to compare against the current instance.</param>
    /// <returns><see langword="true" /> if <paramref name="other"/> is equal to the current instance, otherwise <see langword="false" />.</returns>
    /// <remarks>
    /// <para><see cref="Images"/> is compared by its contents rather than by reference.</para>
    /// </remarks>
    public virtual bool Equals(EmbeddedImages? other)
    {
        if (ReferenceEquals(this, other))
        {
            return true;
        }

        if (other is null || EqualityContract != other.EqualityContract)
        {
            return false;
        }

        return base.Equals(other) && CollectionComparison.SequenceEquals(Images, other.Images);
    }

    /// <summary>
    /// Returns the hash code for the current instance.
    /// </summary>
    /// <returns>The hash code for the current instance.</returns>
    public override int GetHashCode()
    {
        HashCode hashCode = new();

        hashCode.Add(base.GetHashCode());
        CollectionComparison.AddSequence(ref hashCode, Images);

        return hashCode.ToHashCode();
    }
}