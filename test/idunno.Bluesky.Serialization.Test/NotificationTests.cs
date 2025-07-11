// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;

using idunno.Bluesky.Notifications;
using idunno.Bluesky.Notifications.Model;
using idunno.Bluesky.Notifications.PreferenceTypes;

namespace idunno.Bluesky.Serialization.Test
{
    [ExcludeFromCodeCoverage]
    public class NotificationTests
    {
        [Fact]
        public void NotificationDeserializationsFromJson()
        {
            string jsonString = """
                {
                    "uri": "at://did:plc:mvgsfujvam5iekxlk3howidu/app.bsky.feed.post/3ltfofsmbr22k",
                    "cid": "bafyreidipba7d7cql4f4msh3zv5onwnlnwse7wi77zcdu25egc7ijro6aa",
                    "author": {
                        "did": "did:plc:mvgsfujvam5iekxlk3howidu",
                        "handle": "anotherbot.idunno.blue",
                        "displayName": "Test Bot #2",
                        "avatar": "https://cdn.bsky.app/img/avatar/plain/did:plc:mvgsfujvam5iekxlk3howidu/bafkreigom4rh7v3ruxzhjbivwtrwzs5gr54lc2at2wueemji6d4aljo7ua@jpeg",
                        "associated": {
                            "activitySubscription": {
                                "allowSubscriptions": "followers"
                            }
                        },
                        "viewer": {
                            "muted": false,
                            "blockedBy": false,
                            "following": "at://did:plc:ec72yg6n2sydzjvtovvdlxrk/app.bsky.graph.follow/3lcsswdqzqy2a",
                            "followedBy": "at://did:plc:mvgsfujvam5iekxlk3howidu/app.bsky.graph.follow/3lbueooovu32s"
                        },
                        "labels": [],
                        "createdAt": "2024-11-26T15:39:59.145Z",
                        "description": "Test account for code library.",
                        "indexedAt": "2024-12-19T01:27:45.143Z"
                    },
                    "reason": "mention",
                    "record": {
                        "$type": "app.bsky.feed.post",
                        "createdAt": "2025-07-07T20:57:29.868Z",
                        "facets": [
                            {
                                "$type": "app.bsky.richtext.facet",
                                "features": [
                                    {
                                        "$type": "app.bsky.richtext.facet#mention",
                                        "did": "did:plc:hfgp6pj3akhqxntgqwramlbg"
                                    }
                                ],
                                "index": {
                                    "byteEnd": 37,
                                    "byteStart": 25
                                }
                            },
                            {
                                "$type": "app.bsky.richtext.facet",
                                "features": [
                                    {
                                        "$type": "app.bsky.richtext.facet#mention",
                                        "did": "did:plc:ec72yg6n2sydzjvtovvdlxrk"
                                    }
                                ],
                                "index": {
                                    "byteEnd": 91,
                                    "byteStart": 75
                                }
                            }
                        ],
                        "langs": [
                            "en"
                        ],
                        "text": "This will be reposted by @blowdart.me \n\nAnd the repost will be reposted by @bot.idunno.blue"
                    },
                    "isRead": true,
                    "indexedAt": "2025-07-07T20:57:29.868Z",
                    "labels": []
                }
                """;

            NotificationResponse? notification = JsonSerializer.Deserialize<NotificationResponse>(jsonString, BlueskyServer.BlueskyJsonSerializerOptions);

            Assert.NotNull(notification);
            Assert.IsType<Post>(notification.Record);
        }

        [Fact]
        public void NotificationDeserializationsFromJsonAndCreatesTheCorrectNotificationInstance()
        {
            string jsonString = """
                {
                    "uri": "at://did:plc:mvgsfujvam5iekxlk3howidu/app.bsky.feed.post/3ltfofsmbr22k",
                    "cid": "bafyreidipba7d7cql4f4msh3zv5onwnlnwse7wi77zcdu25egc7ijro6aa",
                    "author": {
                        "did": "did:plc:mvgsfujvam5iekxlk3howidu",
                        "handle": "anotherbot.idunno.blue",
                        "displayName": "Test Bot #2",
                        "avatar": "https://cdn.bsky.app/img/avatar/plain/did:plc:mvgsfujvam5iekxlk3howidu/bafkreigom4rh7v3ruxzhjbivwtrwzs5gr54lc2at2wueemji6d4aljo7ua@jpeg",
                        "associated": {
                            "activitySubscription": {
                                "allowSubscriptions": "followers"
                            }
                        },
                        "viewer": {
                            "muted": false,
                            "blockedBy": false,
                            "following": "at://did:plc:ec72yg6n2sydzjvtovvdlxrk/app.bsky.graph.follow/3lcsswdqzqy2a",
                            "followedBy": "at://did:plc:mvgsfujvam5iekxlk3howidu/app.bsky.graph.follow/3lbueooovu32s"
                        },
                        "labels": [],
                        "createdAt": "2024-11-26T15:39:59.145Z",
                        "description": "Test account for code library.",
                        "indexedAt": "2024-12-19T01:27:45.143Z"
                    },
                    "reason": "mention",
                    "record": {
                        "$type": "app.bsky.feed.post",
                        "createdAt": "2025-07-07T20:57:29.868Z",
                        "facets": [
                            {
                                "$type": "app.bsky.richtext.facet",
                                "features": [
                                    {
                                        "$type": "app.bsky.richtext.facet#mention",
                                        "did": "did:plc:hfgp6pj3akhqxntgqwramlbg"
                                    }
                                ],
                                "index": {
                                    "byteEnd": 37,
                                    "byteStart": 25
                                }
                            },
                            {
                                "$type": "app.bsky.richtext.facet",
                                "features": [
                                    {
                                        "$type": "app.bsky.richtext.facet#mention",
                                        "did": "did:plc:ec72yg6n2sydzjvtovvdlxrk"
                                    }
                                ],
                                "index": {
                                    "byteEnd": 91,
                                    "byteStart": 75
                                }
                            }
                        ],
                        "langs": [
                            "en"
                        ],
                        "text": "This will be reposted by @blowdart.me \n\nAnd the repost will be reposted by @bot.idunno.blue"
                    },
                    "isRead": true,
                    "indexedAt": "2025-07-07T20:57:29.868Z",
                    "labels": []
                }
                """;

            NotificationResponse? notificationResponse = JsonSerializer.Deserialize<NotificationResponse>(jsonString, BlueskyServer.BlueskyJsonSerializerOptions);

            Assert.NotNull(notificationResponse);

            Notification notification = new(notificationResponse);

            Assert.Equal(NotificationReason.Mention, notification.Reason);
        }

        [Fact]
        public void GetPreferencesResponseDeserializeCorrectly()
        {
            string json = """
                {
                    "preferences": {
                        "chat": {
                            "include": "all",
                            "push": true
                        },
                        "follow": {
                            "include": "all",
                            "list": true,
                            "push": false
                        },
                        "like": {
                            "include": "all",
                            "list": true,
                            "push": true
                        },
                        "likeViaRepost": {
                            "include": "all",
                            "list": true,
                            "push": true
                        },
                        "mention": {
                            "include": "all",
                            "list": true,
                            "push": true
                        },
                        "quote": {
                            "include": "all",
                            "list": true,
                            "push": true
                        },
                        "reply": {
                            "include": "all",
                            "list": true,
                            "push": true
                        },
                        "repost": {
                            "include": "all",
                            "list": true,
                            "push": true
                        },
                        "repostViaRepost": {
                            "include": "all",
                            "list": true,
                            "push": true
                        },
                        "starterpackJoined": {
                            "list": false,
                            "push": false
                        },
                        "subscribedPost": {
                            "list": true,
                            "push": true
                        },
                        "unverified": {
                            "list": true,
                            "push": true
                        },
                        "verified": {
                            "list": true,
                            "push": true
                        }
                    }
                }
                """;

            GetPreferencesResponse? actual = JsonSerializer.Deserialize<GetPreferencesResponse>(json, BlueskyServer.BlueskyJsonSerializerOptions);

            Assert.NotNull(actual);

            Assert.True(actual.Preferences.Chat.Push);
            Assert.Equal(ChatNotificationsFrom.All, actual.Preferences.Chat.Include);

            Assert.True(actual.Preferences.Follow.List);
            Assert.False(actual.Preferences.Follow.Push);
            Assert.Equal(LimitTo.All, actual.Preferences.Follow.Include);

            Assert.False(actual.Preferences.StarterPackJoined.List);
            Assert.False(actual.Preferences.StarterPackJoined.Push);
        }
    }
}
