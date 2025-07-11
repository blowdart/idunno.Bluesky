// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;

using System.Text.Json;
using System.Text.Json.Serialization;

using idunno.Bluesky.Notifications.PreferenceTypes;

namespace idunno.Bluesky.Notifications
{
    /// <summary>
    /// Encapsulates a user's notification settings.
    /// </summary>
    /// <param name="Chat">Notification settings for chat.</param>
    /// <param name="Follow">Notification settings for follows.</param>
    /// <param name="LikeViaRepost">Notification settings for likes of reposts.</param>
    /// <param name="Mention">Notification settings for mentions.</param>
    /// <param name="Quote">Notification settings for quotes of posts.</param>
    /// <param name="Reply">Notification settings for replies.</param>
    /// <param name="Repost">Notification settings for reposts.</param>
    /// <param name="RepostViaRepost">Notification settings for when a user reposts a post via your repost.</param>
    /// <param name="StarterPackJoined">Notification settings for when a user joins via a starter pack the user created.</param>
    /// <param name="SubscribedPost">Notifications for when a user whose posts you subscribed to posts or replies.</param>
    /// <param name="Unverified">Undocumented</param>
    /// <param name="Verified">Undocumented</param>
    public sealed record Preferences(
        ChatPreference Chat,
        FilterablePreference Follow,
        FilterablePreference LikeViaRepost,
        FilterablePreference Mention,
        FilterablePreference Quote,
        FilterablePreference Reply,
        FilterablePreference Repost,
        FilterablePreference RepostViaRepost,
        NonFilterablePreference StarterPackJoined,
        NonFilterablePreference SubscribedPost,
        NonFilterablePreference Unverified,
        NonFilterablePreference Verified)
    {
        /// <summary>
        /// A list of keys and element data that do not map to any strongly typed properties.
        /// </summary>
        [NotNull]
        [SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "Must be writable for JSON deserialization.")]
        [JsonExtensionData]
        public IDictionary<string, JsonElement>? ExtensionData { get; set; } = new Dictionary<string, JsonElement>();
    }

}
