// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Collections.ObjectModel;

using idunno.AtProto;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace idunno.Bluesky.Actor;
#pragma warning restore IDE0130 // Namespace does not match folder structure

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
        List<SavedFeed> savedFeedPreferenceV2List = [];
        List<AtUri> hiddenPostUris = [];
        List<MutedWord> mutedWords = [];
        Dictionary<string, FeedViewPreference> feedViewPreferences = [];

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

                case SavedFeedsPreference savedFeedPreference:
                    SavedFeedsPreference = savedFeedPreference;
                    break;

                case SavedFeedPreferencesV2 savedFeedPreferencesV2:
                    savedFeedPreferenceV2List.AddRange(savedFeedPreferencesV2.Items);
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
                    Interests = interestsPreference;
                    break;

                case FeedViewPreference feedViewPreference:
                    if (!feedViewPreferences.TryAdd(feedViewPreference.Feed, feedViewPreference))
                    {
                        feedViewPreferences[feedViewPreference.Feed] = feedViewPreference;
                    }
                    break;

                case MutedWordPreferences mutedWordPreferences:
                    mutedWords.AddRange(mutedWordPreferences.Items);
                    break;

                case ThreadViewPreference threadViewPreference:
                    ThreadViewPreference = threadViewPreference;
                    break;

                case PostInteractionSettingsPreferences postInteractionSettingsPreference:
                    PostInteractionSettingsPreferences = postInteractionSettingsPreference;
                    break;

                case VerificationPreferences verificationPreferences:
                    VerificationPreferences = verificationPreferences;
                    break;

                case DeclaredAgePreference declaredAgePreference:
                    DeclaredAgePreference = declaredAgePreference;
                    break;

                case LiveEventPreferences liveEventPreferences:
                    LiveEventPreferences = liveEventPreferences;
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
        SavedFeedsPreferenceV2 = savedFeedPreferenceV2List.AsReadOnly();
        HiddenPosts = hiddenPostUris.AsReadOnly();
        MutedWords = mutedWords.AsReadOnly();
        FeedViewPreferences = feedViewPreferences.AsReadOnly();
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
    /// A list of the actor's <see cref="SavedFeedsPreference"/>s.
    /// </summary>
    public SavedFeedsPreference? SavedFeedsPreference { get; }

    /// <summary>
    /// A list of the actor's <see cref="Actor.SavedFeed"/>s.
    /// </summary>
    public IReadOnlyList<SavedFeed> SavedFeedsPreferenceV2 { get; }

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
    public InterestsPreference? Interests { get; }

    /// <summary>
    /// A list of feeds and their <see cref="FeedViewPreference"/>s for the account owner.
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
    public PostInteractionSettingsPreferences? PostInteractionSettingsPreferences { get; }

    /// <summary>
    /// Preferences for how verified accounts appear in an app.
    /// </summary>
    public VerificationPreferences? VerificationPreferences { get; }

    /// <summary>
    /// Read-only preference containing value(s) inferred from the user's declared birthdate.
    /// Absence of this preference object in the response indicates that the user has not made a declaration.
    /// </summary>
    public DeclaredAgePreference? DeclaredAgePreference { get; }

    /// <summary>
    /// User preferences for live events.
    /// </summary>
    public LiveEventPreferences? LiveEventPreferences { get; }
}