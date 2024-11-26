// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.Bluesky.Actor
{
    /// <summary>
    /// Preferences for displaying how threads are viewed.
    /// </summary>
    public record ThreadViewPreference : Preference
    {
        /// <summary>
        /// Creates a new instance of <see cref="ThreadViewPreference"/>.
        /// </summary>
        /// <param name="sortingMode">The user's preferred sorting mode for threads.</param>
        /// <param name="prioritizeFollowedUsers">Flag indicating whether to show followed users at the top of all replies.</param>
        [JsonConstructor]
        public ThreadViewPreference(ThreadSortingMode sortingMode, bool prioritizeFollowedUsers)
        {
            SortingMode = sortingMode;
            PrioritizeFollowedUsers = prioritizeFollowedUsers;
        }

        /// <summary>
        /// The user's preferred sorting mode for threads.
        /// </summary>
        [JsonInclude]
        public ThreadSortingMode SortingMode { get; init; }

        /// <summary>
        /// Flag indicating whether to show followed users at the top of all replies.
        /// </summary>
        [JsonInclude]
        public bool PrioritizeFollowedUsers { get; init; }
        
    }

    /// <summary>
    /// Groups of users to apply the muted word to.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum ThreadSortingMode
    {
        /// <summary>
        /// Show oldest replies first.
        /// </summary>
        Oldest,

        /// <summary>
        /// Show newest replies first.
        /// </summary>
        Newest,

        /// <summary>
        /// Show the replies with the most likes first.
        /// </summary>
        [JsonStringEnumMemberName("most-likes")]
        MostLikes,

        /// <summary>
        /// Randomize the thread replies.
        /// </summary>
        Random
    }
}
