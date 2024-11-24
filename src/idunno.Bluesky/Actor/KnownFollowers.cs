// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.Bluesky.Actor
{
    /// <summary>
    /// Followers that the current user has in common with the actor whose profile has been retrieved.
    /// </summary>
    public sealed record KnownFollowers
    {
        /// <summary>
        /// Creates a new instance of <see cref="KnownFollowers"/>
        /// </summary>
        /// <param name="count">The count of known followers.</param>
        /// <param name="followers">The list of known followers the authenticated user shares with another actor.</param>
        [JsonConstructor]
        public KnownFollowers(int count, IReadOnlyList<ProfileViewBasic> followers)
        {
            Count = count;
            Followers = new List<ProfileViewBasic>(followers).AsReadOnly();
        }

        /// <summary>
        /// The count of known followers.
        /// </summary>
        [JsonInclude]
        public int Count { get; init; }

        /// <summary>
        /// The list of known followers the authenticated user shares with another actor.
        /// </summary>
        [JsonInclude]
        public IReadOnlyList<ProfileViewBasic> Followers { get; init; }
    }
}
