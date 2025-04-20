// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;

using idunno.Bluesky.Notifications.Model;

namespace idunno.Bluesky.Serialization.Test
{
    [ExcludeFromCodeCoverage]
    public class NotificationsTests
    {
        private readonly JsonSerializerOptions _jsonSerializerOptions = new(JsonSerializerDefaults.Web);

        [Fact]
        public void EmptyListNotificationsResponseDeserializationsFromJsonWithNoTypeResolver()
        {
            string jsonString = File.ReadAllText(Path.Combine("json", "empty_listNotificationsResponse.json"));

            ListNotificationsResponse? notification = JsonSerializer.Deserialize<ListNotificationsResponse>(jsonString, _jsonSerializerOptions);

            Assert.NotNull(notification);

            Assert.Empty(notification.Notifications);
            Assert.Equal("cursor", notification.Cursor);
            Assert.False(notification.Priority);
            Assert.Equal(new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc), notification.SeenAt);
        }

        [Fact]
        public void EmptyListNotificationsResponseDeserializationsFromJsonWithTypeResolver()
        {
            string jsonString = File.ReadAllText(Path.Combine("json", "empty_listNotificationsResponse.json"));

            ListNotificationsResponse? notification = JsonSerializer.Deserialize<ListNotificationsResponse>(jsonString, BlueskyServer.BlueskyJsonSerializerOptions);

            Assert.NotNull(notification);

            Assert.Empty(notification.Notifications);
            Assert.Equal("cursor", notification.Cursor);
            Assert.False(notification.Priority);
            Assert.Equal(new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc), notification.SeenAt);
        }

        [Fact]
        public void ListNotificationsResponseDeserializationsFromJsonWithNoTypeResolver()
        {
            string jsonString = """
                {
                    "notifications": [
                        {
                            "uri": "at://did:plc:hfgp6pj3akhqxntgqwramlbg/app.bsky.feed.post/3l5w6ldfnud2g",
                            "cid": "bafyreia4hewie6r5erf2s25wqh6f3k2mcy55ne5jcfgtpvnidt3gpsmtsi",
                            "author": {
                                "did": "did:plc:hfgp6pj3akhqxntgqwramlbg",
                                "handle": "blowdart.me",
                                "displayName": "Barry Dorrans",
                                "avatar": "https://cdn.bsky.app/img/avatar/plain/did:plc:hfgp6pj3akhqxntgqwramlbg/bafkreicwjaromkjs4jrd5uqznacfgzvhnob2il5fwywxqopbnhfb74n27m@jpeg",
                                "associated": {
                                    "chat": {
                                        "allowIncoming": "all"
                                    }
                                },
                                "viewer": {
                                    "muted": false,
                                    "blockedBy": false,
                                    "following": "at://did:plc:ec72yg6n2sydzjvtovvdlxrk/app.bsky.graph.follow/3kqxzemnnc425",
                                    "followedBy": "at://did:plc:hfgp6pj3akhqxntgqwramlbg/app.bsky.graph.follow/3ko2gwpx37l2m"
                                },
                                "labels": [],
                                "createdAt": "2023-04-22T22:44:04.316Z",
                                "description": "Security Curmudgeon\n\nDo you really think work wants my social media opinions?\n\nNot nice, but kind - @medus4.com\n\n🇮🇪 🇬🇧 🇺🇸 ",
                                "indexedAt": "2024-08-12T16:22:13.776Z"
                            },
                            "reason": "mention",
                            "record": {
                                "$type": "app.bsky.feed.post",
                                "createdAt": "2024-10-07T11:15:13.731Z",
                                "facets": [
                                    {
                                        "$type": "app.bsky.richtext.facet",
                                        "features": [
                                            {
                                                "$type": "app.bsky.richtext.facet#mention",
                                                "did": "did:plc:ec72yg6n2sydzjvtovvdlxrk"
                                            }
                                        ],
                                        "index": {
                                            "byteEnd": 24,
                                            "byteStart": 0
                                        }
                                    }
                                ],
                                "langs": [
                                    "en"
                                ],
                                "text": "@sarcastabot.bsky.social Have a new notification"
                            },
                            "isRead": false,
                            "indexedAt": "2024-10-07T11:15:13.731Z",
                            "labels": []
                        }
                    ],
                    "cursor": "cursor",
                    "priority": false,
                    "seenAt": "2024-01-01T00:00:00.000Z"
                }
                """;

            ListNotificationsResponse? notification = JsonSerializer.Deserialize<ListNotificationsResponse>(jsonString, _jsonSerializerOptions);

            Assert.NotNull(notification);

            Assert.NotEmpty(notification.Notifications);
            Assert.Single(notification.Notifications);
            Assert.Equal("cursor", notification.Cursor);
            Assert.False(notification.Priority);
            Assert.Equal(new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc), notification.SeenAt);
        }

        [Fact]
        public void ListNotificationsResponseDeserializationsFromJsonWithTypeResolver()
        {
            string jsonString = """
                {
                    "notifications": [
                        {
                            "uri": "at://did:plc:hfgp6pj3akhqxntgqwramlbg/app.bsky.feed.post/3l5w6ldfnud2g",
                            "cid": "bafyreia4hewie6r5erf2s25wqh6f3k2mcy55ne5jcfgtpvnidt3gpsmtsi",
                            "author": {
                                "did": "did:plc:hfgp6pj3akhqxntgqwramlbg",
                                "handle": "blowdart.me",
                                "displayName": "Barry Dorrans",
                                "avatar": "https://cdn.bsky.app/img/avatar/plain/did:plc:hfgp6pj3akhqxntgqwramlbg/bafkreicwjaromkjs4jrd5uqznacfgzvhnob2il5fwywxqopbnhfb74n27m@jpeg",
                                "associated": {
                                    "chat": {
                                        "allowIncoming": "all"
                                    }
                                },
                                "viewer": {
                                    "muted": false,
                                    "blockedBy": false,
                                    "following": "at://did:plc:ec72yg6n2sydzjvtovvdlxrk/app.bsky.graph.follow/3kqxzemnnc425",
                                    "followedBy": "at://did:plc:hfgp6pj3akhqxntgqwramlbg/app.bsky.graph.follow/3ko2gwpx37l2m"
                                },
                                "labels": [],
                                "createdAt": "2023-04-22T22:44:04.316Z",
                                "description": "Security Curmudgeon\n\nDo you really think work wants my social media opinions?\n\nNot nice, but kind - @medus4.com\n\n🇮🇪 🇬🇧 🇺🇸 ",
                                "indexedAt": "2024-08-12T16:22:13.776Z"
                            },
                            "reason": "mention",
                            "record": {
                                "$type": "app.bsky.feed.post",
                                "createdAt": "2024-10-07T11:15:13.731Z",
                                "facets": [
                                    {
                                        "$type": "app.bsky.richtext.facet",
                                        "features": [
                                            {
                                                "$type": "app.bsky.richtext.facet#mention",
                                                "did": "did:plc:ec72yg6n2sydzjvtovvdlxrk"
                                            }
                                        ],
                                        "index": {
                                            "byteEnd": 24,
                                            "byteStart": 0
                                        }
                                    }
                                ],
                                "langs": [
                                    "en"
                                ],
                                "text": "@sarcastabot.bsky.social Have a new notification"
                            },
                            "isRead": false,
                            "indexedAt": "2024-10-07T11:15:13.731Z",
                            "labels": []
                        }
                    ],
                    "cursor": "cursor",
                    "priority": false,
                    "seenAt": "2024-01-01T00:00:00.000Z"
                }
                """;
            ListNotificationsResponse? notification = JsonSerializer.Deserialize<ListNotificationsResponse>(jsonString, BlueskyServer.BlueskyJsonSerializerOptions);

            Assert.NotNull(notification);

            Assert.NotEmpty(notification.Notifications);
            Assert.Single(notification.Notifications);
            Assert.Equal("cursor", notification.Cursor);
            Assert.False(notification.Priority);
            Assert.Equal(new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc), notification.SeenAt);
        }
    }
}
