// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.Bluesky.Actor
{
    /// <summary>
    /// Read-only preference containing value(s) inferred from the user's declared birthdate.
    /// Absence of this preference object in the response indicates that the user has not made a declaration
    /// </summary>
    public sealed record DeclaredAgePreference : Preference
    {
        /// <summary>
        /// Creates a new instance of <see cref="DeclaredAgePreference"/>.
        /// </summary>
        /// <param name="isOverAge13">Flag indicating if the user has declared that they are over 13 years of age.</param>
        /// <param name="isOverAge16">Flag indicating if the user has declared that they are over 16 years of age.</param>
        /// <param name="isOverAge18">Flag indicating if the user has declared that they are over 18 years of age.</param>
        public DeclaredAgePreference(bool isOverAge13, bool isOverAge16, bool isOverAge18)
        {
            IsOverAge13 = isOverAge13;
            IsOverAge16 = isOverAge16;
            IsOverAge18 = isOverAge18;
        }

        /// <summary>
        /// Gets a flag indicating if the user has declared that they are over 13 years of age.
        /// </summary>
        public bool IsOverAge13 { get; init; }

        /// <summary>
        /// Gets a flag indicating if the user has declared that they are over 16 years of age.
        /// </summary>
        public bool IsOverAge16 { get; init; }

        /// <summary>
        /// Gets a flag indicating if the user has declared that they are over 18 years of age.
        /// </summary>
        public bool IsOverAge18 { get; init; }
    }
}
