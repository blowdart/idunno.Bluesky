// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;

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
    public InterestsPreference(ICollection<string> tags)
    {
        if (tags is null)
        {
            Tags = [];
        }
        else
        {
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