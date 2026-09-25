// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace idunno.Bluesky.Actor;
#pragma warning restore IDE0130 // Namespace does not match folder structure

/// <summary>
/// Encapsulates a user's muted words preference.
/// </summary>
public record MutedWordPreferences : Preference
{
    /// <summary>
    /// Creates a new instance of <see cref="MutedWordPreferences"/> from the provided <paramref name="items"/>
    /// </summary>
    /// <param name="items">A list of <see cref="MutedWord"/>s.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="items"/> is <see langword="null"/>.</exception>
    public MutedWordPreferences(IReadOnlyList<MutedWord> items) => Items = items;

    /// <summary>
    /// A list of muted words and their configuration.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown when the value set is <see langword="null"/>.</exception>
    public IReadOnlyList<MutedWord> Items
    {
        get;

        init
        {
            ArgumentNullException.ThrowIfNull(value);

            field = new List<MutedWord>(value).AsReadOnly();
        }
    }
}