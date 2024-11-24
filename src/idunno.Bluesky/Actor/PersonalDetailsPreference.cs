// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.Bluesky.Actor
{
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
}
