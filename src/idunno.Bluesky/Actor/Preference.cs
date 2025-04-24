// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace idunno.Bluesky.Actor
{
    /// <summary>
    /// Base record for actor preferences.
    /// </summary>
    [JsonPolymorphic(UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FallBackToNearestAncestor, IgnoreUnrecognizedTypeDiscriminators = true)]
    [JsonDerivedType(typeof(LabelersPreference), typeDiscriminator: PreferenceTypeDiscriminators.Labelers)]
    [JsonDerivedType(typeof(ContentLabelPreference), typeDiscriminator: PreferenceTypeDiscriminators.ContentLabel)]
    [JsonDerivedType(typeof(InterestsPreference), typeDiscriminator: PreferenceTypeDiscriminators.Interests)]
    [JsonDerivedType(typeof(SavedFeedPreference), typeDiscriminator: PreferenceTypeDiscriminators.SavedFeeds)]
    [JsonDerivedType(typeof(SavedFeedPreference2), typeDiscriminator: PreferenceTypeDiscriminators.SavedFeeds2)]
    [JsonDerivedType(typeof(HiddenPostsPreferences), typeDiscriminator: PreferenceTypeDiscriminators.HiddenPosts)]
    [JsonDerivedType(typeof(AdultContentPreference), typeDiscriminator: PreferenceTypeDiscriminators.AdultContent)]
    [JsonDerivedType(typeof(PersonalDetailsPreference), typeDiscriminator: PreferenceTypeDiscriminators.PersonalDetails)]
    [JsonDerivedType(typeof(FeedViewPreference), typeDiscriminator: PreferenceTypeDiscriminators.FeedView)]
    [JsonDerivedType(typeof(MutedWordPreferences), typeDiscriminator: PreferenceTypeDiscriminators.MutedWords)]
    [JsonDerivedType(typeof(ThreadViewPreference), typeDiscriminator: PreferenceTypeDiscriminators.ThreadView)]
    [JsonDerivedType(typeof(BlueskyAppStatePreference), typeDiscriminator: PreferenceTypeDiscriminators.BlueskyAppState)]
    [JsonDerivedType(typeof(InteractionPreferences), typeDiscriminator: PreferenceTypeDiscriminators.PostInteraction)]
    [JsonDerivedType(typeof(VerificationPreferences), typeDiscriminator: PreferenceTypeDiscriminators.Verification)]
    public record Preference
    {
        /// <summary>
        /// Creates a new instance of <see cref="Preference"/>.
        /// </summary>
        [JsonConstructor]
        public Preference()
        {
        }

        /// <summary>
        /// Json overflow data for properties and elements that cannot be mapped to a strongly typed class.
        /// </summary>
        [JsonExtensionData]
        [SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "Standard pattern for JSON Extension Data")]
        public Dictionary<string, JsonElement>? ExtensionData { get; set; }
    }
}
