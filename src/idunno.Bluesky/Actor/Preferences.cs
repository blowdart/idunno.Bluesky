// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Collections.ObjectModel;

using idunno.AtProto;

namespace idunno.Bluesky.Actor
{
    /// <summary>
    /// An actor's Bluesky preferences.
    /// </summary>
    public class Preferences : ReadOnlyCollection<Preference>
    {
        /// <summary>
        /// Creates a new instance of <see cref="Preferences"/>.
        /// </summary>
        /// <param name="list">A list of actor preferences.</param>
        /// <param name="enableBlueskyModerationLabeler">A flag indicating whether the Bluesky moderation labeler should be enabled as part of the actor's subscribed labelers.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="list"/> is <see langword="null"/>.</exception>
        public Preferences(IList<Preference> list, bool enableBlueskyModerationLabeler = true) : base(list)
        {
            ArgumentNullException.ThrowIfNull(list);

            List<Did> labelerPreferenceList = [];
            List<ContentLabelPreference> contentLabelPreferenceList = [];
            List<SavedFeedPreference> savedFeedPreferenceList = [];
            List<SavedFeedPreference2> savedFeedPreference2List = [];
            List<AtUri> hiddenPostUris = [];
            List<string> interestTags = [];
            Dictionary<string, FeedViewPreference> feedViewPreferences = [];
            List<MutedWord> mutedWords = [];

            foreach (Preference preference in this)
            {
                switch (preference)
                {
                    case LabelersPreference labelersPreference:
                        foreach (LabelerPreference labelPreference in labelersPreference.Labelers)
                        {
                            labelerPreferenceList.Add(labelPreference.Did);
                        }
                        break;

                    case ContentLabelPreference contentLabelPreference:
                        contentLabelPreferenceList.Add(contentLabelPreference);
                        break;

                    case SavedFeedPreference savedFeedPreference:
                        savedFeedPreferenceList.Add(savedFeedPreference);
                        break;

                    case SavedFeedPreferences2 savedFeedPreferences2:
                        savedFeedPreference2List.AddRange(savedFeedPreferences2.Items);
                        break;

                    case HiddenPostsPreferences hiddenPostsPreferences:
                        hiddenPostUris.AddRange(hiddenPostsPreferences.Items);
                        break;

                    case AdultContentPreference adultContentPreference:
                        AdultContentPreference = adultContentPreference;
                        break;

                    case PersonalDetailsPreference personalDetailsPreference:
                        PersonalDetailsPreference = personalDetailsPreference;
                        break;

                    case InterestsPreference interestsPreference:
                        interestTags.AddRange(interestsPreference.Tags);
                        break;

                    case FeedViewPreference feedViewPreference:
                        feedViewPreferences.Add(feedViewPreference.Feed, feedViewPreference);
                        break;

                    case MutedWordPreferences mutedWordPreferences:
                        mutedWords.AddRange(mutedWordPreferences.Items);
                        break;

                    case ThreadViewPreference threadViewPreference:
                        ThreadViewPreference = threadViewPreference;
                        break;

                    case InteractionPreferences postInteractionSettingsPreference:
                        InteractionPreferences = postInteractionSettingsPreference;
                        break;

                    case VerificationPreferences verificationPreferences:
                        VerificationPreferences = verificationPreferences;
                        break;

                    case DeclaredAgePreference declaredAgePreference:
                        DeclaredAgePreference = declaredAgePreference;
                        break;

                    // As this is only meant for official Bluesky apps we'll just skip doing anything with it
                    // and not expose it as a Preferences property.
                    case BlueskyAppStatePreference:
                        break;

                    default:
                        break;
                }
            }

            if (enableBlueskyModerationLabeler && !labelerPreferenceList.Contains(WellKnownDistributedIdentifiers.BlueskyModerationLabeler))
            {
                labelerPreferenceList.Insert(0, WellKnownDistributedIdentifiers.BlueskyModerationLabeler);
            }

            SubscribedLabelers = labelerPreferenceList.AsReadOnly();
            ContentLabelPreferences = contentLabelPreferenceList.AsReadOnly();
            SavedFeedPreferences = savedFeedPreferenceList.AsReadOnly();
            SavedFeedPreference2s = savedFeedPreference2List.AsReadOnly();
            HiddenPosts = hiddenPostUris.AsReadOnly();
            InterestTags = interestTags;
            FeedViewPreferences = feedViewPreferences.AsReadOnly();
            MutedWords = mutedWords.AsReadOnly();
        }

        /// <summary>
        /// Creates a new instance of <see cref="Preferences"/>.
        /// </summary>
        /// <param name="enableBlueskyModerationLabeler">A flag indicating whether the Bluesky moderation labeler should be enabled as part of the actor's subscribed labelers.</param>
        public Preferences(bool enableBlueskyModerationLabeler = true) : this([], enableBlueskyModerationLabeler)
        {
        }

        /// <summary>
        /// Creates a new instance of <see cref="Preferences"/>.
        /// </summary>
        /// <param name="preferences">An enumerable collection of actor preferences.</param>
        /// <param name="enableBlueskyModerationLabeler">A flag indicating whether the Bluesky moderation labeler should be enabled as part of the actor's subscribed labelers.</param>
        public Preferences(IEnumerable<Preference> preferences, bool enableBlueskyModerationLabeler = true) : this([.. preferences], enableBlueskyModerationLabeler)
        {
        }

        /// <summary>
        /// A list of labeler <see cref="Did"/>s the actor has subscribed to.
        /// </summary>
        public IReadOnlyList<Did> SubscribedLabelers { get; }

        /// <summary>
        /// A list of <see cref="ContentLabelPreference"/>s the actor has configured.
        /// </summary>
        public IReadOnlyList<ContentLabelPreference> ContentLabelPreferences { get; }

        /// <summary>
        /// A list of the actor's <see cref="SavedFeedPreference"/>s.
        /// </summary>
        public IReadOnlyList<SavedFeedPreference> SavedFeedPreferences { get; }

        /// <summary>
        /// A list of the actor's <see cref="SavedFeedPreference2"/>s.
        /// </summary>
        public IReadOnlyList<SavedFeedPreference2> SavedFeedPreference2s { get; }

        /// <summary>
        /// A list of <see cref="AtUri"/>s of posts the account owner has hidden.
        /// </summary>
        public IReadOnlyList<AtUri> HiddenPosts { get; }

        /// <summary>
        /// The actor's adult content preferences.
        /// </summary>
        public AdultContentPreference? AdultContentPreference { get; }

        /// <summary>
        /// The actor's personal details preferences.
        /// </summary>
        public PersonalDetailsPreference? PersonalDetailsPreference { get; }

        /// <summary>
        /// A list of tags which describe the account owner's interests gathered during onboarding.
        /// </summary>
        public IReadOnlyList<string>? InterestTags { get; }

        /// <summary>
        /// A dictionary of feeds and their <see cref="FeedViewPreference"/> for the account owner.
        /// </summary>
        public IReadOnlyDictionary<string, FeedViewPreference> FeedViewPreferences { get; }

        /// <summary>
        /// A list of muted word properties for the account owner.
        /// </summary>
        public IList<MutedWord> MutedWords { get; }

        /// <summary>
        /// Preferences for displaying how threads are viewed.
        /// </summary>
        public ThreadViewPreference? ThreadViewPreference { get; }

        /// <summary>
        /// Default gate settings for posts and threads.
        /// </summary>
        public InteractionPreferences? InteractionPreferences { get; }

        /// <summary>
        /// Preferences for how verified accounts appear in an app.
        /// </summary>
        public VerificationPreferences? VerificationPreferences { get; }

        /// <summary>
        /// Read-only preference containing value(s) inferred from the user's declared birthdate.
        /// Absence of this preference object in the response indicates that the user has not made a declaration.
        /// </summary>
        public DeclaredAgePreference? DeclaredAgePreference { get; }
    }
}
