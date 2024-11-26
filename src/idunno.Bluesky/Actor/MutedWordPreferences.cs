// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.Bluesky.Actor
{
    /// <summary>
    /// Encapsulates a user's muted words preference.
    /// </summary>
    public record MutedWordPreferences : Preference
    {
        /// <summary>
        /// Creates a new instance of <see cref="MutedWordPreferences"/> from the provided <paramref name="items"/>
        /// </summary>
        /// <param name="items">A list of <see cref="MutedWord"/>s.</param>
        public MutedWordPreferences(IReadOnlyList<MutedWord> items)
        {
            if (items == null)
            {
                Items = new List<MutedWord>().AsReadOnly();
            }
            else
            {
                Items = new List<MutedWord>(items).AsReadOnly();
            }
        }

        /// <summary>
        /// A list of muted words and their configuration.
        /// </summary>
        public IReadOnlyList<MutedWord> Items { get; init; }
    }
}
