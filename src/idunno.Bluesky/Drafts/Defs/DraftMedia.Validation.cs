// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.ComponentModel.DataAnnotations;
using System.Text.Json;

using idunno.Bluesky.Json;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace idunno.Bluesky.Drafts;
#pragma warning restore IDE0130 // Namespace does not match folder structure

public partial record DraftEmbedImage : IValidatableObject, IRecordValidatable
{
    internal DraftEmbedImage(TolerantDraftJson.DraftImageJsonValue value)
    {
        LocalRef = TolerantDraftJson.ReadLocalRef(value.LocalRef);
        _altText = value.AltText;
    }

    /// <summary>Validates the draft image and its local reference.</summary>
    /// <param name="validationContext">The context in which validation is performed.</param>
    /// <returns>A collection of validation failures, or an empty collection when the image is valid.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="validationContext"/> is <see langword="null"/>.</exception>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        ArgumentNullException.ThrowIfNull(validationContext);

        return ValidateImage();
    }

    IEnumerable<ValidationResult> IRecordValidatable.ValidateRecord() => ValidateImage();

    private IEnumerable<ValidationResult> ValidateImage() =>
        RecordValidation.Nested(LocalRef, nameof(LocalRef))
            .Concat(RecordValidation.Text(AltText, nameof(AltText), int.MaxValue, Maximum.DraftEmbedAltTextLengthInGraphemes));
}

public partial record DraftEmbedVideo : IValidatableObject, IRecordValidatable
{
    internal DraftEmbedVideo(TolerantDraftJson.DraftVideoJsonValue value)
    {
        LocalRef = TolerantDraftJson.ReadLocalRef(value.LocalRef);
        _altText = value.AltText;
        Captions = TolerantDraftJson.ReadCollection(value.Captions, TolerantDraftJson.ReadCaption);
    }

    /// <summary>Validates the draft video, captions and local reference.</summary>
    /// <param name="validationContext">The context in which validation is performed.</param>
    /// <returns>A collection of validation failures, or an empty collection when the video is valid.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="validationContext"/> is <see langword="null"/>.</exception>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        ArgumentNullException.ThrowIfNull(validationContext);

        return ValidateVideo();
    }

    IEnumerable<ValidationResult> IRecordValidatable.ValidateRecord() => ValidateVideo();

    private IEnumerable<ValidationResult> ValidateVideo() =>
        RecordValidation.Nested(LocalRef, nameof(LocalRef))
            .Concat(RecordValidation.Text(AltText, nameof(AltText), int.MaxValue, Maximum.DraftEmbedAltTextLengthInGraphemes))
            .Concat(RecordValidation.Collection(Captions, nameof(Captions), Maximum.DraftEmbedVideoCaptions));
}

public partial record DraftEmbedCaption : IValidatableObject, IRecordValidatable
{
    internal DraftEmbedCaption(TolerantDraftJson.DraftCaptionJsonValue value)
    {
        _lang = value.Lang ?? throw new JsonException("A caption language is required.");
        _content = value.Content ?? throw new JsonException("Caption content is required.");
    }

    /// <summary>Validates the draft caption language and content.</summary>
    /// <param name="validationContext">The context in which validation is performed.</param>
    /// <returns>A collection of validation failures, or an empty collection when the caption is valid.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="validationContext"/> is <see langword="null"/>.</exception>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        ArgumentNullException.ThrowIfNull(validationContext);

        return ValidateCaption();
    }

    IEnumerable<ValidationResult> IRecordValidatable.ValidateRecord() => ValidateCaption();

    private IEnumerable<ValidationResult> ValidateCaption()
    {
        if (string.IsNullOrWhiteSpace(Lang))
        {
            yield return new ValidationResult("A caption language is required.", [nameof(Lang)]);
        }

        if (Content is null)
        {
            yield return new ValidationResult("Caption content is required.", [nameof(Content)]);
        }

        foreach (ValidationResult result in RecordValidation.Text(Content, nameof(Content), Maximum.DraftEmbedCaptionContentLengthInBytes, int.MaxValue))
        {
            yield return result;
        }
    }
}

public partial record DraftEmbedLocalRef : IValidatableObject, IRecordValidatable
{
    internal DraftEmbedLocalRef(TolerantDraftJson.DraftLocalRefJsonValue value)
    {
        _path = value.Path ?? throw new JsonException("A local reference path is required.");
    }

    /// <summary>Validates the draft local file reference.</summary>
    /// <param name="validationContext">The context in which validation is performed.</param>
    /// <returns>A collection of validation failures, or an empty collection when the path is valid.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="validationContext"/> is <see langword="null"/>.</exception>
    /// <remarks><para>Validation checks only the stored path text, not file existence or permission to access it.</para></remarks>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        ArgumentNullException.ThrowIfNull(validationContext);

        return ValidatePath();
    }

    IEnumerable<ValidationResult> IRecordValidatable.ValidateRecord() => ValidatePath();

    private IEnumerable<ValidationResult> ValidatePath()
    {
        if (string.IsNullOrWhiteSpace(Path))
        {
            yield return new ValidationResult("A nonempty local path is required.", [nameof(Path)]);
        }

        foreach (ValidationResult result in RecordValidation.Text(Path, nameof(Path), Maximum.DraftEmbedLocalRefPathLengthInBytes, int.MaxValue))
        {
            yield return result;
        }
    }
}

public partial record DraftEmbedGallery : IValidatableObject, IRecordValidatable
{
    /// <summary>Validates the draft gallery items and their schema-level count limit.</summary>
    /// <param name="validationContext">The context in which validation is performed.</param>
    /// <returns>A collection of validation failures, or an empty collection when the gallery is valid.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="validationContext"/> is <see langword="null"/>.</exception>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        ArgumentNullException.ThrowIfNull(validationContext);

        return ValidateGallery();
    }

    IEnumerable<ValidationResult> IRecordValidatable.ValidateRecord() => ValidateGallery();

    private IEnumerable<ValidationResult> ValidateGallery()
    {
        if (Items is null)
        {
            yield return new ValidationResult("Gallery items are required.", [nameof(Items)]);
            yield break;
        }

        foreach (ValidationResult result in RecordValidation.Collection(Items, nameof(Items), 20))
        {
            yield return result;
        }
    }
}

public partial record DraftEmbedExternal : IValidatableObject, IRecordValidatable
{
    /// <summary>Validates the draft external URI.</summary>
    /// <param name="validationContext">The context in which validation is performed.</param>
    /// <returns>A collection of validation failures, or an empty collection when the URI is valid.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="validationContext"/> is <see langword="null"/>.</exception>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        ArgumentNullException.ThrowIfNull(validationContext);

        return ValidateUri();
    }

    IEnumerable<ValidationResult> IRecordValidatable.ValidateRecord() => ValidateUri();

    private IEnumerable<ValidationResult> ValidateUri()
    {
        if (Uri is null || !Uri.IsAbsoluteUri)
        {
            yield return new ValidationResult("An absolute external URI is required.", [nameof(Uri)]);
        }
    }
}

public partial record DraftEmbedRecord : IValidatableObject, IRecordValidatable
{
    /// <summary>Validates the draft embedded record reference.</summary>
    /// <param name="validationContext">The context in which validation is performed.</param>
    /// <returns>A collection of validation failures, or an empty collection when the reference is valid.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="validationContext"/> is <see langword="null"/>.</exception>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        ArgumentNullException.ThrowIfNull(validationContext);

        return ValidateReference();
    }

    IEnumerable<ValidationResult> IRecordValidatable.ValidateRecord() => ValidateReference();

    private IEnumerable<ValidationResult> ValidateReference()
    {
        if (Record is null)
        {
            yield return new ValidationResult("A record reference is required.", [nameof(Record)]);
        }
    }
}
