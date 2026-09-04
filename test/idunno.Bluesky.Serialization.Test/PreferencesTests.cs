// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;

using idunno.AtProto;
using idunno.Bluesky.Actor;
using idunno.Bluesky.Actor.Model;

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
        Assert.NotNull(preferences.SavedFeedPreferenceV2);

        SavedFeedPreferenceV2 savedPreferenceV2 = Assert.Single(preferences.SavedFeedPreferenceV2);
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
    public void SavedFeedPreferenceV2DeserializesCorrectly()
    {
        string json = """
            {
                    "id": "3ligfgnjlu22q",
                    "type": "timeline",
                    "value": "following",
                    "pinned": true
            }
            """;

        SavedFeedPreferenceV2? deserializedSavedFeedPreferenceV2 = JsonSerializer.Deserialize<SavedFeedPreferenceV2>(json, BlueskyJsonSerializerOptions.Options);
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
            """;

        SavedFeedPreferencesV2? deserializedSavedFeedPreferenceV2 = JsonSerializer.Deserialize<SavedFeedPreferencesV2>(json, BlueskyJsonSerializerOptions.Options);

        Assert.NotNull(deserializedSavedFeedPreferenceV2);
        SavedFeedPreferenceV2 savedFeedPreferenceV2 = Assert.Single(deserializedSavedFeedPreferenceV2.Items);

        Assert.NotNull(savedFeedPreferenceV2);
        Assert.Equal("3ligfgnjlu22q", savedFeedPreferenceV2.Id);
        Assert.Equal(SavedFeedPreferenceType.Timeline, savedFeedPreferenceV2.Type);
        Assert.Equal("following", savedFeedPreferenceV2.Value);
        Assert.True(savedFeedPreferenceV2.Pinned);
    }
}