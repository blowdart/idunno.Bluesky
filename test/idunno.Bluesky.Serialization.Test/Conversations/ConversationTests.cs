// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;

using idunno.AtProto;
using idunno.Bluesky.Chat;
using idunno.Bluesky.Chat.Actor;
using idunno.Bluesky.Chat.Model;

namespace idunno.Bluesky.Serialization.Test.Conversations;

public class ConversationTests
{
    private readonly JsonSerializerOptions _jsonSerializerOptions = new(JsonSerializerDefaults.Web) { AllowOutOfOrderMetadataProperties = true };

    [Fact]
    public void UnreadConversationCountsDeserializationsCorrectly()
    {
        string json = """
        {
            "unreadAcceptedConvos": 5,
            "unreadRequestConvos": 3
        }
        """;

        UnreadConversationCounts? counts = JsonSerializer.Deserialize<UnreadConversationCounts>(json, _jsonSerializerOptions);

        Assert.NotNull(counts);

        Assert.Equal(5, counts.UnreadAcceptedConversations);
        Assert.Equal(3, counts.UnreadRequestedConversations);
    }

    [Fact]
    public void UnreadConversationCountsDeserializationsCorrectlyWithBlueskySerializationOptions()
    {
        string json = """
        {
            "unreadAcceptedConvos": 5,
            "unreadRequestConvos": 3
        }
        """;

        UnreadConversationCounts? counts = JsonSerializer.Deserialize<UnreadConversationCounts>(json, BlueskyJsonSerializerOptions.Options);

        Assert.NotNull(counts);

        Assert.Equal(5, counts.UnreadAcceptedConversations);
        Assert.Equal(3, counts.UnreadRequestedConversations);
    }

    [Fact]
    public void DirectConversationAvailabilityDeserializationsCorrectly()
    {
        string json = """
        {
            "canChat": true,
            "convo": {
                "id": "3kt4jgm5nc32q",
                "rev": "2222223gg456s",
                "members": [
                    {
                        "did": "did:plc:ec72yg6n2sydzjvtovvdlxrk",
                        "handle": "bot.idunno.blue",
                        "displayName": "Test Bot",
                        "avatar": "https://cdn.bsky.app/img/avatar/plain/did:plc:ec72yg6n2sydzjvtovvdlxrk/bafkreig5ujmxrechgakn4ukf37oj6mlpivukqfbgpuhb3pqjmdkxxtjpnq",
                        "associated": {
                            "lists": 2,
                            "feedgens": 0,
                            "starterPacks": 1,
                            "labeler": false,
                            "activitySubscription": {
                                "allowSubscriptions": "followers"
                            }
                        },
                        "viewer": {
                            "muted": false,
                            "blockedBy": false
                        },
                        "labels": [
                            {
                                "src": "did:plc:ec72yg6n2sydzjvtovvdlxrk",
                                "uri": "at://did:plc:ec72yg6n2sydzjvtovvdlxrk/app.bsky.actor.profile/self",
                                "cid": "bafyreig52l5b45222ijf3sf5jggvcxcvmpmetlswqyoqzh2dlfyoge74oq",
                                "val": "bot",
                                "cts": "1970-01-01T00:00:00.000Z"
                            },
                            {
                                "src": "did:plc:ec72yg6n2sydzjvtovvdlxrk",
                                "uri": "at://did:plc:ec72yg6n2sydzjvtovvdlxrk/app.bsky.actor.profile/self",
                                "cid": "bafyreig52l5b45222ijf3sf5jggvcxcvmpmetlswqyoqzh2dlfyoge74oq",
                                "val": "!no-unauthenticated",
                                "cts": "1970-01-01T00:00:00.000Z"
                            }
                        ],
                        "createdAt": "2024-03-19T13:00:19.046Z",
                        "chatDisabled": false,
                        "kind": {
                            "$type": "chat.bsky.actor.defs#directConvoMember"
                        }
                    },
                    {
                        "did": "did:plc:hfgp6pj3akhqxntgqwramlbg",
                        "handle": "blowdart.me",
                        "displayName": "Barry Dorrans",
                        "avatar": "https://cdn.bsky.app/img/avatar/plain/did:plc:hfgp6pj3akhqxntgqwramlbg/bafkreicwjaromkjs4jrd5uqznacfgzvhnob2il5fwywxqopbnhfb74n27m",
                        "associated": {
                            "lists": 2,
                            "feedgens": 1,
                            "starterPacks": 0,
                            "labeler": false,
                            "chat": {
                                "allowIncoming": "all"
                            },
                            "activitySubscription": {
                                "allowSubscriptions": "followers"
                            },
                            "germ": {
                                "showButtonTo": "usersIFollow",
                                "messageMeUrl": "https://landing.ger.mx/newUser"
                            }
                        },
                        "viewer": {
                            "muted": false,
                            "blockedBy": false,
                            "following": "at://did:plc:ec72yg6n2sydzjvtovvdlxrk/app.bsky.graph.follow/3kqxzemnnc425",
                            "followedBy": "at://did:plc:hfgp6pj3akhqxntgqwramlbg/app.bsky.graph.follow/3ko2gwpx37l2m",
                            "knownFollowers": {
                                "count": 2,
                                "followers": [
                                    {
                                        "did": "did:plc:wtdzzfgzjpirnk5wvpjutqoy",
                                        "handle": "oracularhades.com",
                                        "displayName": "Josh",
                                        "avatar": "https://cdn.bsky.app/img/avatar/plain/did:plc:wtdzzfgzjpirnk5wvpjutqoy/bafkreiahoixmvuo7blc5dhmul5w64llaeofxfu6qmsu7zewe5jvum2ntdy",
                                        "associated": {
                                            "chat": {
                                                "allowIncoming": "none"
                                            },
                                            "activitySubscription": {
                                                "allowSubscriptions": "mutuals"
                                            }
                                        },
                                        "viewer": {
                                            "muted": false,
                                            "blockedBy": false,
                                            "following": "at://did:plc:ec72yg6n2sydzjvtovvdlxrk/app.bsky.graph.follow/3l7hl6zgkwi25",
                                            "followedBy": "at://did:plc:wtdzzfgzjpirnk5wvpjutqoy/app.bsky.graph.follow/3krd3g33eds23"
                                        },
                                        "labels": [],
                                        "createdAt": "2023-04-24T18:23:37.593Z"
                                    },
                                    {
                                        "did": "did:plc:sgcrgcztzb5eqvajnkigmhhv",
                                        "handle": "bot.oracularhades.com",
                                        "displayName": "hadesbot",
                                        "associated": {
                                            "activitySubscription": {
                                                "allowSubscriptions": "followers"
                                            }
                                        },
                                        "viewer": {
                                            "muted": false,
                                            "blockedBy": false,
                                            "following": "at://did:plc:ec72yg6n2sydzjvtovvdlxrk/app.bsky.graph.follow/3lbsmz2ws6u2s",
                                            "followedBy": "at://did:plc:sgcrgcztzb5eqvajnkigmhhv/app.bsky.graph.follow/3lbsmmyqoxe2d"
                                        },
                                        "labels": [],
                                        "createdAt": "2024-11-18T15:28:50.050Z"
                                    }
                                ]
                            }
                        },
                        "labels": [],
                        "createdAt": "2023-04-22T22:44:04.316Z",
                        "chatDisabled": false,
                        "kind": {
                            "$type": "chat.bsky.actor.defs#directConvoMember"
                        }
                    }
                ],
                "lastMessage": {
                    "id": "3lqzgq3susj24",
                    "rev": "2222223gg456s",
                    "sender": {
                        "did": "did:plc:ec72yg6n2sydzjvtovvdlxrk"
                    },
                    "text": "Embedded post test",
                    "facets": [],
                    "embed": {
                        "record": {
                            "uri": "at://did:plc:hfgp6pj3akhqxntgqwramlbg/app.bsky.feed.post/3lqxyqocwx22m",
                            "cid": "bafyreih5lrbmanjn3rokps54pp5kjrg3ioj6q64ohvpz3pjai5edkgaqim",
                            "author": {
                                "did": "did:plc:hfgp6pj3akhqxntgqwramlbg",
                                "handle": "blowdart.me",
                                "displayName": "Barry Dorrans",
                                "pronouns": "He/Him",
                                "avatar": "https://cdn.bsky.app/img/avatar/plain/did:plc:hfgp6pj3akhqxntgqwramlbg/bafkreicwjaromkjs4jrd5uqznacfgzvhnob2il5fwywxqopbnhfb74n27m",
                                "associated": {
                                    "chat": {
                                        "allowIncoming": "all"
                                    },
                                    "activitySubscription": {
                                        "allowSubscriptions": "followers"
                                    },
                                    "germ": {
                                        "showButtonTo": "usersIFollow",
                                        "messageMeUrl": "https://landing.ger.mx/newUser"
                                    }
                                },
                                "viewer": {
                                    "muted": false,
                                    "blockedBy": false,
                                    "following": "at://did:plc:ec72yg6n2sydzjvtovvdlxrk/app.bsky.graph.follow/3kqxzemnnc425",
                                    "followedBy": "at://did:plc:hfgp6pj3akhqxntgqwramlbg/app.bsky.graph.follow/3ko2gwpx37l2m"
                                },
                                "labels": [],
                                "createdAt": "2023-04-22T22:44:04.316Z"
                            },
                            "value": {
                                "$type": "app.bsky.feed.post",
                                "createdAt": "2025-06-06T23:34:52.283Z",
                                "embed": {
                                    "$type": "app.bsky.embed.images",
                                    "images": [
                                        {
                                            "alt": "An faked image of Pete Davidson walking next to Donald Trump ",
                                            "aspectRatio": {
                                                "height": 1118,
                                                "width": 1206
                                            },
                                            "image": {
                                                "$type": "blob",
                                                "ref": {
                                                    "$link": "bafkreicktaxlwu3zsktkzmofc7vko4furwcxyroaz45qn3cmzxwwuuhsay"
                                                },
                                                "mimeType": "image/jpeg",
                                                "size": 375073
                                            }
                                        }
                                    ]
                                },
                                "langs": [
                                    "en"
                                ],
                                "text": "That was quick"
                            },
                            "labels": [],
                            "replyCount": 1,
                            "repostCount": 14,
                            "likeCount": 54,
                            "embeds": [
                                {
                                    "images": [
                                        {
                                            "thumb": "https://cdn.bsky.app/img/feed_thumbnail/plain/did:plc:hfgp6pj3akhqxntgqwramlbg/bafkreicktaxlwu3zsktkzmofc7vko4furwcxyroaz45qn3cmzxwwuuhsay",
                                            "fullsize": "https://cdn.bsky.app/img/feed_fullsize/plain/did:plc:hfgp6pj3akhqxntgqwramlbg/bafkreicktaxlwu3zsktkzmofc7vko4furwcxyroaz45qn3cmzxwwuuhsay",
                                            "alt": "An faked image of Pete Davidson walking next to Donald Trump ",
                                            "aspectRatio": {
                                                "height": 1118,
                                                "width": 1206
                                            }
                                        }
                                    ],
                                    "$type": "app.bsky.embed.images#view"
                                }
                            ],
                            "indexedAt": "2025-06-06T23:34:55.648Z",
                            "$type": "app.bsky.embed.record#viewRecord"
                        },
                        "$type": "app.bsky.embed.record#view"
                    },
                    "reactions": [],
                    "sentAt": "2025-06-07T13:17:45.015Z",
                    "$type": "chat.bsky.convo.defs#messageView"
                },
                "lastReaction": {
                    "message": {
                        "id": "3lcweif2c622k",
                        "rev": "22222236abbpj",
                        "sender": {
                            "did": "did:plc:ec72yg6n2sydzjvtovvdlxrk"
                        },
                        "text": "@blowdart.me cached",
                        "facets": [
                            {
                                "index": {
                                    "byteEnd": 12,
                                    "byteStart": 0
                                },
                                "features": [
                                    {
                                        "did": "did:plc:hfgp6pj3akhqxntgqwramlbg",
                                        "$type": "app.bsky.richtext.facet#mention"
                                    }
                                ]
                            }
                        ],
                        "reactions": [
                            {
                                "createdAt": "2025-04-04T00:33:56.379Z",
                                "value": "💩",
                                "sender": {
                                    "did": "did:plc:ec72yg6n2sydzjvtovvdlxrk"
                                },
                                "$type": "chat.bsky.convo.defs#reactionView"
                            }
                        ],
                        "sentAt": "2024-12-10T04:06:56.755Z",
                        "$type": "chat.bsky.convo.defs#messageView"
                    },
                    "reaction": {
                        "createdAt": "2025-04-04T00:33:56.379Z",
                        "value": "💩",
                        "sender": {
                            "did": "did:plc:ec72yg6n2sydzjvtovvdlxrk"
                        },
                        "$type": "chat.bsky.convo.defs#reactionView"
                    },
                    "$type": "chat.bsky.convo.defs#messageAndReactionView"
                },
                "unreadCount": 0,
                "status": "accepted",
                "muted": false,
                "kind": {
                    "$type": "chat.bsky.convo.defs#directConvo"
                }
            }
        }
        """;

        ConversationAvailability? availability = JsonSerializer.Deserialize<ConversationAvailability>(json, _jsonSerializerOptions);
        Assert.NotNull(availability);

        Assert.True(availability.CanChat);
        Assert.NotNull(availability.Conversation);
        Assert.Equal("3kt4jgm5nc32q", availability.Conversation.Id);
        Assert.Equal("2222223gg456s", availability.Conversation.Revision);
        Assert.Equal(2, availability.Conversation.Members.Count);

        Assert.IsType<DirectConversation>(availability.Conversation.Kind);
    }

    [Fact]
    public void GetConversationMembersResponseDeserializesCorrectly()
    {
        string json = """
            {
                "members": [
                    {
                        "did": "did:plc:ec72yg6n2sydzjvtovvdlxrk",
                        "handle": "bot.idunno.blue",
                        "displayName": "Test Bot",
                        "avatar": "https://cdn.bsky.app/img/avatar/plain/did:plc:ec72yg6n2sydzjvtovvdlxrk/bafkreig5ujmxrechgakn4ukf37oj6mlpivukqfbgpuhb3pqjmdkxxtjpnq",
                        "associated": {
                            "lists": 2,
                            "feedgens": 0,
                            "starterPacks": 1,
                            "labeler": false,
                            "activitySubscription": {
                                "allowSubscriptions": "followers"
                            }
                        },
                        "viewer": {
                            "muted": false,
                            "blockedBy": false
                        },
                        "labels": [
                            {
                                "src": "did:plc:ec72yg6n2sydzjvtovvdlxrk",
                                "uri": "at://did:plc:ec72yg6n2sydzjvtovvdlxrk/app.bsky.actor.profile/self",
                                "cid": "bafyreig52l5b45222ijf3sf5jggvcxcvmpmetlswqyoqzh2dlfyoge74oq",
                                "val": "bot",
                                "cts": "1970-01-01T00:00:00.000Z"
                            },
                            {
                                "src": "did:plc:ec72yg6n2sydzjvtovvdlxrk",
                                "uri": "at://did:plc:ec72yg6n2sydzjvtovvdlxrk/app.bsky.actor.profile/self",
                                "cid": "bafyreig52l5b45222ijf3sf5jggvcxcvmpmetlswqyoqzh2dlfyoge74oq",
                                "val": "!no-unauthenticated",
                                "cts": "1970-01-01T00:00:00.000Z"
                            }
                        ],
                        "createdAt": "2024-03-19T13:00:19.046Z",
                        "chatDisabled": false,
                        "kind": {
                            "$type": "chat.bsky.actor.defs#directConvoMember"
                        }
                    },
                    {
                        "did": "did:plc:hfgp6pj3akhqxntgqwramlbg",
                        "handle": "blowdart.me",
                        "displayName": "Barry Dorrans",
                        "avatar": "https://cdn.bsky.app/img/avatar/plain/did:plc:hfgp6pj3akhqxntgqwramlbg/bafkreicwjaromkjs4jrd5uqznacfgzvhnob2il5fwywxqopbnhfb74n27m",
                        "associated": {
                            "lists": 2,
                            "feedgens": 1,
                            "starterPacks": 0,
                            "labeler": false,
                            "chat": {
                                "allowIncoming": "all"
                            },
                            "activitySubscription": {
                                "allowSubscriptions": "followers"
                            },
                            "germ": {
                                "showButtonTo": "usersIFollow",
                                "messageMeUrl": "https://landing.ger.mx/newUser"
                            }
                        },
                        "viewer": {
                            "muted": false,
                            "blockedBy": false,
                            "following": "at://did:plc:ec72yg6n2sydzjvtovvdlxrk/app.bsky.graph.follow/3kqxzemnnc425",
                            "followedBy": "at://did:plc:hfgp6pj3akhqxntgqwramlbg/app.bsky.graph.follow/3ko2gwpx37l2m",
                            "knownFollowers": {
                                "count": 2,
                                "followers": [
                                    {
                                        "did": "did:plc:wtdzzfgzjpirnk5wvpjutqoy",
                                        "handle": "oracularhades.com",
                                        "displayName": "Josh",
                                        "avatar": "https://cdn.bsky.app/img/avatar/plain/did:plc:wtdzzfgzjpirnk5wvpjutqoy/bafkreiahoixmvuo7blc5dhmul5w64llaeofxfu6qmsu7zewe5jvum2ntdy",
                                        "associated": {
                                            "chat": {
                                                "allowIncoming": "none"
                                            },
                                            "activitySubscription": {
                                                "allowSubscriptions": "mutuals"
                                            }
                                        },
                                        "viewer": {
                                            "muted": false,
                                            "blockedBy": false,
                                            "following": "at://did:plc:ec72yg6n2sydzjvtovvdlxrk/app.bsky.graph.follow/3l7hl6zgkwi25",
                                            "followedBy": "at://did:plc:wtdzzfgzjpirnk5wvpjutqoy/app.bsky.graph.follow/3krd3g33eds23"
                                        },
                                        "labels": [],
                                        "createdAt": "2023-04-24T18:23:37.593Z"
                                    },
                                    {
                                        "did": "did:plc:sgcrgcztzb5eqvajnkigmhhv",
                                        "handle": "bot.oracularhades.com",
                                        "displayName": "hadesbot",
                                        "associated": {
                                            "activitySubscription": {
                                                "allowSubscriptions": "followers"
                                            }
                                        },
                                        "viewer": {
                                            "muted": false,
                                            "blockedBy": false,
                                            "following": "at://did:plc:ec72yg6n2sydzjvtovvdlxrk/app.bsky.graph.follow/3lbsmz2ws6u2s",
                                            "followedBy": "at://did:plc:sgcrgcztzb5eqvajnkigmhhv/app.bsky.graph.follow/3lbsmmyqoxe2d"
                                        },
                                        "labels": [],
                                        "createdAt": "2024-11-18T15:28:50.050Z"
                                    }
                                ]
                            }
                        },
                        "labels": [],
                        "createdAt": "2023-04-22T22:44:04.316Z",
                        "chatDisabled": false,
                        "kind": {
                            "$type": "chat.bsky.actor.defs#directConvoMember"
                        }
                    }
                ],
                "cursor": "2222222226zlb"
            }
            """;

        GetConversationMembersResponse? getConversationMembersResponse = JsonSerializer.Deserialize<GetConversationMembersResponse>(json, BlueskyJsonSerializerOptions.Options);
        Assert.NotNull(getConversationMembersResponse);
        Assert.Equal(2, getConversationMembersResponse.Members.Count);
        Assert.Equal("2222222226zlb", getConversationMembersResponse.Cursor);
    }

    [Fact]
    public void ListConversationsResponseWithDirectAndGroupConversationsDeserializesCorrectly()
    {
        string json = """
            {
              "convos": [
                {
                  "id": "3moh3ak5nq32s",
                  "rev": "2222224rdcznj",
                  "members": [
                    {
                      "did": "did:plc:mvgsfujvam5iekxlk3howidu",
                      "handle": "anotherbot.idunno.blue",
                      "displayName": "Test Bot #2",
                      "avatar": "https://cdn.bsky.app/img/avatar/plain/did:plc:mvgsfujvam5iekxlk3howidu/bafkreigom4rh7v3ruxzhjbivwtrwzs5gr54lc2at2wueemji6d4aljo7ua",
                      "associated": {
                        "lists": 0,
                        "feedgens": 0,
                        "starterPacks": 0,
                        "labeler": false,
                        "activitySubscription": {
                          "allowSubscriptions": "followers"
                        }
                      },
                      "viewer": {
                        "muted": false,
                        "blockedBy": false,
                        "following": "at://did:plc:ec72yg6n2sydzjvtovvdlxrk/app.bsky.graph.follow/3lcsswdqzqy2a",
                        "followedBy": "at://did:plc:mvgsfujvam5iekxlk3howidu/app.bsky.graph.follow/3lbueooovu32s",
                        "knownFollowers": {
                          "count": 1,
                          "followers": [
                            {
                              "did": "did:plc:wtdzzfgzjpirnk5wvpjutqoy",
                              "handle": "oracularhades.com",
                              "displayName": "Josh",
                              "avatar": "https://cdn.bsky.app/img/avatar/plain/did:plc:wtdzzfgzjpirnk5wvpjutqoy/bafkreiahoixmvuo7blc5dhmul5w64llaeofxfu6qmsu7zewe5jvum2ntdy",
                              "associated": {
                                "chat": {
                                  "allowIncoming": "none"
                                },
                                "activitySubscription": {
                                  "allowSubscriptions": "mutuals"
                                }
                              },
                              "viewer": {
                                "muted": false,
                                "blockedBy": false,
                                "following": "at://did:plc:ec72yg6n2sydzjvtovvdlxrk/app.bsky.graph.follow/3l7hl6zgkwi25",
                                "followedBy": "at://did:plc:wtdzzfgzjpirnk5wvpjutqoy/app.bsky.graph.follow/3krd3g33eds23"
                              },
                              "labels": [],
                              "createdAt": "2023-04-24T18:23:37.593Z"
                            }
                          ]
                        }
                      },
                      "labels": [],
                      "createdAt": "2024-11-26T15:39:59.145Z",
                      "chatDisabled": false,
                      "kind": {
                        "role": "owner",
                        "$type": "chat.bsky.actor.defs#groupConvoMember"
                      }
                    },
                    {
                      "did": "did:plc:ec72yg6n2sydzjvtovvdlxrk",
                      "handle": "bot.idunno.blue",
                      "displayName": "Test Bot",
                      "avatar": "https://cdn.bsky.app/img/avatar/plain/did:plc:ec72yg6n2sydzjvtovvdlxrk/bafkreig5ujmxrechgakn4ukf37oj6mlpivukqfbgpuhb3pqjmdkxxtjpnq",
                      "associated": {
                        "lists": 2,
                        "feedgens": 0,
                        "starterPacks": 1,
                        "labeler": false,
                        "activitySubscription": {
                          "allowSubscriptions": "followers"
                        }
                      },
                      "viewer": {
                        "muted": false,
                        "blockedBy": false
                      },
                      "labels": [
                        {
                          "src": "did:plc:ec72yg6n2sydzjvtovvdlxrk",
                          "uri": "at://did:plc:ec72yg6n2sydzjvtovvdlxrk/app.bsky.actor.profile/self",
                          "cid": "bafyreig52l5b45222ijf3sf5jggvcxcvmpmetlswqyoqzh2dlfyoge74oq",
                          "val": "bot",
                          "cts": "1970-01-01T00:00:00.000Z"
                        },
                        {
                          "src": "did:plc:ec72yg6n2sydzjvtovvdlxrk",
                          "uri": "at://did:plc:ec72yg6n2sydzjvtovvdlxrk/app.bsky.actor.profile/self",
                          "cid": "bafyreig52l5b45222ijf3sf5jggvcxcvmpmetlswqyoqzh2dlfyoge74oq",
                          "val": "!no-unauthenticated",
                          "cts": "1970-01-01T00:00:00.000Z"
                        }
                      ],
                      "createdAt": "2024-03-19T13:00:19.046Z",
                      "chatDisabled": false,
                      "kind": {
                        "role": "standard",
                        "addedBy": {
                          "did": "did:plc:mvgsfujvam5iekxlk3howidu",
                          "handle": "anotherbot.idunno.blue",
                          "displayName": "Test Bot #2",
                          "avatar": "https://cdn.bsky.app/img/avatar/plain/did:plc:mvgsfujvam5iekxlk3howidu/bafkreigom4rh7v3ruxzhjbivwtrwzs5gr54lc2at2wueemji6d4aljo7ua",
                          "associated": {
                            "lists": 0,
                            "feedgens": 0,
                            "starterPacks": 0,
                            "labeler": false,
                            "activitySubscription": {
                              "allowSubscriptions": "followers"
                            }
                          },
                          "viewer": {
                            "muted": false,
                            "blockedBy": false,
                            "following": "at://did:plc:ec72yg6n2sydzjvtovvdlxrk/app.bsky.graph.follow/3lcsswdqzqy2a",
                            "followedBy": "at://did:plc:mvgsfujvam5iekxlk3howidu/app.bsky.graph.follow/3lbueooovu32s",
                            "knownFollowers": {
                              "count": 1,
                              "followers": [
                                {
                                  "did": "did:plc:wtdzzfgzjpirnk5wvpjutqoy",
                                  "handle": "oracularhades.com",
                                  "displayName": "Josh",
                                  "avatar": "https://cdn.bsky.app/img/avatar/plain/did:plc:wtdzzfgzjpirnk5wvpjutqoy/bafkreiahoixmvuo7blc5dhmul5w64llaeofxfu6qmsu7zewe5jvum2ntdy",
                                  "associated": {
                                    "chat": {
                                      "allowIncoming": "none"
                                    },
                                    "activitySubscription": {
                                      "allowSubscriptions": "mutuals"
                                    }
                                  },
                                  "viewer": {
                                    "muted": false,
                                    "blockedBy": false,
                                    "following": "at://did:plc:ec72yg6n2sydzjvtovvdlxrk/app.bsky.graph.follow/3l7hl6zgkwi25",
                                    "followedBy": "at://did:plc:wtdzzfgzjpirnk5wvpjutqoy/app.bsky.graph.follow/3krd3g33eds23"
                                  },
                                  "labels": [],
                                  "createdAt": "2023-04-24T18:23:37.593Z"
                                }
                              ]
                            }
                          },
                          "labels": [],
                          "createdAt": "2024-11-26T15:39:59.145Z",
                          "chatDisabled": false
                        },
                        "$type": "chat.bsky.actor.defs#groupConvoMember"
                      }
                    }
                  ],
                  "lastMessage": {
                    "id": "3moh3aswtrx2h",
                    "rev": "2222224rdcznj",
                    "sender": {
                      "did": "did:plc:mvgsfujvam5iekxlk3howidu"
                    },
                    "text": "This is a group chat",
                    "reactions": [],
                    "sentAt": "2026-06-17T00:40:14.774Z",
                    "$type": "chat.bsky.convo.defs#messageView"
                  },
                  "unreadCount": 1,
                  "status": "request",
                  "muted": false,
                  "kind": {
                    "name": "Test Group Chat",
                    "joinLink": {
                      "code": "OyiHQqB",
                      "enabledStatus": "enabled",
                      "requireApproval": false,
                      "joinRule": "anyone",
                      "createdAt": "2026-06-17T12:47:59.822Z",
                      "$type": "chat.bsky.group.defs#joinLinkView"
                    },
                    "lockStatus": "unlocked",
                    "lockStatusModerationOverride": false,
                    "memberCount": 2,
                    "memberLimit": 50,
                    "createdAt": "2026-06-17T00:40:05.563Z",
                    "$type": "chat.bsky.convo.defs#groupConvo"
                  }
                },
                {
                  "id": "3kt4jgm5nc32q",
                  "rev": "2222223gg456s",
                  "members": [
                    {
                      "did": "did:plc:ec72yg6n2sydzjvtovvdlxrk",
                      "handle": "bot.idunno.blue",
                      "displayName": "Test Bot",
                      "avatar": "https://cdn.bsky.app/img/avatar/plain/did:plc:ec72yg6n2sydzjvtovvdlxrk/bafkreig5ujmxrechgakn4ukf37oj6mlpivukqfbgpuhb3pqjmdkxxtjpnq",
                      "associated": {
                        "lists": 2,
                        "feedgens": 0,
                        "starterPacks": 1,
                        "labeler": false,
                        "activitySubscription": {
                          "allowSubscriptions": "followers"
                        }
                      },
                      "viewer": {
                        "muted": false,
                        "blockedBy": false
                      },
                      "labels": [
                        {
                          "src": "did:plc:ec72yg6n2sydzjvtovvdlxrk",
                          "uri": "at://did:plc:ec72yg6n2sydzjvtovvdlxrk/app.bsky.actor.profile/self",
                          "cid": "bafyreig52l5b45222ijf3sf5jggvcxcvmpmetlswqyoqzh2dlfyoge74oq",
                          "val": "bot",
                          "cts": "1970-01-01T00:00:00.000Z"
                        },
                        {
                          "src": "did:plc:ec72yg6n2sydzjvtovvdlxrk",
                          "uri": "at://did:plc:ec72yg6n2sydzjvtovvdlxrk/app.bsky.actor.profile/self",
                          "cid": "bafyreig52l5b45222ijf3sf5jggvcxcvmpmetlswqyoqzh2dlfyoge74oq",
                          "val": "!no-unauthenticated",
                          "cts": "1970-01-01T00:00:00.000Z"
                        }
                      ],
                      "createdAt": "2024-03-19T13:00:19.046Z",
                      "chatDisabled": false,
                      "kind": {
                        "$type": "chat.bsky.actor.defs#directConvoMember"
                      }
                    },
                    {
                      "did": "did:plc:hfgp6pj3akhqxntgqwramlbg",
                      "handle": "blowdart.me",
                      "displayName": "Barry Dorrans",
                      "avatar": "https://cdn.bsky.app/img/avatar/plain/did:plc:hfgp6pj3akhqxntgqwramlbg/bafkreicwjaromkjs4jrd5uqznacfgzvhnob2il5fwywxqopbnhfb74n27m",
                      "associated": {
                        "lists": 2,
                        "feedgens": 1,
                        "starterPacks": 0,
                        "labeler": false,
                        "chat": {
                          "allowIncoming": "all"
                        },
                        "activitySubscription": {
                          "allowSubscriptions": "followers"
                        },
                        "germ": {
                          "showButtonTo": "usersIFollow",
                          "messageMeUrl": "https://landing.ger.mx/newUser"
                        }
                      },
                      "viewer": {
                        "muted": false,
                        "blockedBy": false,
                        "following": "at://did:plc:ec72yg6n2sydzjvtovvdlxrk/app.bsky.graph.follow/3kqxzemnnc425",
                        "followedBy": "at://did:plc:hfgp6pj3akhqxntgqwramlbg/app.bsky.graph.follow/3ko2gwpx37l2m",
                        "knownFollowers": {
                          "count": 2,
                          "followers": [
                            {
                              "did": "did:plc:wtdzzfgzjpirnk5wvpjutqoy",
                              "handle": "oracularhades.com",
                              "displayName": "Josh",
                              "avatar": "https://cdn.bsky.app/img/avatar/plain/did:plc:wtdzzfgzjpirnk5wvpjutqoy/bafkreiahoixmvuo7blc5dhmul5w64llaeofxfu6qmsu7zewe5jvum2ntdy",
                              "associated": {
                                "chat": {
                                  "allowIncoming": "none"
                                },
                                "activitySubscription": {
                                  "allowSubscriptions": "mutuals"
                                }
                              },
                              "viewer": {
                                "muted": false,
                                "blockedBy": false,
                                "following": "at://did:plc:ec72yg6n2sydzjvtovvdlxrk/app.bsky.graph.follow/3l7hl6zgkwi25",
                                "followedBy": "at://did:plc:wtdzzfgzjpirnk5wvpjutqoy/app.bsky.graph.follow/3krd3g33eds23"
                              },
                              "labels": [],
                              "createdAt": "2023-04-24T18:23:37.593Z"
                            },
                            {
                              "did": "did:plc:sgcrgcztzb5eqvajnkigmhhv",
                              "handle": "bot.oracularhades.com",
                              "displayName": "hadesbot",
                              "associated": {
                                "activitySubscription": {
                                  "allowSubscriptions": "followers"
                                }
                              },
                              "viewer": {
                                "muted": false,
                                "blockedBy": false,
                                "following": "at://did:plc:ec72yg6n2sydzjvtovvdlxrk/app.bsky.graph.follow/3lbsmz2ws6u2s",
                                "followedBy": "at://did:plc:sgcrgcztzb5eqvajnkigmhhv/app.bsky.graph.follow/3lbsmmyqoxe2d"
                              },
                              "labels": [],
                              "createdAt": "2024-11-18T15:28:50.050Z"
                            }
                          ]
                        }
                      },
                      "labels": [],
                      "createdAt": "2023-04-22T22:44:04.316Z",
                      "chatDisabled": false,
                      "kind": {
                        "$type": "chat.bsky.actor.defs#directConvoMember"
                      }
                    }
                  ],
                  "lastMessage": {
                    "id": "3lqzgq3susj24",
                    "rev": "2222223gg456s",
                    "sender": {
                      "did": "did:plc:ec72yg6n2sydzjvtovvdlxrk"
                    },
                    "text": "Embedded post test",
                    "facets": [],
                    "embed": {
                      "record": {
                        "uri": "at://did:plc:hfgp6pj3akhqxntgqwramlbg/app.bsky.feed.post/3lqxyqocwx22m",
                        "cid": "bafyreih5lrbmanjn3rokps54pp5kjrg3ioj6q64ohvpz3pjai5edkgaqim",
                        "author": {
                          "did": "did:plc:hfgp6pj3akhqxntgqwramlbg",
                          "handle": "blowdart.me",
                          "displayName": "Barry Dorrans",
                          "pronouns": "He/Him",
                          "avatar": "https://cdn.bsky.app/img/avatar/plain/did:plc:hfgp6pj3akhqxntgqwramlbg/bafkreicwjaromkjs4jrd5uqznacfgzvhnob2il5fwywxqopbnhfb74n27m",
                          "associated": {
                            "chat": {
                              "allowIncoming": "all"
                            },
                            "activitySubscription": {
                              "allowSubscriptions": "followers"
                            },
                            "germ": {
                              "showButtonTo": "usersIFollow",
                              "messageMeUrl": "https://landing.ger.mx/newUser"
                            }
                          },
                          "viewer": {
                            "muted": false,
                            "blockedBy": false,
                            "following": "at://did:plc:ec72yg6n2sydzjvtovvdlxrk/app.bsky.graph.follow/3kqxzemnnc425",
                            "followedBy": "at://did:plc:hfgp6pj3akhqxntgqwramlbg/app.bsky.graph.follow/3ko2gwpx37l2m"
                          },
                          "labels": [],
                          "createdAt": "2023-04-22T22:44:04.316Z"
                        },
                        "value": {
                          "$type": "app.bsky.feed.post",
                          "createdAt": "2025-06-06T23:34:52.283Z",
                          "embed": {
                            "$type": "app.bsky.embed.images",
                            "images": [
                              {
                                "alt": "An faked image of Pete Davidson walking next to Donald Trump ",
                                "aspectRatio": {
                                  "height": 1118,
                                  "width": 1206
                                },
                                "image": {
                                  "$type": "blob",
                                  "ref": {
                                    "$link": "bafkreicktaxlwu3zsktkzmofc7vko4furwcxyroaz45qn3cmzxwwuuhsay"
                                  },
                                  "mimeType": "image/jpeg",
                                  "size": 375073
                                }
                              }
                            ]
                          },
                          "langs": [
                            "en"
                          ],
                          "text": "That was quick"
                        },
                        "labels": [],
                        "replyCount": 1,
                        "repostCount": 14,
                        "likeCount": 54,
                        "embeds": [
                          {
                            "images": [
                              {
                                "thumb": "https://cdn.bsky.app/img/feed_thumbnail/plain/did:plc:hfgp6pj3akhqxntgqwramlbg/bafkreicktaxlwu3zsktkzmofc7vko4furwcxyroaz45qn3cmzxwwuuhsay",
                                "fullsize": "https://cdn.bsky.app/img/feed_fullsize/plain/did:plc:hfgp6pj3akhqxntgqwramlbg/bafkreicktaxlwu3zsktkzmofc7vko4furwcxyroaz45qn3cmzxwwuuhsay",
                                "alt": "An faked image of Pete Davidson walking next to Donald Trump ",
                                "aspectRatio": {
                                  "height": 1118,
                                  "width": 1206
                                }
                              }
                            ],
                            "$type": "app.bsky.embed.images#view"
                          }
                        ],
                        "indexedAt": "2025-06-06T23:34:55.648Z",
                        "$type": "app.bsky.embed.record#viewRecord"
                      },
                      "$type": "app.bsky.embed.record#view"
                    },
                    "reactions": [],
                    "sentAt": "2025-06-07T13:17:45.015Z",
                    "$type": "chat.bsky.convo.defs#messageView"
                  },
                  "lastReaction": {
                    "message": {
                      "id": "3lcweif2c622k",
                      "rev": "22222236abbpj",
                      "sender": {
                        "did": "did:plc:ec72yg6n2sydzjvtovvdlxrk"
                      },
                      "text": "@blowdart.me cached",
                      "facets": [
                        {
                          "index": {
                            "byteEnd": 12,
                            "byteStart": 0
                          },
                          "features": [
                            {
                              "did": "did:plc:hfgp6pj3akhqxntgqwramlbg",
                              "$type": "app.bsky.richtext.facet#mention"
                            }
                          ]
                        }
                      ],
                      "reactions": [
                        {
                          "createdAt": "2025-04-04T00:33:56.379Z",
                          "value": "💩",
                          "sender": {
                            "did": "did:plc:ec72yg6n2sydzjvtovvdlxrk"
                          },
                          "$type": "chat.bsky.convo.defs#reactionView"
                        }
                      ],
                      "sentAt": "2024-12-10T04:06:56.755Z",
                      "$type": "chat.bsky.convo.defs#messageView"
                    },
                    "reaction": {
                      "createdAt": "2025-04-04T00:33:56.379Z",
                      "value": "💩",
                      "sender": {
                        "did": "did:plc:ec72yg6n2sydzjvtovvdlxrk"
                      },
                      "$type": "chat.bsky.convo.defs#reactionView"
                    },
                    "$type": "chat.bsky.convo.defs#messageAndReactionView"
                  },
                  "unreadCount": 0,
                  "status": "accepted",
                  "muted": false,
                  "kind": {
                    "$type": "chat.bsky.convo.defs#directConvo"
                  }
                },
                {
                  "id": "3lcsswcbumw23",
                  "rev": "22222236a36xl",
                  "members": [
                    {
                      "did": "did:plc:ec72yg6n2sydzjvtovvdlxrk",
                      "handle": "bot.idunno.blue",
                      "displayName": "Test Bot",
                      "avatar": "https://cdn.bsky.app/img/avatar/plain/did:plc:ec72yg6n2sydzjvtovvdlxrk/bafkreig5ujmxrechgakn4ukf37oj6mlpivukqfbgpuhb3pqjmdkxxtjpnq",
                      "associated": {
                        "lists": 2,
                        "feedgens": 0,
                        "starterPacks": 1,
                        "labeler": false,
                        "activitySubscription": {
                          "allowSubscriptions": "followers"
                        }
                      },
                      "viewer": {
                        "muted": false,
                        "blockedBy": false
                      },
                      "labels": [
                        {
                          "src": "did:plc:ec72yg6n2sydzjvtovvdlxrk",
                          "uri": "at://did:plc:ec72yg6n2sydzjvtovvdlxrk/app.bsky.actor.profile/self",
                          "cid": "bafyreig52l5b45222ijf3sf5jggvcxcvmpmetlswqyoqzh2dlfyoge74oq",
                          "val": "bot",
                          "cts": "1970-01-01T00:00:00.000Z"
                        },
                        {
                          "src": "did:plc:ec72yg6n2sydzjvtovvdlxrk",
                          "uri": "at://did:plc:ec72yg6n2sydzjvtovvdlxrk/app.bsky.actor.profile/self",
                          "cid": "bafyreig52l5b45222ijf3sf5jggvcxcvmpmetlswqyoqzh2dlfyoge74oq",
                          "val": "!no-unauthenticated",
                          "cts": "1970-01-01T00:00:00.000Z"
                        }
                      ],
                      "createdAt": "2024-03-19T13:00:19.046Z",
                      "chatDisabled": false,
                      "kind": {
                        "$type": "chat.bsky.actor.defs#directConvoMember"
                      }
                    },
                    {
                      "did": "did:plc:mvgsfujvam5iekxlk3howidu",
                      "handle": "anotherbot.idunno.blue",
                      "displayName": "Test Bot #2",
                      "avatar": "https://cdn.bsky.app/img/avatar/plain/did:plc:mvgsfujvam5iekxlk3howidu/bafkreigom4rh7v3ruxzhjbivwtrwzs5gr54lc2at2wueemji6d4aljo7ua",
                      "associated": {
                        "lists": 0,
                        "feedgens": 0,
                        "starterPacks": 0,
                        "labeler": false,
                        "activitySubscription": {
                          "allowSubscriptions": "followers"
                        }
                      },
                      "viewer": {
                        "muted": false,
                        "blockedBy": false,
                        "following": "at://did:plc:ec72yg6n2sydzjvtovvdlxrk/app.bsky.graph.follow/3lcsswdqzqy2a",
                        "followedBy": "at://did:plc:mvgsfujvam5iekxlk3howidu/app.bsky.graph.follow/3lbueooovu32s",
                        "knownFollowers": {
                          "count": 1,
                          "followers": [
                            {
                              "did": "did:plc:wtdzzfgzjpirnk5wvpjutqoy",
                              "handle": "oracularhades.com",
                              "displayName": "Josh",
                              "avatar": "https://cdn.bsky.app/img/avatar/plain/did:plc:wtdzzfgzjpirnk5wvpjutqoy/bafkreiahoixmvuo7blc5dhmul5w64llaeofxfu6qmsu7zewe5jvum2ntdy",
                              "associated": {
                                "chat": {
                                  "allowIncoming": "none"
                                },
                                "activitySubscription": {
                                  "allowSubscriptions": "mutuals"
                                }
                              },
                              "viewer": {
                                "muted": false,
                                "blockedBy": false,
                                "following": "at://did:plc:ec72yg6n2sydzjvtovvdlxrk/app.bsky.graph.follow/3l7hl6zgkwi25",
                                "followedBy": "at://did:plc:wtdzzfgzjpirnk5wvpjutqoy/app.bsky.graph.follow/3krd3g33eds23"
                              },
                              "labels": [],
                              "createdAt": "2023-04-24T18:23:37.593Z"
                            }
                          ]
                        }
                      },
                      "labels": [],
                      "createdAt": "2024-11-26T15:39:59.145Z",
                      "chatDisabled": false,
                      "kind": {
                        "$type": "chat.bsky.actor.defs#directConvoMember"
                      }
                    }
                  ],
                  "lastMessage": {
                    "id": "3llx3iwuxq32n",
                    "rev": "22222236a36xl",
                    "sender": {
                      "did": "did:plc:mvgsfujvam5iekxlk3howidu"
                    },
                    "text": "You smell",
                    "reactions": [],
                    "sentAt": "2025-04-03T23:45:36.184Z",
                    "$type": "chat.bsky.convo.defs#messageView"
                  },
                  "unreadCount": 0,
                  "status": "request",
                  "muted": false,
                  "kind": {
                    "$type": "chat.bsky.convo.defs#directConvo"
                  }
                }
              ]
            }
            """;

        ListConversationsResponse? conversations = JsonSerializer.Deserialize<ListConversationsResponse>(json, options: BlueskyJsonSerializerOptions.Options);

        Assert.NotNull(conversations);
        Assert.Equal(3, conversations.Conversations.Count);

        ConversationView conversationView = conversations.Conversations.ElementAt(0);

        Assert.Equal("3moh3ak5nq32s", conversationView.Id);
        Assert.Equal("2222224rdcznj", conversationView.Revision);

        Assert.Equal(2, conversationView.Members.Count);

        ProfileViewBasic conversationMemberProfileView = conversationView.Members.ElementAt(0);
        Assert.Equal("did:plc:mvgsfujvam5iekxlk3howidu", conversationMemberProfileView.Did);
        Assert.Equal("anotherbot.idunno.blue", conversationMemberProfileView.Handle);
        Assert.Equal("Test Bot #2", conversationMemberProfileView.DisplayName);
        Assert.Equal(
            new Uri("https://cdn.bsky.app/img/avatar/plain/did:plc:mvgsfujvam5iekxlk3howidu/bafkreigom4rh7v3ruxzhjbivwtrwzs5gr54lc2at2wueemji6d4aljo7ua"),
            conversationMemberProfileView.Avatar);
        Assert.NotNull(conversationMemberProfileView.Associated);
        Assert.NotNull(conversationMemberProfileView.Viewer);
        Assert.Empty(conversationMemberProfileView.Labels);
        Assert.Equal(DateTimeOffset.Parse("2024-11-26T15:39:59.145Z"), conversationMemberProfileView.CreatedAt);
        Assert.False(conversationMemberProfileView.ChatDisabled);

        Assert.IsType<GroupConversationMember>(conversationMemberProfileView.Kind);
        GroupConversationMember? memberKind = conversationMemberProfileView.Kind as GroupConversationMember;
        Assert.Equal(Chat.Actor.MemberRole.Owner, memberKind!.Role);

        conversationMemberProfileView = conversationView.Members.ElementAt(1);

        Assert.IsType<GroupConversationMember>(conversationMemberProfileView.Kind);
        memberKind = conversationMemberProfileView.Kind as GroupConversationMember;
        Assert.Equal(Chat.Actor.MemberRole.Standard, memberKind!.Role);
        Assert.NotNull(memberKind.AddedBy);
        Assert.Equal(new Did("did:plc:mvgsfujvam5iekxlk3howidu"), memberKind.AddedBy.Did);

        Assert.NotNull(conversationView.LastMessage);
        Assert.Equal("3moh3aswtrx2h", conversationView.LastMessage.Id);
        Assert.Equal("2222224rdcznj", conversationView.LastMessage.Revision);

        Assert.IsType<MessageView>(conversationView.LastMessage);
        var lastmessage = conversationView.LastMessage as MessageView;
        Assert.Equal("did:plc:mvgsfujvam5iekxlk3howidu", lastmessage!.Sender.Did);
        Assert.Equal("This is a group chat", lastmessage.Text);
        Assert.Empty(lastmessage.Reactions);
        Assert.Equal(DateTimeOffset.Parse("2026-06-17T00:40:14.774Z"), lastmessage.SentAt);

        Assert.Equal(1, conversationView.UnreadCount);
        Assert.Equal(ConversationStatus.Requested, conversationView.Status);
        Assert.False(conversationView.Muted);

        Assert.IsType<GroupConversation>(conversationView.Kind);
        var groupConversation = conversationView.Kind as GroupConversation;
        Assert.Equal("Test Group Chat", groupConversation!.Name);
        Assert.NotNull(groupConversation.JoinLink);

        Assert.Equal("OyiHQqB", groupConversation.JoinLink.Code);
        Assert.Equal(Group.LinkEnabledStatus.Enabled, groupConversation.JoinLink.EnabledStatus);
        Assert.False(groupConversation.JoinLink.RequireApproval);
        Assert.Equal(Group.JoinRule.Anyone, groupConversation.JoinLink.JoinRule);
        Assert.Equal(DateTimeOffset.Parse("2026-06-17T12:47:59.822Z"), groupConversation.JoinLink.CreatedAt);

        Assert.Equal(ConversationLockStatus.Unlocked, groupConversation.LockStatus);
        Assert.False(groupConversation.LockStatusModerationOverride);
        Assert.Equal(2, groupConversation.MemberCount);
        Assert.Equal(50, groupConversation.MemberLimit);
        Assert.Equal(DateTimeOffset.Parse("2026-06-17T00:40:05.563Z"), groupConversation.CreatedAt);

        conversationView = conversations.Conversations.ElementAt(1);

        Assert.IsType<DirectConversationMember>(conversationView.Members.ElementAt(0).Kind);
        Assert.IsType<DirectConversation>(conversationView.Kind);
    }
}
