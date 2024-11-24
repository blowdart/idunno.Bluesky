// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.Bluesky.Actor
{
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
        public InterestsPreference(IReadOnlyList<string> tags)
        {
            if (tags is null)
            {
                Tags = new List<string>().AsReadOnly();
            }
            else
            {
                Tags = new List<string>(tags).AsReadOnly();
            }
        }

        /// <summary>
        /// A list of tags which describe the account owner's interests gathered during onboarding.
        /// </summary>
        public IReadOnlyList<string> Tags { get; set; }
    }
}
