// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;

using idunno.AtProto;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace idunno.Bluesky.Actor;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// A <see cref="Preference"/> containing tags which describe the account owner's interests gathered during onboarding.
/// </summary>
/// <remarks>
/// <para>See <see href="https://github.com/bluesky-social/atproto/blob/main/lexicons/app/bsky/actor/defs.json" /> for the definition.</para>
/// </remarks>
public sealed record InterestsPreference : Preference
{
    /// <summary>
    /// Creates a new instance of <see cref="InterestsPreference"/>.
    /// </summary>
    /// <param name="tags">A list of tags which describe the account owner's interests gathered during onboarding.</param>
    /// <exception cref="ArgumentNullException">Thrown when any entry in <paramref name="tags"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="tags"/> contains more than <see cref="Maximum.InterestTags"/> tags, or when any tag is
    /// longer than <see cref="Maximum.TagLengthInBytes"/> bytes or <see cref="Maximum.TagLengthInGraphemes"/> graphemes.
    /// </exception>
    public InterestsPreference(ICollection<string> tags)
    {
        if (tags is null)
        {
            Tags = [];
        }
        else
        {
            ArgumentOutOfRangeException.ThrowIfGreaterThan(tags.Count, Maximum.InterestTags);

            foreach (string tag in tags)
            {
                ArgumentNullException.ThrowIfNull(tag);
                ArgumentOutOfRangeException.ThrowIfGreaterThan(tag.GetUtf8Length(), Maximum.TagLengthInBytes);
                ArgumentOutOfRangeException.ThrowIfGreaterThan(tag.GetGraphemeLength(), Maximum.TagLengthInGraphemes);
            }

            Tags = [.. tags];
        }
    }

    /// <summary>
    /// A list of tags which describe the account owner's interests gathered during onboarding.
    /// </summary>
    [SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "Set as writable to allow for ease of full replacement.")]
    public ICollection<string> Tags { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the account owner last updated their interests
    /// </summary>
    public DateTimeOffset? UpdatedAt { get; set; }

}