// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;
using idunno.Bluesky.Notifications;
using idunno.Bluesky.Notifications.Model;

namespace idunno.Bluesky.Serialization.Test
{
    public class ActivitySubscriptionsTests
    {
        [Fact]
        public void ListActivitySubscriptionsResponseDeserializesCorrectly()
        {
            string json = """
                {
                    "subscriptions": [
                        {
                            "did": "did:plc:hfgp6pj3akhqxntgqwramlbg",
                            "handle": "blowdart.me",
                            "displayName": "Barry Dorrans",
                            "avatar": "https://cdn.bsky.app/img/avatar/plain/did:plc:hfgp6pj3akhqxntgqwramlbg/bafkreicwjaromkjs4jrd5uqznacfgzvhnob2il5fwywxqopbnhfb74n27m@jpeg",
                            "associated": {
                                "chat": {
                                    "allowIncoming": "all"
                                },
                                "activitySubscription": {
                                    "allowSubscriptions": "followers"
                                }
                            },
                            "viewer": {
                                "muted": false,
                                "blockedBy": false,
                                "following": "at://did:plc:ec72yg6n2sydzjvtovvdlxrk/app.bsky.graph.follow/3kqxzemnnc425",
                                "followedBy": "at://did:plc:hfgp6pj3akhqxntgqwramlbg/app.bsky.graph.follow/3ko2gwpx37l2m",
                                "activitySubscription": {
                                    "post": true,
                                    "reply": true
                                }
                            },
                            "labels": [],
                            "createdAt": "2023-04-22T22:44:04.316Z",
                            "description": "Security Curmudgeon for Microsoft .NET\n\nDo you really think work wants my social media opinions?\n\nNot nice, but kind - @medus4.com\n\n🇮🇪 🇬🇧 🇺🇸 ",
                            "indexedAt": "2024-10-19T11:12:55.111Z"
                        }
                    ],
                    "cursor": "3ltmosfi4b22o"
                }
                """;

            ListActivitySubscriptionsResponse? activitySubscriptionsResponse = JsonSerializer.Deserialize<ListActivitySubscriptionsResponse>(json, BlueskyServer.BlueskyJsonSerializerOptions);

            Assert.NotNull(activitySubscriptionsResponse);
            Assert.Equal("3ltmosfi4b22o", activitySubscriptionsResponse.Cursor);
            Assert.Single(activitySubscriptionsResponse.Subscriptions);
            Assert.Equal("did:plc:hfgp6pj3akhqxntgqwramlbg", activitySubscriptionsResponse.Subscriptions[0].Did);
            Assert.NotNull(activitySubscriptionsResponse.Subscriptions[0].Viewer);

            ActivitySubscription? activitySubscription = activitySubscriptionsResponse.Subscriptions[0].Viewer!.ActivitySubscription;
            Assert.NotNull(activitySubscription);

            Assert.True(activitySubscription.Post);
            Assert.True(activitySubscription.Reply);
        }
    }
}
