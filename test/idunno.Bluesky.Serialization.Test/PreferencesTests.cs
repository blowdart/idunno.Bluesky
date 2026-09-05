// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;

using idunno.AtProto;
using idunno.Bluesky.Actor;
using idunno.Bluesky.Actor.Model;
using idunno.Bluesky.Feed.Gates;

namespace idunno.Bluesky.Serialization.Test;

[ExcludeFromCodeCoverage]
public class PreferencesTests
{
    [Fact]
    public void ThreadViewPreferenceSerializesToJsonWithBlueskyOptions()
    {
        var threadViewPreference = new ThreadViewPreference(prioritizeFollowedUsers: true);

        string threadViewPreferenceAsJson = JsonSerializer.Serialize(threadViewPreference, BlueskyJsonSerializerOptions.Options);

        Assert.NotNull(threadViewPreferenceAsJson);
    }

    [Fact]
    public void PutPreferencesRequestSerializesToJsonWithBlueskyOptions()
    {
        var threadViewPreference = new ThreadViewPreference(prioritizeFollowedUsers: true);

        var putPreferencesRequest = new PutPreferencesRequest(new Preferences([threadViewPreference]));

        string putPreferencesRequestAsJson = JsonSerializer.Serialize(putPreferencesRequest, BlueskyJsonSerializerOptions.Options);

        Assert.NotNull(putPreferencesRequestAsJson);
    }

    [Fact]
    public void GetPreferencesResponseDeserializesCorrectly()
    {
        string json = """
            {
                "preferences": [
                    {
                        "$type": "app.bsky.actor.defs#savedFeedsPrefV2",
                        "items": [
                            {
                                "id": "3ligfgnjlu22q",
                                "type": "timeline",
                                "value": "following",
                                "pinned": true
                            }
                        ]
                    },
                    {
                        "$type": "app.bsky.actor.defs#threadViewPref",
                        "prioritizeFollowedUsers": false
                    },
                    {
                        "$type": "app.bsky.actor.defs#contentLabelPref",
                        "label": "follow-farming",
                        "labelerDid": "did:plc:e4elbtctnfqocyfcml6h2lf7",
                        "visibility": "warn"
                    },
                    {
                        "$type": "app.bsky.actor.defs#contentLabelPref",
                        "label": "sensual-alf",
                        "labelerDid": "did:plc:e4elbtctnfqocyfcml6h2lf7",
                        "visibility": "ignore"
                    },
                    {
                        "$type": "app.bsky.actor.defs#contentLabelPref",
                        "label": "troll",
                        "labelerDid": "did:plc:e4elbtctnfqocyfcml6h2lf7",
                        "visibility": "warn"
                    },
                    {
                        "$type": "app.bsky.actor.defs#contentLabelPref",
                        "label": "bluesky-elder",
                        "labelerDid": "did:plc:e4elbtctnfqocyfcml6h2lf7",
                        "visibility": "warn"
                    },
                    {
                        "$type": "app.bsky.actor.defs#labelersPref",
                        "labelers": [
                            {
                                "did": "did:plc:e4elbtctnfqocyfcml6h2lf7"
                            },
                            {
                                "did": "did:plc:newitj5jo3uel7o4mnf3vj2o"
                            }
                        ]
                    },
                    {
                        "$type": "app.bsky.actor.defs#contentLabelPref",
                        "label": "altright-screenshot",
                        "labelerDid": "did:plc:newitj5jo3uel7o4mnf3vj2o",
                        "visibility": "hide"
                    },
                    {
                        "$type": "app.bsky.actor.defs#contentLabelPref",
                        "label": "ngl-screenshot",
                        "labelerDid": "did:plc:newitj5jo3uel7o4mnf3vj2o",
                        "visibility": "hide"
                    },
                    {
                        "$type": "app.bsky.actor.defs#contentLabelPref",
                        "label": "reddit-screenshot",
                        "labelerDid": "did:plc:newitj5jo3uel7o4mnf3vj2o",
                        "visibility": "hide"
                    },
                    {
                        "$type": "app.bsky.actor.defs#contentLabelPref",
                        "label": "tumblr-screenshot",
                        "labelerDid": "did:plc:newitj5jo3uel7o4mnf3vj2o",
                        "visibility": "hide"
                    },
                    {
                        "$type": "app.bsky.actor.defs#contentLabelPref",
                        "label": "instagram-screenshot",
                        "labelerDid": "did:plc:newitj5jo3uel7o4mnf3vj2o",
                        "visibility": "hide"
                    },
                    {
                        "$type": "app.bsky.actor.defs#contentLabelPref",
                        "label": "facebook-screenshot",
                        "labelerDid": "did:plc:newitj5jo3uel7o4mnf3vj2o",
                        "visibility": "hide"
                    },
                    {
                        "$type": "app.bsky.actor.defs#contentLabelPref",
                        "label": "fediverse-screenshot",
                        "labelerDid": "did:plc:newitj5jo3uel7o4mnf3vj2o",
                        "visibility": "hide"
                    },
                    {
                        "$type": "app.bsky.actor.defs#contentLabelPref",
                        "label": "threads-screenshot",
                        "labelerDid": "did:plc:newitj5jo3uel7o4mnf3vj2o",
                        "visibility": "hide"
                    },
                    {
                        "$type": "app.bsky.actor.defs#contentLabelPref",
                        "label": "discord-screenshot",
                        "labelerDid": "did:plc:newitj5jo3uel7o4mnf3vj2o",
                        "visibility": "hide"
                    },
                    {
                        "$type": "app.bsky.actor.defs#verificationPrefs",
                        "hideBadges": true
                    },
                    {
                        "$type": "app.bsky.actor.defs#personalDetailsPref",
                        "birthDate": "1970-06-08T00:00:00.000Z"
                    },
                    {
                        "$type": "app.bsky.actor.defs#bskyAppStatePref",
                        "nuxs": [
                            {
                                "id": "InitialVerificationAnnouncement",
                                "completed": true
                            },
                            {
                                "id": "ActivitySubscriptions",
                                "completed": true
                            },
                            {
                                "id": "PolicyUpdate202508",
                                "completed": true
                            },
                            {
                                "id": "BookmarksAnnouncement",
                                "completed": true
                            },
                            {
                                "id": "FindContactsAnnouncement",
                                "completed": true
                            },
                            {
                                "id": "LiveNowBetaDialog",
                                "completed": true
                            },
                            {
                                "id": "DraftsAnnouncement",
                                "completed": true
                            },
                            {
                                "id": "LiveNowBetaNudge",
                                "completed": true
                            },
                            {
                                "id": "FindContactsDismissibleBanner",
                                "completed": true
                            },
                            {
                                "id": "GroupChatsAnnouncement",
                                "completed": true
                            },
                            {
                                "id": "InviteFriendsAnnouncement",
                                "completed": true
                            }
                        ],
                        "isBetaUser": false
                    },
                    {
                        "isOverAge13": true,
                        "isOverAge16": true,
                        "isOverAge18": true,
                        "$type": "app.bsky.actor.defs#declaredAgePref"
                    }
                ]
            }
            """;

        GetPreferencesResponse? deserializedGetPreferencesResponse = JsonSerializer.Deserialize<GetPreferencesResponse>(json, BlueskyJsonSerializerOptions.Options);
        Assert.NotNull(deserializedGetPreferencesResponse);
        Assert.NotNull(deserializedGetPreferencesResponse.Preferences);

        var preferences = new Preferences(deserializedGetPreferencesResponse.Preferences, false);

        Assert.NotNull(preferences);
        Assert.NotNull(preferences.SavedFeedsPreferenceV2);

        SavedFeed savedPreferenceV2 = Assert.Single(preferences.SavedFeedsPreferenceV2);
        Assert.NotNull(savedPreferenceV2);
        Assert.Equal("3ligfgnjlu22q", savedPreferenceV2.Id);
        Assert.Equal(SavedFeedPreferenceType.Timeline, savedPreferenceV2.Type);
        Assert.Equal("following", savedPreferenceV2.Value);
        Assert.True(savedPreferenceV2.Pinned);

        Assert.NotNull(preferences.ThreadViewPreference);
        Assert.False(preferences.ThreadViewPreference.PrioritizeFollowedUsers);

        Assert.Equal(13, preferences.ContentLabelPreferences.Count);
        Assert.Contains(new ContentLabelPreference("follow-farming", "did:plc:e4elbtctnfqocyfcml6h2lf7", LabelVisibility.Warn), preferences.ContentLabelPreferences);
        Assert.Contains(new ContentLabelPreference("sensual-alf", "did:plc:e4elbtctnfqocyfcml6h2lf7", LabelVisibility.Ignore), preferences.ContentLabelPreferences);
        Assert.Contains(new ContentLabelPreference("altright-screenshot", "did:plc:newitj5jo3uel7o4mnf3vj2o", LabelVisibility.Hide), preferences.ContentLabelPreferences);

        Assert.Equal(2, preferences.SubscribedLabelers.Count);
        Assert.Contains(new Did("did:plc:e4elbtctnfqocyfcml6h2lf7"), preferences.SubscribedLabelers);
        Assert.Contains(new Did("did:plc:newitj5jo3uel7o4mnf3vj2o"), preferences.SubscribedLabelers);

        Assert.NotNull(preferences.PersonalDetailsPreference);
        Assert.Equal(new DateTimeOffset(new DateTime(1970, 6, 8), TimeSpan.Zero), preferences.PersonalDetailsPreference.BirthDate);

        Assert.NotNull(preferences.VerificationPreferences);
        Assert.True(preferences.VerificationPreferences.HideBadges);

        Assert.NotNull(preferences.DeclaredAgePreference);
        Assert.True(preferences.DeclaredAgePreference.IsOverAge13);
        Assert.True(preferences.DeclaredAgePreference.IsOverAge16);
        Assert.True(preferences.DeclaredAgePreference.IsOverAge18);
    }

    [Fact]
    public void IndividualSavedFeedPreferenceV2DeserializesCorrectly()
    {
        string json = """
            {
                    "id": "3ligfgnjlu22q",
                    "type": "timeline",
                    "value": "following",
                    "pinned": true
            }
            """;

        SavedFeed? deserializedSavedFeedPreferenceV2 = JsonSerializer.Deserialize<SavedFeed>(json, BlueskyJsonSerializerOptions.Options);
        Assert.NotNull(deserializedSavedFeedPreferenceV2);
        Assert.Equal("3ligfgnjlu22q", deserializedSavedFeedPreferenceV2.Id);
        Assert.Equal(SavedFeedPreferenceType.Timeline, deserializedSavedFeedPreferenceV2.Type);
        Assert.Equal("following", deserializedSavedFeedPreferenceV2.Value);
        Assert.True(deserializedSavedFeedPreferenceV2.Pinned);
    }

    [Fact]
    public void SavedFeedPreferencesV2DeserializesCorrectly()
    {
        string json = """
            {
                "preferences": [
                    {
                        "$type": "app.bsky.actor.defs#savedFeedsPrefV2",
                        "items": [
                            {
                                "id": "3ligfgnjlu22q",
                                "type": "timeline",
                                "value": "following",
                                "pinned": true
                            }
                        ]
                    }
                ]
            }
            """;

        GetPreferencesResponse? deserializedGetPreferencesResponse = JsonSerializer.Deserialize<GetPreferencesResponse>(json, BlueskyJsonSerializerOptions.Options);
        Assert.NotNull(deserializedGetPreferencesResponse);
        Assert.NotNull(deserializedGetPreferencesResponse.Preferences);

        var preferences = new Preferences(deserializedGetPreferencesResponse.Preferences, false);

        Assert.NotNull(preferences);
        Assert.NotNull(preferences.SavedFeedsPreferenceV2);

        SavedFeed savedFeedPreferenceV2 = Assert.Single(preferences.SavedFeedsPreferenceV2);

        Assert.NotNull(savedFeedPreferenceV2);
        Assert.Equal("3ligfgnjlu22q", savedFeedPreferenceV2.Id);
        Assert.Equal(SavedFeedPreferenceType.Timeline, savedFeedPreferenceV2.Type);
        Assert.Equal("following", savedFeedPreferenceV2.Value);
        Assert.True(savedFeedPreferenceV2.Pinned);
    }

    [Fact]
    public void InterestsPrefsDeserializesCorrectly()
    {
        string json = """
            {
                "preferences": [
                    {
                        "$type": "app.bsky.actor.defs#interestsPref",
                        "tags": [
                            "tag1",
                            "tag2",
                            "tag3"
                        ]
                    }
                ]
            }
            """;

        GetPreferencesResponse? deserializedGetPreferencesResponse = JsonSerializer.Deserialize<GetPreferencesResponse>(json, BlueskyJsonSerializerOptions.Options);
        Assert.NotNull(deserializedGetPreferencesResponse);
        Assert.NotNull(deserializedGetPreferencesResponse.Preferences);

        var preferences = new Preferences(deserializedGetPreferencesResponse.Preferences, false);

        Assert.NotNull(preferences);
        Assert.Equal(3, preferences.Interests!.Tags.Count);
        Assert.Contains("tag1", preferences.Interests.Tags);
        Assert.Contains("tag2", preferences.Interests.Tags);
        Assert.Contains("tag3", preferences.Interests.Tags);
        Assert.Null(preferences.Interests.UpdatedAt);
    }

    [Fact]
    public void InterestsPrefsWithUpdatedAtDeserializesCorrectly()
    {
        string json = """
            {
                "preferences": [
                    {
                        "$type": "app.bsky.actor.defs#interestsPref",
                        "tags": [
                            "tag1",
                            "tag2",
                            "tag3"
                        ],
                        "updatedAt": "2024-06-01T12:34:56.789Z"
                    }
                ]
            }
            """;

        GetPreferencesResponse? deserializedGetPreferencesResponse = JsonSerializer.Deserialize<GetPreferencesResponse>(json, BlueskyJsonSerializerOptions.Options);
        Assert.NotNull(deserializedGetPreferencesResponse);
        Assert.NotNull(deserializedGetPreferencesResponse.Preferences);

        var preferences = new Preferences(deserializedGetPreferencesResponse.Preferences, false);

        Assert.NotNull(preferences);
        Assert.NotNull(preferences.Interests);

        Assert.Equal(DateTimeOffset.Parse("2024-06-01T12:34:56.789Z"), preferences.Interests.UpdatedAt);
    }

    [Fact]
    public void MutedWordsPrefsDeserializesCorrectly()
    {
        string json = """
            {
                "preferences": [
                    {
                        "$type": "app.bsky.actor.defs#mutedWordsPref",
                        "items": [
                            {
                                "value": "Wordle",
                                "targets": [
                                    "tag",
                                    "content"
                                ],
                                "id": "3lkeh5t5pwt2s",
                                "actorTarget": "all"
                            }
                        ]
                    }
                ]
            }
            """;

        GetPreferencesResponse? deserializedGetPreferencesResponse = JsonSerializer.Deserialize<GetPreferencesResponse>(json, BlueskyJsonSerializerOptions.Options);
        Assert.NotNull(deserializedGetPreferencesResponse);
        Assert.NotNull(deserializedGetPreferencesResponse.Preferences);

        var preferences = new Preferences(deserializedGetPreferencesResponse.Preferences, false);

        Assert.NotNull(preferences);
        Assert.NotNull(preferences.MutedWords);

        MutedWord mutedWord = Assert.Single(preferences.MutedWords);
        Assert.Equal("Wordle", mutedWord.Value);
        Assert.Contains(MutedWordTarget.Tag, mutedWord.Targets);
        Assert.Contains(MutedWordTarget.Content, mutedWord.Targets);
        Assert.Equal("3lkeh5t5pwt2s", mutedWord.Id);
        Assert.Equal(MutedWordActorTarget.All, mutedWord.ActorTarget);
    }

    [Fact]
    public void SavedFeedPrefV2DeserializesCorrectly()
    {
        string json = """
            {
                "preferences": [
                    {
                        "$type": "app.bsky.actor.defs#savedFeedsPrefV2",
                        "items": [
                            {
                                "id": "3ligfgnjlu22q",
                                "type": "timeline",
                                "value": "following",
                                "pinned": true
                            }
                        ]
                    }
                ]
            }
            """;

        GetPreferencesResponse? deserializedGetPreferencesResponse = JsonSerializer.Deserialize<GetPreferencesResponse>(json, BlueskyJsonSerializerOptions.Options);
        Assert.NotNull(deserializedGetPreferencesResponse);
        Assert.NotNull(deserializedGetPreferencesResponse.Preferences);

        var preferences = new Preferences(deserializedGetPreferencesResponse.Preferences, false);

        Assert.NotNull(preferences);
        Assert.NotNull(preferences.SavedFeedsPreferenceV2);

        SavedFeed savedFeedPreferenceV2 = Assert.Single(preferences.SavedFeedsPreferenceV2);
        Assert.Equal("3ligfgnjlu22q", savedFeedPreferenceV2.Id);
        Assert.Equal(SavedFeedPreferenceType.Timeline, savedFeedPreferenceV2.Type);
        Assert.Equal("following", savedFeedPreferenceV2.Value);
        Assert.True(savedFeedPreferenceV2.Pinned);
    }

    [Fact]
    public void SavedFeedsPrefDeserializesCorrectly()
    {
        string json = """
            {
                "preferences": [
                    {
                        "$type": "app.bsky.actor.defs#savedFeedsPref",
                        "pinned": [
                            "at://did:plc:z72i7hdynmk6r22z27h6tvur/app.bsky.feed.generator/whats-hot",
                            "at://did:plc:wqowuobffl66jv3kpsvo7ak4/app.bsky.feed.generator/the-algorithm"
                        ],
                        "saved": [
                            "at://did:plc:z72i7hdynmk6r22z27h6tvur/app.bsky.feed.generator/bsky-team"
                        ]
                    }
                ]
            }
            """;

        GetPreferencesResponse? deserializedGetPreferencesResponse = JsonSerializer.Deserialize<GetPreferencesResponse>(json, BlueskyJsonSerializerOptions.Options);
        Assert.NotNull(deserializedGetPreferencesResponse);
        Assert.NotNull(deserializedGetPreferencesResponse.Preferences);
        var preferences = new Preferences(deserializedGetPreferencesResponse.Preferences, false);

        Assert.NotNull(preferences);
        Assert.NotNull(preferences.SavedFeedsPreference);

        Assert.Equal(2, preferences.SavedFeedsPreference.Pinned.Count);

        AtUri saved = Assert.Single(preferences.SavedFeedsPreference.Saved);

        Assert.Contains(new AtUri("at://did:plc:z72i7hdynmk6r22z27h6tvur/app.bsky.feed.generator/whats-hot"), preferences.SavedFeedsPreference.Pinned);
        Assert.Contains(new AtUri("at://did:plc:wqowuobffl66jv3kpsvo7ak4/app.bsky.feed.generator/the-algorithm"), preferences.SavedFeedsPreference.Pinned);
        Assert.Equal(new AtUri("at://did:plc:z72i7hdynmk6r22z27h6tvur/app.bsky.feed.generator/bsky-team"), saved);

        Assert.Null(preferences.SavedFeedsPreference.TimelineIndex);
    }

    [Fact]
    public void PostInteractionPreferencesDeserializesCorrectly()
    {
        string json = """
            {
                "preferences": [
                    {
                        "threadgateAllowRules": [
                            {
                                "$type": "app.bsky.feed.threadgate#followingRule"
                            }
                        ],
                        "postgateEmbeddingRules": [
                            {
                                "$type": "app.bsky.feed.postgate#disableRule"
                            }
                        ],
                        "$type": "app.bsky.actor.defs#postInteractionSettingsPref"
                    }
                ]
            }
            """;
        GetPreferencesResponse? deserializedGetPreferencesResponse = JsonSerializer.Deserialize<GetPreferencesResponse>(json, BlueskyJsonSerializerOptions.Options);
        Assert.NotNull(deserializedGetPreferencesResponse);
        Assert.NotNull(deserializedGetPreferencesResponse.Preferences);

        var preferences = new Preferences(deserializedGetPreferencesResponse.Preferences, false);
        Assert.NotNull(preferences);
        Assert.NotNull(preferences.PostInteractionSettingsPreferences);

        Assert.NotNull(preferences.PostInteractionSettingsPreferences.ThreadGateAllowRules);
        ThreadGateRule threadGateAllowRule = Assert.Single(preferences.PostInteractionSettingsPreferences.ThreadGateAllowRules);
        Assert.IsType<FollowingRule>(threadGateAllowRule);

        Assert.NotNull(preferences.PostInteractionSettingsPreferences.PostGateEmbeddingRules);
        PostGateRule postgateEmbeddingRule = Assert.Single(preferences.PostInteractionSettingsPreferences.PostGateEmbeddingRules);
        Assert.IsType<DisableEmbeddingRule>(postgateEmbeddingRule);
    }

    [Fact]
    public void EnableAdultContentPreferenceDeserializesCorrectlyWhenTrue()
    {
        string json = """
            {
                "preferences": [
                    {
                        "$type": "app.bsky.actor.defs#adultContentPref",
                        "enabled": true
                    }
                ]
            }
            """;

        GetPreferencesResponse? deserializedGetPreferencesResponse = JsonSerializer.Deserialize<GetPreferencesResponse>(json, BlueskyJsonSerializerOptions.Options);
        Assert.NotNull(deserializedGetPreferencesResponse);
        Assert.NotNull(deserializedGetPreferencesResponse.Preferences);

        var preferences = new Preferences(deserializedGetPreferencesResponse.Preferences, false);
        Assert.NotNull(preferences);
        Assert.NotNull(preferences.AdultContentPreference);
        Assert.True(preferences.AdultContentPreference.Enabled);
    }

    [Fact]
    public void EnableAdultContentPreferenceDeserializesCorrectlyWhenFalse()
    {
        string json = """
            {
                "preferences": [
                    {
                        "$type": "app.bsky.actor.defs#adultContentPref",
                        "enabled": false
                    }
                ]
            }
            """;

        GetPreferencesResponse? deserializedGetPreferencesResponse = JsonSerializer.Deserialize<GetPreferencesResponse>(json, BlueskyJsonSerializerOptions.Options);
        Assert.NotNull(deserializedGetPreferencesResponse);
        Assert.NotNull(deserializedGetPreferencesResponse.Preferences);

        var preferences = new Preferences(deserializedGetPreferencesResponse.Preferences, false);
        Assert.NotNull(preferences);
        Assert.NotNull(preferences.AdultContentPreference);
        Assert.False(preferences.AdultContentPreference.Enabled);
    }

    [Fact]
    public void FeedViewPerferenceDeserializesCorrectly()
    {
        string json = """
            {
                "preferences": [
                    {
                        "$type": "app.bsky.actor.defs#feedViewPref",
                        "feed": "home",
                        "lab_mergeFeedEnabled": true,
                        "hideReplies": false,
                        "hideRepliesByUnfollowed": false,
                        "hideRepliesByLikeCount": 0,
                        "hideReposts": true,
                        "hideQuotePosts": false
                    }
                ]
            }
            """;
        GetPreferencesResponse? deserializedGetPreferencesResponse = JsonSerializer.Deserialize<GetPreferencesResponse>(json, BlueskyJsonSerializerOptions.Options);
        Assert.NotNull(deserializedGetPreferencesResponse);
        Assert.NotNull(deserializedGetPreferencesResponse.Preferences);
        var preferences = new Preferences(deserializedGetPreferencesResponse.Preferences, false);
        Assert.NotNull(preferences);
        Assert.NotNull(preferences.FeedViewPreference);

        Assert.Equal("home", preferences.FeedViewPreference.Feed);
        Assert.False(preferences.FeedViewPreference.HideReplies);
        Assert.False(preferences.FeedViewPreference.HideRepliesByUnfollowed);
        Assert.Equal(0, preferences.FeedViewPreference.HideRepliesByLikeCount);
        Assert.True(preferences.FeedViewPreference.HideReposts);
        Assert.False(preferences.FeedViewPreference.HideQuotePosts);

        Assert.NotNull(preferences.FeedViewPreference.ExtensionData);
        Assert.Contains("lab_mergeFeedEnabled", preferences.FeedViewPreference.ExtensionData.Keys);
    }
}