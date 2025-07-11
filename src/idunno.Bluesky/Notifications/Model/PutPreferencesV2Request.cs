// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.Bluesky.Notifications.PreferenceTypes;

namespace idunno.Bluesky.Notifications.Model
{
    internal sealed record PutPreferencesV2Request(
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
        public PutPreferencesV2Request(Preferences preferences) :this(
            Chat: preferences.Chat,
            Follow: preferences.Follow,
            LikeViaRepost: preferences.LikeViaRepost,
            Mention: preferences.Mention,
            Quote: preferences.Quote,
            Reply: preferences.Reply,
            Repost: preferences.Repost,
            RepostViaRepost: preferences.RepostViaRepost,
            StarterPackJoined: preferences.StarterPackJoined,
            SubscribedPost: preferences.SubscribedPost,
            Unverified: preferences.Unverified,
            Verified: preferences.Verified)
        {
        }
    }
}
