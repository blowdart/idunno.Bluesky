// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;

using idunno.Bluesky.Chat.Convo.Model;

namespace idunno.Bluesky.Serialization.Test.Conversations;

public class LogTests
{
    readonly string _json = """
        {
            "cursor": "2222224ssuhx3",
            "logs": [
                {
                    "convoId": "3kt4jgm5nc32q",
                    "rev": "2222222226zl7",
                    "$type": "chat.bsky.convo.defs#logBeginConvo"
                },
                {
                    "convoId": "3kt4jgm5nc32q",
                    "rev": "2222222c4irts",
                    "message": {
                        "id": "3l7dmb2oa4p26",
                        "rev": "2222222c4irts",
                        "sender": {
                            "did": "did:plc:hfgp6pj3akhqxntgqwramlbg"
                        },
                        "text": "You smell funny",
                        "reactions": [],
                        "sentAt": "2024-10-25T12:49:50.984Z",
                        "$type": "chat.bsky.convo.defs#messageView"
                    },
                    "relatedProfiles": [],
                    "$type": "chat.bsky.convo.defs#logCreateMessage"
                },
                {
                    "convoId": "3kt4jgm5nc32q",
                    "rev": "2222222c4is56",
                    "message": {
                        "id": "3l7dmbdgzca2p",
                        "rev": "2222222c4is56",
                        "sender": {
                            "did": "did:plc:hfgp6pj3akhqxntgqwramlbg"
                        },
                        "text": "Your code base sucks",
                        "reactions": [],
                        "sentAt": "2024-10-25T12:50:00.191Z",
                        "$type": "chat.bsky.convo.defs#messageView"
                    },
                    "relatedProfiles": [],
                    "$type": "chat.bsky.convo.defs#logCreateMessage"
                },
                {
                    "convoId": "3kt4jgm5nc32q",
                    "rev": "2222222c4isrk",
                    "message": {
                        "id": "3l7dmbvxh2m23",
                        "rev": "2222222c4isrk",
                        "sender": {
                            "did": "did:plc:hfgp6pj3akhqxntgqwramlbg"
                        },
                        "text": "But at least it’s strongly typed",
                        "reactions": [],
                        "sentAt": "2024-10-25T12:50:19.597Z",
                        "$type": "chat.bsky.convo.defs#messageView"
                    },
                    "relatedProfiles": [],
                    "$type": "chat.bsky.convo.defs#logCreateMessage"
                },
                {
                    "convoId": "3kt4jgm5nc32q",
                    "rev": "2222222c4kh2v",
                    "message": {
                        "id": "3l7dnormcv22u",
                        "rev": "2222222c4kh2v",
                        "sender": {
                            "did": "did:plc:ec72yg6n2sydzjvtovvdlxrk"
                        },
                        "text": "But the DM bug only happens with long messages, so let's test that\n\nLorem ipsum dolor sit amet, consectetur adipiscing elit, sed do eiusmod tempor incididunt ut labore et dolore magna aliqua. Ut enim ad minim veniam, quis nostrud exercitation ullamco laboris nisi ut aliquip ex ea commodo consequat. Duis aute irure dolor in reprehenderit in voluptate velit esse cillum dolore eu fugiat nulla pariatur. Excepteur sint occaecat cupidatat non proident, sunt in culpa qui officia deserunt mollit anim id est laborum",
                        "reactions": [],
                        "sentAt": "2024-10-25T13:15:24.989Z",
                        "$type": "chat.bsky.convo.defs#messageView"
                    },
                    "relatedProfiles": [],
                    "$type": "chat.bsky.convo.defs#logCreateMessage"
                },
                {
                    "convoId": "3kt4jgm5nc32q",
                    "rev": "2222222c4kjja",
                    "message": {
                        "id": "3l7dnqrzrqy22",
                        "rev": "2222222c4kjja",
                        "sender": {
                            "did": "did:plc:hfgp6pj3akhqxntgqwramlbg"
                        },
                        "text": "You think you’re so clever with your pseudo Latin.\n\nSed ut perspiciatis unde omnis iste natus error sit voluptatem accusantium doloremque laudantium, totam rem aperiam, eaque ipsa quae ab illo inventore veritatis et quasi architecto beatae vitae dicta sunt explicabo. Nemo enim ipsam voluptatem quia voluptas sit aspernatur aut odit aut fugit, sed quia consequuntur magni dolores eos qui ratione voluptatem sequi nesciunt. Neque porro quisquam est, qui dolorem ipsum quia dolor sit amet, consectetur, adipisci velit, sed quia non numquam eius modi tempora incidunt ut labore et dolore magnam aliquam quaerat voluptatem. Ut enim ad minima veniam, quis nostrum exercitationem ullam corporis suscipit laboriosam, nisi ut aliquid ex ea commodi consequatur? Quis autem vel eum iure reprehenderit qui in ea voluptate velit esse quam nihil molestiae consequatur, vel illum qui dolorem eum fugiat quo voluptas nulla pariatur?",
                        "reactions": [],
                        "sentAt": "2024-10-25T13:16:32.540Z",
                        "$type": "chat.bsky.convo.defs#messageView"
                    },
                    "relatedProfiles": [],
                    "$type": "chat.bsky.convo.defs#logCreateMessage"
                },
                {
                    "convoId": "3kt4jgm5nc32q",
                    "rev": "2222222c4kklc",
                    "message": {
                        "id": "3l7dnrl2oxz2f",
                        "rev": "2222222c4kklc",
                        "sender": {
                            "did": "did:plc:ec72yg6n2sydzjvtovvdlxrk"
                        },
                        "text": "At vero eos et accusamus et iusto odio dignissimos ducimus qui blanditiis praesentium voluptatum deleniti atque corrupti quos dolores et quas molestias excepturi sint occaecati cupiditate non provident, similique sunt in culpa qui officia deserunt mollitia animi, id est laborum et dolorum fuga. Et harum quidem rerum facilis est et expedita distinctio. Nam libero tempore, cum soluta nobis est eligendi optio cumque nihil impedit quo minus id quod maxime placeat facere possimus, omnis voluptas assumenda est, omnis dolor repellendus. Temporibus autem quibusdam et aut officiis debitis aut rerum necessitatibus saepe eveniet ut et voluptates repudiandae sint et molestiae non recusandae. Itaque earum rerum hic tenetur a sapiente delectus, ut aut reiciendis voluptatibus maiores alias consequatur aut perferendis doloribus asperiores repellat.",
                        "reactions": [],
                        "sentAt": "2024-10-25T13:16:58.791Z",
                        "$type": "chat.bsky.convo.defs#messageView"
                    },
                    "relatedProfiles": [],
                    "$type": "chat.bsky.convo.defs#logCreateMessage"
                },
                {
                    "convoId": "3kt4jgm5nc32q",
                    "rev": "2222222c4klom",
                    "message": {
                        "id": "3l7dnsbofrd2u",
                        "rev": "2222222c4klom",
                        "sender": {
                            "did": "did:plc:hfgp6pj3akhqxntgqwramlbg"
                        },
                        "text": "Of course it doesn’t reproduce nicely.",
                        "reactions": [],
                        "sentAt": "2024-10-25T13:17:22.498Z",
                        "$type": "chat.bsky.convo.defs#messageView"
                    },
                    "relatedProfiles": [],
                    "$type": "chat.bsky.convo.defs#logCreateMessage"
                },
                {
                    "convoId": "3kt4jgm5nc32q",
                    "rev": "2222222c4klvu",
                    "message": {
                        "id": "3l7dnsim6262c",
                        "rev": "2222222c4klvu",
                        "sender": {
                            "did": "did:plc:hfgp6pj3akhqxntgqwramlbg"
                        },
                        "text": "Damn it",
                        "reactions": [],
                        "sentAt": "2024-10-25T13:17:29.766Z",
                        "$type": "chat.bsky.convo.defs#messageView"
                    },
                    "relatedProfiles": [],
                    "$type": "chat.bsky.convo.defs#logCreateMessage"
                },
                {
                    "convoId": "3lcsswcbumw23",
                    "rev": "2222222l3uunf",
                    "$type": "chat.bsky.convo.defs#logLeaveConvo"
                },
                {
                    "convoId": "3kt4jgm5nc32q",
                    "rev": "2222222lanttq",
                    "message": {
                        "id": "3lcw4nmzj3w2k",
                        "rev": "2222222lanttq",
                        "sender": {
                            "did": "did:plc:ec72yg6n2sydzjvtovvdlxrk"
                        },
                        "text": "See https://bullshit.com #bullshit @blowdart.me arse",
                        "facets": [
                            {
                                "index": {
                                    "byteEnd": 34,
                                    "byteStart": 25
                                },
                                "features": [
                                    {
                                        "tag": "bullshit",
                                        "$type": "app.bsky.richtext.facet#tag"
                                    }
                                ]
                            },
                            {
                                "index": {
                                    "byteEnd": 24,
                                    "byteStart": 4
                                },
                                "features": [
                                    {
                                        "uri": "https://bullshit.com",
                                        "$type": "app.bsky.richtext.facet#link"
                                    }
                                ]
                            }
                        ],
                        "reactions": [],
                        "sentAt": "2024-12-10T01:46:42.955Z",
                        "$type": "chat.bsky.convo.defs#messageView"
                    },
                    "relatedProfiles": [],
                    "$type": "chat.bsky.convo.defs#logCreateMessage"
                },
                {
                    "convoId": "3kt4jgm5nc32q",
                    "rev": "2222222lbbehp",
                    "message": {
                        "id": "3lcwehe3u4322",
                        "rev": "2222222lbbehp",
                        "sender": {
                            "did": "did:plc:ec72yg6n2sydzjvtovvdlxrk"
                        },
                        "text": "See https://bullshit.com #bullshit @blowdart.me arse",
                        "facets": [
                            {
                                "index": {
                                    "byteEnd": 34,
                                    "byteStart": 25
                                },
                                "features": [
                                    {
                                        "tag": "bullshit",
                                        "$type": "app.bsky.richtext.facet#tag"
                                    }
                                ]
                            },
                            {
                                "index": {
                                    "byteEnd": 24,
                                    "byteStart": 4
                                },
                                "features": [
                                    {
                                        "uri": "https://bullshit.com",
                                        "$type": "app.bsky.richtext.facet#link"
                                    }
                                ]
                            },
                            {
                                "index": {
                                    "byteEnd": 47,
                                    "byteStart": 35
                                },
                                "features": [
                                    {
                                        "did": "did:plc:hfgp6pj3akhqxntgqwramlbg",
                                        "$type": "app.bsky.richtext.facet#mention"
                                    }
                                ]
                            }
                        ],
                        "reactions": [],
                        "sentAt": "2024-12-10T04:06:22.203Z",
                        "$type": "chat.bsky.convo.defs#messageView"
                    },
                    "relatedProfiles": [],
                    "$type": "chat.bsky.convo.defs#logCreateMessage"
                },
                {
                    "convoId": "3kt4jgm5nc32q",
                    "rev": "2222222lbbgoo",
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
                    "relatedProfiles": [],
                    "$type": "chat.bsky.convo.defs#logCreateMessage"
                },
                {
                    "convoId": "3kt4jgm5nc32q",
                    "rev": "222222367ktp5",
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
                    "$type": "chat.bsky.convo.defs#logReadMessage"
                },
                {
                    "convoId": "3kt4jgm5nc32q",
                    "rev": "222222367ktth",
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
                    "$type": "chat.bsky.convo.defs#logReadMessage"
                },
                {
                    "convoId": "3kt4jgm5nc32q",
                    "rev": "222222367xlyt",
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
                    "$type": "chat.bsky.convo.defs#logReadMessage"
                },
                {
                    "convoId": "3lcsswcbumw23",
                    "rev": "22222236a36xm",
                    "message": {
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
                    "relatedProfiles": [],
                    "$type": "chat.bsky.convo.defs#logCreateMessage"
                },
                {
                    "convoId": "3lcsswcbumw23",
                    "rev": "22222236a3dfh",
                    "message": {
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
                    "$type": "chat.bsky.convo.defs#logReadMessage"
                },
                {
                    "convoId": "3kt4jgm5nc32q",
                    "rev": "22222236a3dgr",
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
                    "$type": "chat.bsky.convo.defs#logReadMessage"
                },
                {
                    "convoId": "3kt4jgm5nc32q",
                    "rev": "22222236abbpj",
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
                    "relatedProfiles": [
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
                                "chat": {
                                    "allowIncoming": "following",
                                    "allowGroupInvites": "following"
                                },
                                "activitySubscription": {
                                    "allowSubscriptions": "followers"
                                }
                            },
                            "viewer": {
                                "muted": false,
                                "blockedBy": false,
                                "knownFollowers": {
                                    "count": 4,
                                    "followers": [
                                        {
                                            "did": "did:plc:hfgp6pj3akhqxntgqwramlbg",
                                            "handle": "blowdart.me",
                                            "displayName": "Barry Dorrans",
                                            "pronouns": "He/Him",
                                            "avatar": "https://cdn.bsky.app/img/avatar/plain/did:plc:hfgp6pj3akhqxntgqwramlbg/bafkreicwjaromkjs4jrd5uqznacfgzvhnob2il5fwywxqopbnhfb74n27m",
                                            "associated": {
                                                "chat": {
                                                    "allowIncoming": "all",
                                                    "allowGroupInvites": "all"
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
                                        {
                                            "did": "did:plc:wtdzzfgzjpirnk5wvpjutqoy",
                                            "handle": "oracularhades.com",
                                            "displayName": "Josh",
                                            "avatar": "https://cdn.bsky.app/img/avatar/plain/did:plc:wtdzzfgzjpirnk5wvpjutqoy/bafkreiahoixmvuo7blc5dhmul5w64llaeofxfu6qmsu7zewe5jvum2ntdy",
                                            "associated": {
                                                "chat": {
                                                    "allowIncoming": "none",
                                                    "allowGroupInvites": "none"
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
                                            "did": "did:plc:mvgsfujvam5iekxlk3howidu",
                                            "handle": "anotherbot.idunno.blue",
                                            "displayName": "Test Bot #2",
                                            "avatar": "https://cdn.bsky.app/img/avatar/plain/did:plc:mvgsfujvam5iekxlk3howidu/bafkreigom4rh7v3ruxzhjbivwtrwzs5gr54lc2at2wueemji6d4aljo7ua",
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
                                            "createdAt": "2024-11-26T15:39:59.145Z"
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
                        }
                    ],
                    "$type": "chat.bsky.convo.defs#logAddReaction"
                },
                {
                    "convoId": "3kt4jgm5nc32q",
                    "rev": "22222236abce4",
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
                    "$type": "chat.bsky.convo.defs#logReadMessage"
                },
                {
                    "convoId": "3lcsswcbumw23",
                    "rev": "22222236abfee",
                    "message": {
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
                    "$type": "chat.bsky.convo.defs#logReadMessage"
                },
                {
                    "convoId": "3kt4jgm5nc32q",
                    "rev": "22222236abffp",
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
                    "$type": "chat.bsky.convo.defs#logReadMessage"
                },
                {
                    "convoId": "3kt4jgm5nc32q",
                    "rev": "22222236absoi",
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
                    "$type": "chat.bsky.convo.defs#logReadMessage"
                },
                {
                    "convoId": "3kt4jgm5nc32q",
                    "rev": "22222236ac4qp",
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
                    "$type": "chat.bsky.convo.defs#logReadMessage"
                },
                {
                    "convoId": "3kt4jgm5nc32q",
                    "rev": "22222236af7hx",
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
                    "$type": "chat.bsky.convo.defs#logReadMessage"
                },
                {
                    "convoId": "3kt4jgm5nc32q",
                    "rev": "22222236afdnc",
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
                    "$type": "chat.bsky.convo.defs#logReadMessage"
                },
                {
                    "convoId": "3kt4jgm5nc32q",
                    "rev": "22222236ah3tr",
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
                    "$type": "chat.bsky.convo.defs#logReadMessage"
                },
                {
                    "convoId": "3kt4jgm5nc32q",
                    "rev": "22222236ai237",
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
                    "$type": "chat.bsky.convo.defs#logReadMessage"
                },
                {
                    "convoId": "3kt4jgm5nc32q",
                    "rev": "22222236ai3ll",
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
                    "$type": "chat.bsky.convo.defs#logReadMessage"
                },
                {
                    "convoId": "3kt4jgm5nc32q",
                    "rev": "22222237ae36l",
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
                    "$type": "chat.bsky.convo.defs#logReadMessage"
                },
                {
                    "convoId": "3kt4jgm5nc32q",
                    "rev": "22222237ae3jg",
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
                    "$type": "chat.bsky.convo.defs#logReadMessage"
                },
                {
                    "convoId": "3kt4jgm5nc32q",
                    "rev": "22222237ae4tc",
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
                    "$type": "chat.bsky.convo.defs#logReadMessage"
                },
                {
                    "convoId": "3kt4jgm5nc32q",
                    "rev": "22222237aecfu",
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
                    "$type": "chat.bsky.convo.defs#logReadMessage"
                },
                {
                    "convoId": "3kt4jgm5nc32q",
                    "rev": "22222237aecj6",
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
                    "$type": "chat.bsky.convo.defs#logReadMessage"
                },
                {
                    "convoId": "3kt4jgm5nc32q",
                    "rev": "22222237aecj7",
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
                    "$type": "chat.bsky.convo.defs#logReadMessage"
                },
                {
                    "convoId": "3kt4jgm5nc32q",
                    "rev": "22222237afgkn",
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
                    "$type": "chat.bsky.convo.defs#logReadMessage"
                },
                {
                    "convoId": "3kt4jgm5nc32q",
                    "rev": "22222237afgl4",
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
                    "$type": "chat.bsky.convo.defs#logReadMessage"
                },
                {
                    "convoId": "3kt4jgm5nc32q",
                    "rev": "22222237afglb",
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
                    "$type": "chat.bsky.convo.defs#logReadMessage"
                },
                {
                    "convoId": "3kt4jgm5nc32q",
                    "rev": "22222237afgmm",
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
                    "$type": "chat.bsky.convo.defs#logReadMessage"
                },
                {
                    "convoId": "3kt4jgm5nc32q",
                    "rev": "22222237afgpn",
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
                    "$type": "chat.bsky.convo.defs#logReadMessage"
                },
                {
                    "convoId": "3kt4jgm5nc32q",
                    "rev": "22222237afgpp",
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
                    "$type": "chat.bsky.convo.defs#logReadMessage"
                },
                {
                    "convoId": "3kt4jgm5nc32q",
                    "rev": "22222237aguor",
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
                    "$type": "chat.bsky.convo.defs#logReadMessage"
                },
                {
                    "convoId": "3kt4jgm5nc32q",
                    "rev": "22222237aguuv",
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
                    "$type": "chat.bsky.convo.defs#logReadMessage"
                },
                {
                    "convoId": "3kt4jgm5nc32q",
                    "rev": "2222223gc2uub",
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
                    "$type": "chat.bsky.convo.defs#logReadMessage"
                },
                {
                    "convoId": "3kt4jgm5nc32q",
                    "rev": "2222223gc2uxu",
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
                    "$type": "chat.bsky.convo.defs#logReadMessage"
                },
                {
                    "convoId": "3kt4jgm5nc32q",
                    "rev": "2222223gc2wzu",
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
                    "$type": "chat.bsky.convo.defs#logReadMessage"
                },
                {
                    "convoId": "3kt4jgm5nc32q",
                    "rev": "2222223gc2xcv",
                    "message": {
                        "id": "3lqwm5fnemw2i",
                        "rev": "2222223gc2xcu",
                        "sender": {
                            "did": "did:plc:ec72yg6n2sydzjvtovvdlxrk"
                        },
                        "text": "DM with post attached",
                        "embed": {
                            "record": {
                                "uri": "at://did:plc:7xkc5gsqnj33qs3fsa2mewzj/app.bsky.feed.post/3lqvtxtvtfc2q",
                                "cid": "bafyreihdfqqjhjwctdcf6kxnpk47kr6cc7u2jtxntnb54ibhb43bbbfw54",
                                "author": {
                                    "did": "did:plc:7xkc5gsqnj33qs3fsa2mewzj",
                                    "handle": "anniesexton.com",
                                    "displayName": "Annie Sexton",
                                    "avatar": "https://cdn.bsky.app/img/avatar/plain/did:plc:7xkc5gsqnj33qs3fsa2mewzj/bafkreidxkpgcgmv4ommtzupmc2k2wh6jsc266ram4am6ax7wk2w4m4xtvy",
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
                                        "blockedBy": false
                                    },
                                    "labels": [],
                                    "createdAt": "2024-08-17T14:08:21.885Z"
                                },
                                "value": {
                                    "$type": "app.bsky.feed.post",
                                    "createdAt": "2025-06-06T03:04:04.849Z",
                                    "langs": [
                                        "en"
                                    ],
                                    "reply": {
                                        "parent": {
                                            "cid": "bafyreigk44bcyytcprdcz4iz2n6xmwdizqvj5aqnz4wqxqgse7rcnu7g4e",
                                            "uri": "at://did:plc:vc7f4oafdgxsihk4cry2xpze/app.bsky.feed.post/3lqux7ccnec2p"
                                        },
                                        "root": {
                                            "cid": "bafyreigk44bcyytcprdcz4iz2n6xmwdizqvj5aqnz4wqxqgse7rcnu7g4e",
                                            "uri": "at://did:plc:vc7f4oafdgxsihk4cry2xpze/app.bsky.feed.post/3lqux7ccnec2p"
                                        }
                                    },
                                    "text": "Jerry why you gotta ruin everything I love"
                                },
                                "labels": [],
                                "replyCount": 1,
                                "repostCount": 1,
                                "likeCount": 12,
                                "embeds": [],
                                "indexedAt": "2025-06-06T03:04:06.045Z",
                                "$type": "app.bsky.embed.record#viewRecord"
                            },
                            "$type": "app.bsky.embed.record#view"
                        },
                        "reactions": [],
                        "sentAt": "2025-06-06T10:16:41.022Z",
                        "$type": "chat.bsky.convo.defs#messageView"
                    },
                    "relatedProfiles": [],
                    "$type": "chat.bsky.convo.defs#logCreateMessage"
                },
                {
                    "convoId": "3kt4jgm5nc32q",
                    "rev": "2222223gc3x7q",
                    "message": {
                        "id": "3lqwm5fnemw2i",
                        "rev": "2222223gc2xcu",
                        "sender": {
                            "did": "did:plc:ec72yg6n2sydzjvtovvdlxrk"
                        },
                        "text": "DM with post attached",
                        "embed": {
                            "record": {
                                "uri": "at://did:plc:7xkc5gsqnj33qs3fsa2mewzj/app.bsky.feed.post/3lqvtxtvtfc2q",
                                "cid": "bafyreihdfqqjhjwctdcf6kxnpk47kr6cc7u2jtxntnb54ibhb43bbbfw54",
                                "author": {
                                    "did": "did:plc:7xkc5gsqnj33qs3fsa2mewzj",
                                    "handle": "anniesexton.com",
                                    "displayName": "Annie Sexton",
                                    "avatar": "https://cdn.bsky.app/img/avatar/plain/did:plc:7xkc5gsqnj33qs3fsa2mewzj/bafkreidxkpgcgmv4ommtzupmc2k2wh6jsc266ram4am6ax7wk2w4m4xtvy",
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
                                        "blockedBy": false
                                    },
                                    "labels": [],
                                    "createdAt": "2024-08-17T14:08:21.885Z"
                                },
                                "value": {
                                    "$type": "app.bsky.feed.post",
                                    "createdAt": "2025-06-06T03:04:04.849Z",
                                    "langs": [
                                        "en"
                                    ],
                                    "reply": {
                                        "parent": {
                                            "cid": "bafyreigk44bcyytcprdcz4iz2n6xmwdizqvj5aqnz4wqxqgse7rcnu7g4e",
                                            "uri": "at://did:plc:vc7f4oafdgxsihk4cry2xpze/app.bsky.feed.post/3lqux7ccnec2p"
                                        },
                                        "root": {
                                            "cid": "bafyreigk44bcyytcprdcz4iz2n6xmwdizqvj5aqnz4wqxqgse7rcnu7g4e",
                                            "uri": "at://did:plc:vc7f4oafdgxsihk4cry2xpze/app.bsky.feed.post/3lqux7ccnec2p"
                                        }
                                    },
                                    "text": "Jerry why you gotta ruin everything I love"
                                },
                                "labels": [],
                                "replyCount": 1,
                                "repostCount": 1,
                                "likeCount": 12,
                                "embeds": [],
                                "indexedAt": "2025-06-06T03:04:06.045Z",
                                "$type": "app.bsky.embed.record#viewRecord"
                            },
                            "$type": "app.bsky.embed.record#view"
                        },
                        "reactions": [],
                        "sentAt": "2025-06-06T10:16:41.022Z",
                        "$type": "chat.bsky.convo.defs#messageView"
                    },
                    "$type": "chat.bsky.convo.defs#logReadMessage"
                },
                {
                    "convoId": "3lcsswcbumw23",
                    "rev": "2222223gc3xae",
                    "message": {
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
                    "$type": "chat.bsky.convo.defs#logReadMessage"
                },
                {
                    "convoId": "3kt4jgm5nc32q",
                    "rev": "2222223gc44hs",
                    "message": {
                        "id": "3lqwm5fnemw2i",
                        "rev": "2222223gc2xcu",
                        "sender": {
                            "did": "did:plc:ec72yg6n2sydzjvtovvdlxrk"
                        },
                        "text": "DM with post attached",
                        "embed": {
                            "record": {
                                "uri": "at://did:plc:7xkc5gsqnj33qs3fsa2mewzj/app.bsky.feed.post/3lqvtxtvtfc2q",
                                "cid": "bafyreihdfqqjhjwctdcf6kxnpk47kr6cc7u2jtxntnb54ibhb43bbbfw54",
                                "author": {
                                    "did": "did:plc:7xkc5gsqnj33qs3fsa2mewzj",
                                    "handle": "anniesexton.com",
                                    "displayName": "Annie Sexton",
                                    "avatar": "https://cdn.bsky.app/img/avatar/plain/did:plc:7xkc5gsqnj33qs3fsa2mewzj/bafkreidxkpgcgmv4ommtzupmc2k2wh6jsc266ram4am6ax7wk2w4m4xtvy",
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
                                        "blockedBy": false
                                    },
                                    "labels": [],
                                    "createdAt": "2024-08-17T14:08:21.885Z"
                                },
                                "value": {
                                    "$type": "app.bsky.feed.post",
                                    "createdAt": "2025-06-06T03:04:04.849Z",
                                    "langs": [
                                        "en"
                                    ],
                                    "reply": {
                                        "parent": {
                                            "cid": "bafyreigk44bcyytcprdcz4iz2n6xmwdizqvj5aqnz4wqxqgse7rcnu7g4e",
                                            "uri": "at://did:plc:vc7f4oafdgxsihk4cry2xpze/app.bsky.feed.post/3lqux7ccnec2p"
                                        },
                                        "root": {
                                            "cid": "bafyreigk44bcyytcprdcz4iz2n6xmwdizqvj5aqnz4wqxqgse7rcnu7g4e",
                                            "uri": "at://did:plc:vc7f4oafdgxsihk4cry2xpze/app.bsky.feed.post/3lqux7ccnec2p"
                                        }
                                    },
                                    "text": "Jerry why you gotta ruin everything I love"
                                },
                                "labels": [],
                                "replyCount": 1,
                                "repostCount": 1,
                                "likeCount": 12,
                                "embeds": [],
                                "indexedAt": "2025-06-06T03:04:06.045Z",
                                "$type": "app.bsky.embed.record#viewRecord"
                            },
                            "$type": "app.bsky.embed.record#view"
                        },
                        "reactions": [],
                        "sentAt": "2025-06-06T10:16:41.022Z",
                        "$type": "chat.bsky.convo.defs#messageView"
                    },
                    "$type": "chat.bsky.convo.defs#logReadMessage"
                },
                {
                    "convoId": "3kt4jgm5nc32q",
                    "rev": "2222223gc4cxd",
                    "message": {
                        "id": "3lqwm5fnemw2i",
                        "rev": "2222223gc2xcu",
                        "sender": {
                            "did": "did:plc:ec72yg6n2sydzjvtovvdlxrk"
                        },
                        "text": "DM with post attached",
                        "embed": {
                            "record": {
                                "uri": "at://did:plc:7xkc5gsqnj33qs3fsa2mewzj/app.bsky.feed.post/3lqvtxtvtfc2q",
                                "cid": "bafyreihdfqqjhjwctdcf6kxnpk47kr6cc7u2jtxntnb54ibhb43bbbfw54",
                                "author": {
                                    "did": "did:plc:7xkc5gsqnj33qs3fsa2mewzj",
                                    "handle": "anniesexton.com",
                                    "displayName": "Annie Sexton",
                                    "avatar": "https://cdn.bsky.app/img/avatar/plain/did:plc:7xkc5gsqnj33qs3fsa2mewzj/bafkreidxkpgcgmv4ommtzupmc2k2wh6jsc266ram4am6ax7wk2w4m4xtvy",
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
                                        "blockedBy": false
                                    },
                                    "labels": [],
                                    "createdAt": "2024-08-17T14:08:21.885Z"
                                },
                                "value": {
                                    "$type": "app.bsky.feed.post",
                                    "createdAt": "2025-06-06T03:04:04.849Z",
                                    "langs": [
                                        "en"
                                    ],
                                    "reply": {
                                        "parent": {
                                            "cid": "bafyreigk44bcyytcprdcz4iz2n6xmwdizqvj5aqnz4wqxqgse7rcnu7g4e",
                                            "uri": "at://did:plc:vc7f4oafdgxsihk4cry2xpze/app.bsky.feed.post/3lqux7ccnec2p"
                                        },
                                        "root": {
                                            "cid": "bafyreigk44bcyytcprdcz4iz2n6xmwdizqvj5aqnz4wqxqgse7rcnu7g4e",
                                            "uri": "at://did:plc:vc7f4oafdgxsihk4cry2xpze/app.bsky.feed.post/3lqux7ccnec2p"
                                        }
                                    },
                                    "text": "Jerry why you gotta ruin everything I love"
                                },
                                "labels": [],
                                "replyCount": 1,
                                "repostCount": 1,
                                "likeCount": 12,
                                "embeds": [],
                                "indexedAt": "2025-06-06T03:04:06.045Z",
                                "$type": "app.bsky.embed.record#viewRecord"
                            },
                            "$type": "app.bsky.embed.record#view"
                        },
                        "reactions": [],
                        "sentAt": "2025-06-06T10:16:41.022Z",
                        "$type": "chat.bsky.convo.defs#messageView"
                    },
                    "$type": "chat.bsky.convo.defs#logReadMessage"
                },
                {
                    "convoId": "3kt4jgm5nc32q",
                    "rev": "2222223gc5gim",
                    "message": {
                        "id": "3lqwm5fnemw2i",
                        "rev": "2222223gc2xcu",
                        "sender": {
                            "did": "did:plc:ec72yg6n2sydzjvtovvdlxrk"
                        },
                        "text": "DM with post attached",
                        "embed": {
                            "record": {
                                "uri": "at://did:plc:7xkc5gsqnj33qs3fsa2mewzj/app.bsky.feed.post/3lqvtxtvtfc2q",
                                "cid": "bafyreihdfqqjhjwctdcf6kxnpk47kr6cc7u2jtxntnb54ibhb43bbbfw54",
                                "author": {
                                    "did": "did:plc:7xkc5gsqnj33qs3fsa2mewzj",
                                    "handle": "anniesexton.com",
                                    "displayName": "Annie Sexton",
                                    "avatar": "https://cdn.bsky.app/img/avatar/plain/did:plc:7xkc5gsqnj33qs3fsa2mewzj/bafkreidxkpgcgmv4ommtzupmc2k2wh6jsc266ram4am6ax7wk2w4m4xtvy",
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
                                        "blockedBy": false
                                    },
                                    "labels": [],
                                    "createdAt": "2024-08-17T14:08:21.885Z"
                                },
                                "value": {
                                    "$type": "app.bsky.feed.post",
                                    "createdAt": "2025-06-06T03:04:04.849Z",
                                    "langs": [
                                        "en"
                                    ],
                                    "reply": {
                                        "parent": {
                                            "cid": "bafyreigk44bcyytcprdcz4iz2n6xmwdizqvj5aqnz4wqxqgse7rcnu7g4e",
                                            "uri": "at://did:plc:vc7f4oafdgxsihk4cry2xpze/app.bsky.feed.post/3lqux7ccnec2p"
                                        },
                                        "root": {
                                            "cid": "bafyreigk44bcyytcprdcz4iz2n6xmwdizqvj5aqnz4wqxqgse7rcnu7g4e",
                                            "uri": "at://did:plc:vc7f4oafdgxsihk4cry2xpze/app.bsky.feed.post/3lqux7ccnec2p"
                                        }
                                    },
                                    "text": "Jerry why you gotta ruin everything I love"
                                },
                                "labels": [],
                                "replyCount": 1,
                                "repostCount": 1,
                                "likeCount": 12,
                                "embeds": [],
                                "indexedAt": "2025-06-06T03:04:06.045Z",
                                "$type": "app.bsky.embed.record#viewRecord"
                            },
                            "$type": "app.bsky.embed.record#view"
                        },
                        "reactions": [],
                        "sentAt": "2025-06-06T10:16:41.022Z",
                        "$type": "chat.bsky.convo.defs#messageView"
                    },
                    "$type": "chat.bsky.convo.defs#logReadMessage"
                },
                {
                    "convoId": "3kt4jgm5nc32q",
                    "rev": "2222223gc66ss",
                    "message": {
                        "id": "3lqwm5fnemw2i",
                        "rev": "2222223gc2xcu",
                        "sender": {
                            "did": "did:plc:ec72yg6n2sydzjvtovvdlxrk"
                        },
                        "text": "DM with post attached",
                        "embed": {
                            "record": {
                                "uri": "at://did:plc:7xkc5gsqnj33qs3fsa2mewzj/app.bsky.feed.post/3lqvtxtvtfc2q",
                                "cid": "bafyreihdfqqjhjwctdcf6kxnpk47kr6cc7u2jtxntnb54ibhb43bbbfw54",
                                "author": {
                                    "did": "did:plc:7xkc5gsqnj33qs3fsa2mewzj",
                                    "handle": "anniesexton.com",
                                    "displayName": "Annie Sexton",
                                    "avatar": "https://cdn.bsky.app/img/avatar/plain/did:plc:7xkc5gsqnj33qs3fsa2mewzj/bafkreidxkpgcgmv4ommtzupmc2k2wh6jsc266ram4am6ax7wk2w4m4xtvy",
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
                                        "blockedBy": false
                                    },
                                    "labels": [],
                                    "createdAt": "2024-08-17T14:08:21.885Z"
                                },
                                "value": {
                                    "$type": "app.bsky.feed.post",
                                    "createdAt": "2025-06-06T03:04:04.849Z",
                                    "langs": [
                                        "en"
                                    ],
                                    "reply": {
                                        "parent": {
                                            "cid": "bafyreigk44bcyytcprdcz4iz2n6xmwdizqvj5aqnz4wqxqgse7rcnu7g4e",
                                            "uri": "at://did:plc:vc7f4oafdgxsihk4cry2xpze/app.bsky.feed.post/3lqux7ccnec2p"
                                        },
                                        "root": {
                                            "cid": "bafyreigk44bcyytcprdcz4iz2n6xmwdizqvj5aqnz4wqxqgse7rcnu7g4e",
                                            "uri": "at://did:plc:vc7f4oafdgxsihk4cry2xpze/app.bsky.feed.post/3lqux7ccnec2p"
                                        }
                                    },
                                    "text": "Jerry why you gotta ruin everything I love"
                                },
                                "labels": [],
                                "replyCount": 1,
                                "repostCount": 1,
                                "likeCount": 12,
                                "embeds": [],
                                "indexedAt": "2025-06-06T03:04:06.045Z",
                                "$type": "app.bsky.embed.record#viewRecord"
                            },
                            "$type": "app.bsky.embed.record#view"
                        },
                        "reactions": [],
                        "sentAt": "2025-06-06T10:16:41.022Z",
                        "$type": "chat.bsky.convo.defs#messageView"
                    },
                    "$type": "chat.bsky.convo.defs#logReadMessage"
                },
                {
                    "convoId": "3kt4jgm5nc32q",
                    "rev": "2222223gcafcx",
                    "message": {
                        "id": "3lqwm5fnemw2i",
                        "rev": "2222223gc2xcu",
                        "sender": {
                            "did": "did:plc:ec72yg6n2sydzjvtovvdlxrk"
                        },
                        "text": "DM with post attached",
                        "embed": {
                            "record": {
                                "uri": "at://did:plc:7xkc5gsqnj33qs3fsa2mewzj/app.bsky.feed.post/3lqvtxtvtfc2q",
                                "cid": "bafyreihdfqqjhjwctdcf6kxnpk47kr6cc7u2jtxntnb54ibhb43bbbfw54",
                                "author": {
                                    "did": "did:plc:7xkc5gsqnj33qs3fsa2mewzj",
                                    "handle": "anniesexton.com",
                                    "displayName": "Annie Sexton",
                                    "avatar": "https://cdn.bsky.app/img/avatar/plain/did:plc:7xkc5gsqnj33qs3fsa2mewzj/bafkreidxkpgcgmv4ommtzupmc2k2wh6jsc266ram4am6ax7wk2w4m4xtvy",
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
                                        "blockedBy": false
                                    },
                                    "labels": [],
                                    "createdAt": "2024-08-17T14:08:21.885Z"
                                },
                                "value": {
                                    "$type": "app.bsky.feed.post",
                                    "createdAt": "2025-06-06T03:04:04.849Z",
                                    "langs": [
                                        "en"
                                    ],
                                    "reply": {
                                        "parent": {
                                            "cid": "bafyreigk44bcyytcprdcz4iz2n6xmwdizqvj5aqnz4wqxqgse7rcnu7g4e",
                                            "uri": "at://did:plc:vc7f4oafdgxsihk4cry2xpze/app.bsky.feed.post/3lqux7ccnec2p"
                                        },
                                        "root": {
                                            "cid": "bafyreigk44bcyytcprdcz4iz2n6xmwdizqvj5aqnz4wqxqgse7rcnu7g4e",
                                            "uri": "at://did:plc:vc7f4oafdgxsihk4cry2xpze/app.bsky.feed.post/3lqux7ccnec2p"
                                        }
                                    },
                                    "text": "Jerry why you gotta ruin everything I love"
                                },
                                "labels": [],
                                "replyCount": 1,
                                "repostCount": 1,
                                "likeCount": 12,
                                "embeds": [],
                                "indexedAt": "2025-06-06T03:04:06.045Z",
                                "$type": "app.bsky.embed.record#viewRecord"
                            },
                            "$type": "app.bsky.embed.record#view"
                        },
                        "reactions": [],
                        "sentAt": "2025-06-06T10:16:41.022Z",
                        "$type": "chat.bsky.convo.defs#messageView"
                    },
                    "$type": "chat.bsky.convo.defs#logReadMessage"
                },
                {
                    "convoId": "3kt4jgm5nc32q",
                    "rev": "2222223gciznf",
                    "message": {
                        "id": "3lqwm5fnemw2i",
                        "rev": "2222223gc2xcu",
                        "sender": {
                            "did": "did:plc:ec72yg6n2sydzjvtovvdlxrk"
                        },
                        "text": "DM with post attached",
                        "embed": {
                            "record": {
                                "uri": "at://did:plc:7xkc5gsqnj33qs3fsa2mewzj/app.bsky.feed.post/3lqvtxtvtfc2q",
                                "cid": "bafyreihdfqqjhjwctdcf6kxnpk47kr6cc7u2jtxntnb54ibhb43bbbfw54",
                                "author": {
                                    "did": "did:plc:7xkc5gsqnj33qs3fsa2mewzj",
                                    "handle": "anniesexton.com",
                                    "displayName": "Annie Sexton",
                                    "avatar": "https://cdn.bsky.app/img/avatar/plain/did:plc:7xkc5gsqnj33qs3fsa2mewzj/bafkreidxkpgcgmv4ommtzupmc2k2wh6jsc266ram4am6ax7wk2w4m4xtvy",
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
                                        "blockedBy": false
                                    },
                                    "labels": [],
                                    "createdAt": "2024-08-17T14:08:21.885Z"
                                },
                                "value": {
                                    "$type": "app.bsky.feed.post",
                                    "createdAt": "2025-06-06T03:04:04.849Z",
                                    "langs": [
                                        "en"
                                    ],
                                    "reply": {
                                        "parent": {
                                            "cid": "bafyreigk44bcyytcprdcz4iz2n6xmwdizqvj5aqnz4wqxqgse7rcnu7g4e",
                                            "uri": "at://did:plc:vc7f4oafdgxsihk4cry2xpze/app.bsky.feed.post/3lqux7ccnec2p"
                                        },
                                        "root": {
                                            "cid": "bafyreigk44bcyytcprdcz4iz2n6xmwdizqvj5aqnz4wqxqgse7rcnu7g4e",
                                            "uri": "at://did:plc:vc7f4oafdgxsihk4cry2xpze/app.bsky.feed.post/3lqux7ccnec2p"
                                        }
                                    },
                                    "text": "Jerry why you gotta ruin everything I love"
                                },
                                "labels": [],
                                "replyCount": 1,
                                "repostCount": 1,
                                "likeCount": 12,
                                "embeds": [],
                                "indexedAt": "2025-06-06T03:04:06.045Z",
                                "$type": "app.bsky.embed.record#viewRecord"
                            },
                            "$type": "app.bsky.embed.record#view"
                        },
                        "reactions": [],
                        "sentAt": "2025-06-06T10:16:41.022Z",
                        "$type": "chat.bsky.convo.defs#messageView"
                    },
                    "$type": "chat.bsky.convo.defs#logReadMessage"
                },
                {
                    "convoId": "3kt4jgm5nc32q",
                    "rev": "2222223gckwpk",
                    "message": {
                        "id": "3lqwm5fnemw2i",
                        "rev": "2222223gc2xcu",
                        "sender": {
                            "did": "did:plc:ec72yg6n2sydzjvtovvdlxrk"
                        },
                        "text": "DM with post attached",
                        "embed": {
                            "record": {
                                "uri": "at://did:plc:7xkc5gsqnj33qs3fsa2mewzj/app.bsky.feed.post/3lqvtxtvtfc2q",
                                "cid": "bafyreihdfqqjhjwctdcf6kxnpk47kr6cc7u2jtxntnb54ibhb43bbbfw54",
                                "author": {
                                    "did": "did:plc:7xkc5gsqnj33qs3fsa2mewzj",
                                    "handle": "anniesexton.com",
                                    "displayName": "Annie Sexton",
                                    "avatar": "https://cdn.bsky.app/img/avatar/plain/did:plc:7xkc5gsqnj33qs3fsa2mewzj/bafkreidxkpgcgmv4ommtzupmc2k2wh6jsc266ram4am6ax7wk2w4m4xtvy",
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
                                        "blockedBy": false
                                    },
                                    "labels": [],
                                    "createdAt": "2024-08-17T14:08:21.885Z"
                                },
                                "value": {
                                    "$type": "app.bsky.feed.post",
                                    "createdAt": "2025-06-06T03:04:04.849Z",
                                    "langs": [
                                        "en"
                                    ],
                                    "reply": {
                                        "parent": {
                                            "cid": "bafyreigk44bcyytcprdcz4iz2n6xmwdizqvj5aqnz4wqxqgse7rcnu7g4e",
                                            "uri": "at://did:plc:vc7f4oafdgxsihk4cry2xpze/app.bsky.feed.post/3lqux7ccnec2p"
                                        },
                                        "root": {
                                            "cid": "bafyreigk44bcyytcprdcz4iz2n6xmwdizqvj5aqnz4wqxqgse7rcnu7g4e",
                                            "uri": "at://did:plc:vc7f4oafdgxsihk4cry2xpze/app.bsky.feed.post/3lqux7ccnec2p"
                                        }
                                    },
                                    "text": "Jerry why you gotta ruin everything I love"
                                },
                                "labels": [],
                                "replyCount": 1,
                                "repostCount": 1,
                                "likeCount": 12,
                                "embeds": [],
                                "indexedAt": "2025-06-06T03:04:06.045Z",
                                "$type": "app.bsky.embed.record#viewRecord"
                            },
                            "$type": "app.bsky.embed.record#view"
                        },
                        "reactions": [],
                        "sentAt": "2025-06-06T10:16:41.022Z",
                        "$type": "chat.bsky.convo.defs#messageView"
                    },
                    "$type": "chat.bsky.convo.defs#logReadMessage"
                },
                {
                    "convoId": "3kt4jgm5nc32q",
                    "rev": "2222223gcvp4s",
                    "message": {
                        "id": "3lqwm5fnemw2i",
                        "rev": "2222223gc2xcu",
                        "sender": {
                            "did": "did:plc:ec72yg6n2sydzjvtovvdlxrk"
                        },
                        "text": "DM with post attached",
                        "embed": {
                            "record": {
                                "uri": "at://did:plc:7xkc5gsqnj33qs3fsa2mewzj/app.bsky.feed.post/3lqvtxtvtfc2q",
                                "cid": "bafyreihdfqqjhjwctdcf6kxnpk47kr6cc7u2jtxntnb54ibhb43bbbfw54",
                                "author": {
                                    "did": "did:plc:7xkc5gsqnj33qs3fsa2mewzj",
                                    "handle": "anniesexton.com",
                                    "displayName": "Annie Sexton",
                                    "avatar": "https://cdn.bsky.app/img/avatar/plain/did:plc:7xkc5gsqnj33qs3fsa2mewzj/bafkreidxkpgcgmv4ommtzupmc2k2wh6jsc266ram4am6ax7wk2w4m4xtvy",
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
                                        "blockedBy": false
                                    },
                                    "labels": [],
                                    "createdAt": "2024-08-17T14:08:21.885Z"
                                },
                                "value": {
                                    "$type": "app.bsky.feed.post",
                                    "createdAt": "2025-06-06T03:04:04.849Z",
                                    "langs": [
                                        "en"
                                    ],
                                    "reply": {
                                        "parent": {
                                            "cid": "bafyreigk44bcyytcprdcz4iz2n6xmwdizqvj5aqnz4wqxqgse7rcnu7g4e",
                                            "uri": "at://did:plc:vc7f4oafdgxsihk4cry2xpze/app.bsky.feed.post/3lqux7ccnec2p"
                                        },
                                        "root": {
                                            "cid": "bafyreigk44bcyytcprdcz4iz2n6xmwdizqvj5aqnz4wqxqgse7rcnu7g4e",
                                            "uri": "at://did:plc:vc7f4oafdgxsihk4cry2xpze/app.bsky.feed.post/3lqux7ccnec2p"
                                        }
                                    },
                                    "text": "Jerry why you gotta ruin everything I love"
                                },
                                "labels": [],
                                "replyCount": 1,
                                "repostCount": 1,
                                "likeCount": 12,
                                "embeds": [],
                                "indexedAt": "2025-06-06T03:04:06.045Z",
                                "$type": "app.bsky.embed.record#viewRecord"
                            },
                            "$type": "app.bsky.embed.record#view"
                        },
                        "reactions": [],
                        "sentAt": "2025-06-06T10:16:41.022Z",
                        "$type": "chat.bsky.convo.defs#messageView"
                    },
                    "$type": "chat.bsky.convo.defs#logReadMessage"
                },
                {
                    "convoId": "3kt4jgm5nc32q",
                    "rev": "2222223gg456t",
                    "message": {
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
                                            "allowIncoming": "all",
                                            "allowGroupInvites": "all"
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
                    "relatedProfiles": [],
                    "$type": "chat.bsky.convo.defs#logCreateMessage"
                },
                {
                    "convoId": "3kt4jgm5nc32q",
                    "rev": "2222224rbh72c",
                    "message": {
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
                                            "allowIncoming": "all",
                                            "allowGroupInvites": "all"
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
                    "$type": "chat.bsky.convo.defs#logReadMessage"
                },
                {
                    "convoId": "3kt4jgm5nc32q",
                    "rev": "2222224rbh72d",
                    "message": {
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
                                            "allowIncoming": "all",
                                            "allowGroupInvites": "all"
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
                    "$type": "chat.bsky.convo.defs#logReadConvo"
                },
                {
                    "convoId": "3kt4jgm5nc32q",
                    "rev": "2222224rbh7a4",
                    "message": {
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
                                            "allowIncoming": "all",
                                            "allowGroupInvites": "all"
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
                    "$type": "chat.bsky.convo.defs#logReadMessage"
                },
                {
                    "convoId": "3kt4jgm5nc32q",
                    "rev": "2222224rbh7a5",
                    "message": {
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
                                            "allowIncoming": "all",
                                            "allowGroupInvites": "all"
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
                    "$type": "chat.bsky.convo.defs#logReadConvo"
                },
                {
                    "convoId": "3moh3ak5nq32s",
                    "rev": "2222224rdcz4r",
                    "$type": "chat.bsky.convo.defs#logBeginConvo"
                },
                {
                    "convoId": "3moh3ak5nq32s",
                    "rev": "2222224rdcznm",
                    "message": {
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
                    "relatedProfiles": [
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
                                                    "allowIncoming": "none",
                                                    "allowGroupInvites": "none"
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
                        }
                    ],
                    "$type": "chat.bsky.convo.defs#logCreateMessage"
                },
                {
                    "convoId": "3moh3ak5nq32s",
                    "rev": "2222224rfi6dg",
                    "message": {
                        "id": "3moidw56g6g2g",
                        "rev": "2222224rfi6dg",
                        "sentAt": "2026-06-17T12:47:59.824Z",
                        "data": {
                            "$type": "chat.bsky.convo.defs#systemMessageDataCreateJoinLink"
                        },
                        "$type": "chat.bsky.convo.defs#systemMessageView"
                    },
                    "$type": "chat.bsky.convo.defs#logCreateJoinLink"
                },
                {
                    "convoId": "3moh3ak5nq32s",
                    "rev": "2222224ssqayw",
                    "message": {
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
                    "$type": "chat.bsky.convo.defs#logReadMessage"
                },
                {
                    "convoId": "3moh3ak5nq32s",
                    "rev": "2222224ssqayx",
                    "message": {
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
                    "$type": "chat.bsky.convo.defs#logReadConvo"
                },
                {
                    "convoId": "3kt4jgm5nc32q",
                    "rev": "2222224sssy25",
                    "message": {
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
                                            "allowIncoming": "all",
                                            "allowGroupInvites": "all"
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
                    "$type": "chat.bsky.convo.defs#logReadMessage"
                },
                {
                    "convoId": "3kt4jgm5nc32q",
                    "rev": "2222224sssy26",
                    "message": {
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
                                            "allowIncoming": "all",
                                            "allowGroupInvites": "all"
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
                    "$type": "chat.bsky.convo.defs#logReadConvo"
                },
                {
                    "convoId": "3kt4jgm5nc32q",
                    "rev": "2222224sssy5e",
                    "message": {
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
                                            "allowIncoming": "all",
                                            "allowGroupInvites": "all"
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
                    "$type": "chat.bsky.convo.defs#logReadMessage"
                },
                {
                    "convoId": "3kt4jgm5nc32q",
                    "rev": "2222224sssy5f",
                    "message": {
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
                                            "allowIncoming": "all",
                                            "allowGroupInvites": "all"
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
                    "$type": "chat.bsky.convo.defs#logReadConvo"
                },
                {
                    "convoId": "3kt4jgm5nc32q",
                    "rev": "2222224sssynv",
                    "message": {
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
                                            "allowIncoming": "all",
                                            "allowGroupInvites": "all"
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
                    "$type": "chat.bsky.convo.defs#logReadMessage"
                },
                {
                    "convoId": "3kt4jgm5nc32q",
                    "rev": "2222224sssynw",
                    "message": {
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
                                            "allowIncoming": "all",
                                            "allowGroupInvites": "all"
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
                    "$type": "chat.bsky.convo.defs#logReadConvo"
                },
                {
                    "convoId": "3kt4jgm5nc32q",
                    "rev": "2222224sssyuu",
                    "message": {
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
                                            "allowIncoming": "all",
                                            "allowGroupInvites": "all"
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
                    "$type": "chat.bsky.convo.defs#logReadMessage"
                },
                {
                    "convoId": "3kt4jgm5nc32q",
                    "rev": "2222224sssyuv",
                    "message": {
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
                                            "allowIncoming": "all",
                                            "allowGroupInvites": "all"
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
                    "$type": "chat.bsky.convo.defs#logReadConvo"
                },
                {
                    "convoId": "3mp4urwifv22y",
                    "rev": "2222224sst2zw",
                    "$type": "chat.bsky.convo.defs#logBeginConvo"
                },
                {
                    "convoId": "3mp4urwifv22y",
                    "rev": "2222224sst5ru",
                    "message": {
                        "id": "3mp4usrcmrk2g",
                        "rev": "2222224sst5rs",
                        "sender": {
                            "did": "did:plc:ec72yg6n2sydzjvtovvdlxrk"
                        },
                        "text": "This is a test group conversation, started by Test Bot, with only blowdart",
                        "reactions": [],
                        "sentAt": "2026-06-25T16:43:35.094Z",
                        "$type": "chat.bsky.convo.defs#messageView"
                    },
                    "relatedProfiles": [
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
                                "chat": {
                                    "allowIncoming": "following",
                                    "allowGroupInvites": "following"
                                },
                                "activitySubscription": {
                                    "allowSubscriptions": "followers"
                                }
                            },
                            "viewer": {
                                "muted": false,
                                "blockedBy": false,
                                "knownFollowers": {
                                    "count": 4,
                                    "followers": [
                                        {
                                            "did": "did:plc:hfgp6pj3akhqxntgqwramlbg",
                                            "handle": "blowdart.me",
                                            "displayName": "Barry Dorrans",
                                            "pronouns": "He/Him",
                                            "avatar": "https://cdn.bsky.app/img/avatar/plain/did:plc:hfgp6pj3akhqxntgqwramlbg/bafkreicwjaromkjs4jrd5uqznacfgzvhnob2il5fwywxqopbnhfb74n27m",
                                            "associated": {
                                                "chat": {
                                                    "allowIncoming": "all",
                                                    "allowGroupInvites": "all"
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
                                        {
                                            "did": "did:plc:wtdzzfgzjpirnk5wvpjutqoy",
                                            "handle": "oracularhades.com",
                                            "displayName": "Josh",
                                            "avatar": "https://cdn.bsky.app/img/avatar/plain/did:plc:wtdzzfgzjpirnk5wvpjutqoy/bafkreiahoixmvuo7blc5dhmul5w64llaeofxfu6qmsu7zewe5jvum2ntdy",
                                            "associated": {
                                                "chat": {
                                                    "allowIncoming": "none",
                                                    "allowGroupInvites": "none"
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
                                            "did": "did:plc:mvgsfujvam5iekxlk3howidu",
                                            "handle": "anotherbot.idunno.blue",
                                            "displayName": "Test Bot #2",
                                            "avatar": "https://cdn.bsky.app/img/avatar/plain/did:plc:mvgsfujvam5iekxlk3howidu/bafkreigom4rh7v3ruxzhjbivwtrwzs5gr54lc2at2wueemji6d4aljo7ua",
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
                                            "createdAt": "2024-11-26T15:39:59.145Z"
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
                                "role": "owner",
                                "$type": "chat.bsky.actor.defs#groupConvoMember"
                            }
                        }
                    ],
                    "$type": "chat.bsky.convo.defs#logCreateMessage"
                },
                {
                    "convoId": "3mp4urwifv22y",
                    "rev": "2222224ssu7zt",
                    "message": {
                        "id": "3mp4v66f3nc2w",
                        "rev": "2222224ssu7zs",
                        "sender": {
                            "did": "did:plc:hfgp6pj3akhqxntgqwramlbg"
                        },
                        "text": "This is a reply in the ground conversation",
                        "reactions": [],
                        "sentAt": "2026-06-25T16:49:57.905Z",
                        "$type": "chat.bsky.convo.defs#messageView"
                    },
                    "relatedProfiles": [
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
                                    "allowIncoming": "all",
                                    "allowGroupInvites": "all"
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
                                                    "allowIncoming": "none",
                                                    "allowGroupInvites": "none"
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
                                "role": "standard",
                                "addedBy": {
                                    "did": "did:plc:ec72yg6n2sydzjvtovvdlxrk",
                                    "handle": "bot.idunno.blue",
                                    "displayName": "Test Bot",
                                    "avatar": "https://cdn.bsky.app/img/avatar/plain/did:plc:ec72yg6n2sydzjvtovvdlxrk/bafkreig5ujmxrechgakn4ukf37oj6mlpivukqfbgpuhb3pqjmdkxxtjpnq",
                                    "associated": {
                                        "lists": 2,
                                        "feedgens": 0,
                                        "starterPacks": 1,
                                        "labeler": false,
                                        "chat": {
                                            "allowIncoming": "following",
                                            "allowGroupInvites": "following"
                                        },
                                        "activitySubscription": {
                                            "allowSubscriptions": "followers"
                                        }
                                    },
                                    "viewer": {
                                        "muted": false,
                                        "blockedBy": false,
                                        "knownFollowers": {
                                            "count": 4,
                                            "followers": [
                                                {
                                                    "did": "did:plc:hfgp6pj3akhqxntgqwramlbg",
                                                    "handle": "blowdart.me",
                                                    "displayName": "Barry Dorrans",
                                                    "pronouns": "He/Him",
                                                    "avatar": "https://cdn.bsky.app/img/avatar/plain/did:plc:hfgp6pj3akhqxntgqwramlbg/bafkreicwjaromkjs4jrd5uqznacfgzvhnob2il5fwywxqopbnhfb74n27m",
                                                    "associated": {
                                                        "chat": {
                                                            "allowIncoming": "all",
                                                            "allowGroupInvites": "all"
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
                                                {
                                                    "did": "did:plc:wtdzzfgzjpirnk5wvpjutqoy",
                                                    "handle": "oracularhades.com",
                                                    "displayName": "Josh",
                                                    "avatar": "https://cdn.bsky.app/img/avatar/plain/did:plc:wtdzzfgzjpirnk5wvpjutqoy/bafkreiahoixmvuo7blc5dhmul5w64llaeofxfu6qmsu7zewe5jvum2ntdy",
                                                    "associated": {
                                                        "chat": {
                                                            "allowIncoming": "none",
                                                            "allowGroupInvites": "none"
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
                                                    "did": "did:plc:mvgsfujvam5iekxlk3howidu",
                                                    "handle": "anotherbot.idunno.blue",
                                                    "displayName": "Test Bot #2",
                                                    "avatar": "https://cdn.bsky.app/img/avatar/plain/did:plc:mvgsfujvam5iekxlk3howidu/bafkreigom4rh7v3ruxzhjbivwtrwzs5gr54lc2at2wueemji6d4aljo7ua",
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
                                                    "createdAt": "2024-11-26T15:39:59.145Z"
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
                                    "chatDisabled": false
                                },
                                "$type": "chat.bsky.actor.defs#groupConvoMember"
                            }
                        }
                    ],
                    "$type": "chat.bsky.convo.defs#logCreateMessage"
                },
                {
                    "convoId": "3mp4urwifv22y",
                    "rev": "2222224ssudq4",
                    "message": {
                        "id": "3mp4v66f3nc2w",
                        "rev": "2222224ssu7zs",
                        "sender": {
                            "did": "did:plc:hfgp6pj3akhqxntgqwramlbg"
                        },
                        "text": "This is a reply in the ground conversation",
                        "reactions": [],
                        "sentAt": "2026-06-25T16:49:57.905Z",
                        "$type": "chat.bsky.convo.defs#messageView"
                    },
                    "$type": "chat.bsky.convo.defs#logReadMessage"
                },
                {
                    "convoId": "3mp4urwifv22y",
                    "rev": "2222224ssudq5",
                    "message": {
                        "id": "3mp4v66f3nc2w",
                        "rev": "2222224ssu7zs",
                        "sender": {
                            "did": "did:plc:hfgp6pj3akhqxntgqwramlbg"
                        },
                        "text": "This is a reply in the ground conversation",
                        "reactions": [],
                        "sentAt": "2026-06-25T16:49:57.905Z",
                        "$type": "chat.bsky.convo.defs#messageView"
                    },
                    "$type": "chat.bsky.convo.defs#logReadConvo"
                },
                {
                    "convoId": "3mp4urwifv22y",
                    "rev": "2222224ssuedx",
                    "message": {
                        "id": "3mp4v7izeul2p",
                        "rev": "2222224ssuedx",
                        "sentAt": "2026-06-25T16:50:42.610Z",
                        "data": {
                            "$type": "chat.bsky.convo.defs#systemMessageDataCreateJoinLink"
                        },
                        "$type": "chat.bsky.convo.defs#systemMessageView"
                    },
                    "$type": "chat.bsky.convo.defs#logCreateJoinLink"
                },
                {
                    "convoId": "3mp4urwifv22y",
                    "rev": "2222224ssug5l",
                    "message": {
                        "id": "3mp4va2uuxc23",
                        "rev": "2222224ssug5j",
                        "sentAt": "2026-06-25T16:51:01.337Z",
                        "data": {
                            "member": {
                                "did": "did:plc:mvgsfujvam5iekxlk3howidu"
                            },
                            "role": "standard",
                            "$type": "chat.bsky.convo.defs#systemMessageDataMemberJoin"
                        },
                        "$type": "chat.bsky.convo.defs#systemMessageView"
                    },
                    "relatedProfiles": [
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
                                                    "allowIncoming": "none",
                                                    "allowGroupInvites": "none"
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
                                "role": "standard",
                                "$type": "chat.bsky.actor.defs#groupConvoMember"
                            }
                        }
                    ],
                    "$type": "chat.bsky.convo.defs#logMemberJoin"
                },
                {
                    "convoId": "3mp4urwifv22y",
                    "rev": "2222224ssuhx3",
                    "message": {
                        "id": "3mp4valh3qs2t",
                        "rev": "2222224ssuhx2",
                        "sender": {
                            "did": "did:plc:mvgsfujvam5iekxlk3howidu"
                        },
                        "text": "Oh look, another bot joined",
                        "reactions": [],
                        "sentAt": "2026-06-25T16:51:18.711Z",
                        "$type": "chat.bsky.convo.defs#messageView"
                    },
                    "relatedProfiles": [
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
                                                    "allowIncoming": "none",
                                                    "allowGroupInvites": "none"
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
                                "role": "standard",
                                "$type": "chat.bsky.actor.defs#groupConvoMember"
                            }
                        }
                    ],
                    "$type": "chat.bsky.convo.defs#logCreateMessage"
                }
            ]
        }
        """;

    [Fact]
    public void ConversationLogsDeserializeCorrectly()
    {
        GetLogResponse? logs = JsonSerializer.Deserialize<GetLogResponse>(_json, BlueskyJsonSerializerOptions.Options);
        Assert.NotNull(logs);
    }
}