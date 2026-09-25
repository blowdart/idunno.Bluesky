// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace idunno.Bluesky.Actor;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Personal details about the account owner.
/// </summary>
public record PersonalDetailsPreference : Preference
{
    /// <summary>
    /// Creates a new instance of <see cref="PersonalDetailsPreference"/>.
    /// </summary>
    /// <param name="birthDate">The birth date of account owner.</param>
    public PersonalDetailsPreference(DateTimeOffset? birthDate)
    {
        BirthDate = birthDate;
    }

    /// <summary>
    /// Gets the birth date of account owner.
    /// </summary>
    public DateTimeOffset? BirthDate { get; init; }
}