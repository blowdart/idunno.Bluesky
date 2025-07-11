// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace idunno.Bluesky.Notifications.PreferenceTypes
{
    /// <summary>
    /// Encapsulates a non-filterable notification preference
    /// </summary>
    public sealed record NonFilterablePreference
    {
        /// <summary>
        /// Creates a new instance of <see cref="NonFilterablePreference"/>.
        /// </summary>
        public NonFilterablePreference()
        {
        }

        /// <summary>
        /// Creates a new instance of <see cref="NonFilterablePreference"/>.
        /// </summary>
        /// <param name="list">A flag indicating whether the notification type should be shown in the user's notifications list.</param>
        /// <param name="push">A flag indicating whether the notification type should be pushed.</param>
        [JsonConstructor]
        public NonFilterablePreference(bool list, bool push)
        {
            List = list;
            Push = push;
        }

        /// <summary>
        /// Gets or sets a flag indicating whether the notification type should be shown in the user's notifications list.
        /// </summary>
        public bool List { get; set; }

        /// <summary>
        /// Gets or sets a flag indicating whether the notification type should be pushed.
        /// </summary>
        public bool Push { get; set; }
    }
}
