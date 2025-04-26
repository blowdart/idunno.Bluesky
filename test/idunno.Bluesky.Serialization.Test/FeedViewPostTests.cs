// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;

using idunno.Bluesky.Feed;

namespace idunno.Bluesky.Serialization.Test
{
    [ExcludeFromCodeCoverage]
    public class FeedViewPostTests
    {
        [Fact]
        public void FeedViewPostCorrectlyWithSourceGeneratedJsonContext()
        {
            string json = """
            {
                "post": {
                    "uri": "at://did:plc:hfgp6pj3akhqxntgqwramlbg/app.bsky.feed.post/3lnoba24kuk2f",
                    "cid": "bafyreififokk7ii62nxnsoldrnm33bro62a2k36xp5fhfhgptuanobuwuq",
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
                        "createdAt": "2023-04-22T22:44:04.316Z"
                    },
                    "record": {
                        "$type": "app.bsky.feed.post",
                        "createdAt": "2025-04-25T22:24:25.648Z",
                        "langs": [
                            "en"
                        ],
                        "reply": {
                            "parent": {
                                "cid": "bafyreidejqrrcsfmjo4klt7axtdglp5e5cnrterovqxzd6bmbqz3tvlvua",
                                "uri": "at://did:plc:46xpsjkrbhv4tphe36nszjna/app.bsky.feed.post/3lnoarqrud22j"
                            },
                            "root": {
                                "cid": "bafyreigrph3jf4zs3wnntm6ubbdknvv36evndg7odf56xubrnu57cgoiqa",
                                "uri": "at://did:plc:hfgp6pj3akhqxntgqwramlbg/app.bsky.feed.post/3lnmbltm4j223"
                            }
                        },
                        "text": "He still noms on me so I have to pretend it hurts 😂"
                    },
                    "replyCount": 0,
                    "repostCount": 0,
                    "likeCount": 0,
                    "quoteCount": 0,
                    "indexedAt": "2025-04-25T22:24:26.383Z",
                    "viewer": {
                        "threadMuted": false,
                        "embeddingDisabled": false
                    },
                    "labels": []
                },
                "reply": {
                    "root": {
                        "uri": "at://did:plc:hfgp6pj3akhqxntgqwramlbg/app.bsky.feed.post/3lnmbltm4j223",
                        "cid": "bafyreigrph3jf4zs3wnntm6ubbdknvv36evndg7odf56xubrnu57cgoiqa",
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
                            "createdAt": "2023-04-22T22:44:04.316Z"
                        },
                        "record": {
                            "$type": "app.bsky.feed.post",
                            "createdAt": "2025-04-25T03:25:41.979Z",
                            "embed": {
                                "$type": "app.bsky.embed.images",
                                "images": [
                                    {
                                        "alt": "A cat loafing on the back for a sofa sticking his tongue out",
                                        "aspectRatio": {
                                            "height": 2000,
                                            "width": 1500
                                        },
                                        "image": {
                                            "$type": "blob",
                                            "ref": {
                                                "$link": "bafkreifulgueyb5ndxgj5zgqeme2tmqttgkooceev74el6g5am5tsz6dqu"
                                            },
                                            "mimeType": "image/jpeg",
                                            "size": 550217
                                        }
                                    }
                                ]
                            },
                            "langs": [
                                "en"
                            ],
                            "text": "After his canines got removed he has resting melm face"
                        },
                        "embed": {
                            "$type": "app.bsky.embed.images#view",
                            "images": [
                                {
                                    "thumb": "https://cdn.bsky.app/img/feed_thumbnail/plain/did:plc:hfgp6pj3akhqxntgqwramlbg/bafkreifulgueyb5ndxgj5zgqeme2tmqttgkooceev74el6g5am5tsz6dqu@jpeg",
                                    "fullsize": "https://cdn.bsky.app/img/feed_fullsize/plain/did:plc:hfgp6pj3akhqxntgqwramlbg/bafkreifulgueyb5ndxgj5zgqeme2tmqttgkooceev74el6g5am5tsz6dqu@jpeg",
                                    "alt": "A cat loafing on the back for a sofa sticking his tongue out",
                                    "aspectRatio": {
                                        "height": 2000,
                                        "width": 1500
                                    }
                                }
                            ]
                        },
                        "replyCount": 14,
                        "repostCount": 9,
                        "likeCount": 670,
                        "quoteCount": 1,
                        "indexedAt": "2025-04-25T03:25:44.578Z",
                        "viewer": {
                            "threadMuted": false,
                            "embeddingDisabled": false
                        },
                        "labels": [],
                        "$type": "app.bsky.feed.defs#postView"
                    },
                    "parent": {
                        "uri": "at://did:plc:46xpsjkrbhv4tphe36nszjna/app.bsky.feed.post/3lnoarqrud22j",
                        "cid": "bafyreidejqrrcsfmjo4klt7axtdglp5e5cnrterovqxzd6bmbqz3tvlvua",
                        "author": {
                            "did": "did:plc:46xpsjkrbhv4tphe36nszjna",
                            "handle": "wildeied.bsky.social",
                            "displayName": "",
                            "avatar": "https://cdn.bsky.app/img/avatar/plain/did:plc:46xpsjkrbhv4tphe36nszjna/bafkreigfj7ncdyhymtucroqncdz2d6mx7ye2fartfivgjleagakckxdnuq@jpeg",
                            "viewer": {
                                "muted": false,
                                "blockedBy": false
                            },
                            "labels": [],
                            "createdAt": "2024-11-21T17:31:07.971Z"
                        },
                        "record": {
                            "$type": "app.bsky.feed.post",
                            "createdAt": "2025-04-25T22:16:26.101Z",
                            "langs": [
                                "en"
                            ],
                            "reply": {
                                "parent": {
                                    "cid": "bafyreigrph3jf4zs3wnntm6ubbdknvv36evndg7odf56xubrnu57cgoiqa",
                                    "uri": "at://did:plc:hfgp6pj3akhqxntgqwramlbg/app.bsky.feed.post/3lnmbltm4j223"
                                },
                                "root": {
                                    "cid": "bafyreigrph3jf4zs3wnntm6ubbdknvv36evndg7odf56xubrnu57cgoiqa",
                                    "uri": "at://did:plc:hfgp6pj3akhqxntgqwramlbg/app.bsky.feed.post/3lnmbltm4j223"
                                }
                            },
                            "text": "Poor kitty.  We had to remove ALL of our kitty's teeth (at great expense) stomatosis?  But is really doing a lot better now.  Doesn't seem to miss them at all."
                        },
                        "replyCount": 1,
                        "repostCount": 0,
                        "likeCount": 0,
                        "quoteCount": 0,
                        "indexedAt": "2025-04-25T22:16:26.779Z",
                        "viewer": {
                            "threadMuted": false,
                            "embeddingDisabled": false
                        },
                        "labels": [],
                        "$type": "app.bsky.feed.defs#postView"
                    },
                    "grandparentAuthor": {
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
                        "createdAt": "2023-04-22T22:44:04.316Z"
                    }
                }
            }
            """;

            FeedViewPost? feedViewPost = JsonSerializer.Deserialize<FeedViewPost>(json, BlueskyServer.BlueskyJsonSerializerOptions);

            Assert.NotNull(feedViewPost);
        }
    }
}
