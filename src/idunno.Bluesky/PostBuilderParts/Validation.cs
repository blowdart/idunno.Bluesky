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
    public IEnumerable<string> ValidationErrors()
    {
        if (!HasText && !HasImages && !HasEmbed && !HasVideo)
        {
            yield return Properties.Resources.EmptyPostTextValidationError;
        }

        if (HasVideo && HasImages)
        {
            yield return Properties.Resources.PostCannotHaveImagesAndVideoValidationError;
        }

        if (HasText && Length > MaxCapacity)
        {
            yield return string.Format(null, s_postTextExceedsMaxLengthValidationError, MaxCapacity);
        }

        if (HasText && Text.GetGraphemeLength() > MaxCapacityGraphemes)
        {
            yield return string.Format(null, s_postTextExceedsMaxLengthInGraphemesValidationError, MaxCapacityGraphemes);
        }

        if (!HasImages && ! HasVideo && _post.Labels is not null)
        {
            yield return Properties.Resources.PostHasLabelsButNoMediaValidationError;
        }
    }
}
