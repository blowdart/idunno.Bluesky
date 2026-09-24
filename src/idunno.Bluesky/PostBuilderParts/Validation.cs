// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace idunno.Bluesky;
#pragma warning restore IDE0130 // Namespace does not match folder structure

public sealed partial class PostBuilder
{
    /// <summary>
    /// Determines whether this instance of <see cref="PostBuilder"/> is valid and can be converted to a <see cref="Post"/>.
    /// </summary>
    /// <returns><see langword="true"/> if the post builder is valid; otherwise, <see langword="false"/>.</returns>
    public bool IsValid()
    {
        return !ValidationErrors().Any();
    }

    /// <summary>
    /// Returns a list of validation errors, if any, for this instance of <see cref="PostBuilder"/>.
    /// </summary>
    /// <returns>An enumeration of validation errors, if any.</returns>
    /// <remarks>
    /// <para>
    ///   The builder is snapshotted once under its lock and the rules are applied to that snapshot, so the errors
    ///   describe a state the builder actually had. The errors are produced before this returns, so they describe the
    ///   builder as it was when this was called, not as it is when the result is enumerated.
    /// </para>
    /// </remarks>
    public IEnumerable<string> ValidationErrors()
    {
        // Every value the rules depend on is read in a single pass. Reading the properties one at a time would take a
        // separate snapshot for each rule, so a builder being mutated on another thread could be judged against a
        // combination of values it never actually held, and text going away between the HasText check and the Text
        // read would dereference null.
        bool hasText;
        bool hasImages;
        bool hasGalleryImages;
        bool hasEmbed;
        bool hasVideo;
        bool hasLabels;
        int imageCount;
        int length;
        string text;

        lock (_syncLock)
        {
            text = _post.Text ?? string.Empty;
            hasText = text.Length != 0;
            length = _post.Length;
            imageCount = _embeddedImages.Count;
            hasImages = imageCount > 0;
            hasGalleryImages = _embeddedGalleryImages.Count > 0;
            hasEmbed = _post.EmbeddedRecord is not null;
            hasVideo = _embeddedVideo is not null;
            hasLabels = _post.Labels is not null;
        }

        List<string> errors = [];

        if (!hasText && !hasImages && !hasEmbed && !hasVideo)
        {
            errors.Add(Properties.Resources.EmptyPostTextValidationError);
        }

        if (hasVideo && hasImages)
        {
            errors.Add(Properties.Resources.PostCannotHaveImagesAndVideoValidationError);
        }

        if (hasImages && imageCount > Maximum.GalleryItems)
        {
            errors.Add(string.Format(null, s_postHasTooManyImages, Maximum.GalleryItems));
        }

        if (hasImages && hasGalleryImages)
        {
            errors.Add(Properties.Resources.PostBuilderCannotHaveImagesAndGalleryImages);
        }

        if (hasText && length > MaxCapacity)
        {
            errors.Add(string.Format(null, s_postTextExceedsMaxLengthValidationError, MaxCapacity));
        }

        if (hasText && text.GetGraphemeLength() > MaxCapacityGraphemes)
        {
            errors.Add(string.Format(null, s_postTextExceedsMaxLengthInGraphemesValidationError, MaxCapacityGraphemes));
        }

        if (!hasImages && !hasVideo && hasLabels)
        {
            errors.Add(Properties.Resources.PostHasLabelsButNoMediaValidationError);
        }

        return errors;
    }
}