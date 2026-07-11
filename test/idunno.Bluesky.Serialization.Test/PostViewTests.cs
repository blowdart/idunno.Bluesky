// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;

using idunno.AtProto;
using idunno.Bluesky.Feed;

namespace idunno.Bluesky.Serialization.Test;

public class PostViewTests
{
    private readonly JsonSerializerOptions _options = new(JsonSerializerDefaults.Web) { AllowOutOfOrderMetadataProperties = true };

    [Fact]
    public void SimplePostViewShouldDeserialize()
    {
        string json = """
            {
                "uri": "at://did:plc:hfgp6pj3akhqxntgqwramlbg/app.bsky.feed.post/3l66cdbste424",
                "cid": "bafyreibtb55kfqsny3qk4c2puoqom4cu5zkpnp5xdwu5dud264b4efocyq",
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
                "record": {
                    "$type": "app.bsky.feed.post",
                    "createdAt": "2024-10-10T16:43:37.262Z",
                    "langs": [
                        "en"
                    ],
                    "text": "What doesn't kill you disappoints me."
                },
                "bookmarkCount": 4,
                "replyCount": 14,
                "repostCount": 26,
                "likeCount": 160,
                "quoteCount": 2,
                "indexedAt": "2024-10-10T16:43:37.262Z",
                "viewer": {
                    "bookmarked": false,
                    "threadMuted": false,
                    "replyDisabled": false,
                    "embeddingDisabled": false
                },
                "labels": [],
                "threadgate": {
                    "uri": "at://did:plc:hfgp6pj3akhqxntgqwramlbg/app.bsky.feed.threadgate/3l66cdbste424",
                    "cid": "bafyreidhzawv3ddhrrnyssjaofz7tadqnlp6txbsybxrrcxsmg4m46sose",
                    "record": {
                        "$type": "app.bsky.feed.threadgate",
                        "createdAt": "2025-09-09T14:17:39.972Z",
                        "hiddenReplies": [
                            "at://did:plc:ya6t4yar5gqysdesbhf6s5hy/app.bsky.feed.post/3lyfvpujsk22h",
                            "at://did:plc:2im2mnjlctr3c5qnp4d5rkek/app.bsky.feed.post/3ldapa4rtqs2w"
                        ],
                        "post": "at://did:plc:hfgp6pj3akhqxntgqwramlbg/app.bsky.feed.post/3l66cdbste424"
                    },
                    "lists": []
                }
            }
            """;
        PostView? postView = JsonSerializer.Deserialize<PostView>(json, _options);
        Assert.NotNull(postView);
        Assert.Null(postView.Embed);
        Assert.Equal(new AtUri("at://did:plc:hfgp6pj3akhqxntgqwramlbg/app.bsky.feed.post/3l66cdbste424"), postView.Uri);
        Assert.Equal(new Cid("bafyreibtb55kfqsny3qk4c2puoqom4cu5zkpnp5xdwu5dud264b4efocyq"), postView.Cid);
        Assert.NotNull(postView.Author);
        Assert.Equal(new Did("did:plc:hfgp6pj3akhqxntgqwramlbg"), postView.Author.Did);
        Assert.Equal(new Handle("blowdart.me"), postView.Author.Handle);
        Assert.Equal("Barry Dorrans", postView.Author.DisplayName);
        Assert.Equal("He/Him", postView.Author.Pronouns);
        Assert.Equal(new Uri("https://cdn.bsky.app/img/avatar/plain/did:plc:hfgp6pj3akhqxntgqwramlbg/bafkreicwjaromkjs4jrd5uqznacfgzvhnob2il5fwywxqopbnhfb74n27m"), postView.Author.Avatar);
        Assert.NotNull(postView.Author.Associated);
        Assert.NotNull(postView.Author.Associated.Chat);
        Assert.Equal(Actor.AllowIncomingChat.All, postView.Author.Associated.Chat.AllowIncoming);
        Assert.NotNull(postView.Author.Associated.ActivitySubscription);
        Assert.Equal(Notifications.NotificationAllowedFrom.Followers, postView.Author.Associated.ActivitySubscription.AllowSubscriptions);
        Assert.NotNull(postView.Author.Associated.Germ);
        Assert.Equal(GermNetwork.Com.ShowButtonToKnownValues.UsersIFollow, postView.Author.Associated.Germ.ShowButtonTo);
        Assert.Equal(new Uri("https://landing.ger.mx/newUser"), postView.Author.Associated.Germ.MessageMeUrl);
        Assert.NotNull(postView.Author.Viewer);
        Assert.False(postView.Author.Viewer.Muted);
        Assert.False(postView.Author.Viewer.BlockedBy);
        Assert.Equal(new AtUri("at://did:plc:ec72yg6n2sydzjvtovvdlxrk/app.bsky.graph.follow/3kqxzemnnc425"), postView.Author.Viewer.Following);
        Assert.Equal(new AtUri("at://did:plc:hfgp6pj3akhqxntgqwramlbg/app.bsky.graph.follow/3ko2gwpx37l2m"), postView.Author.Viewer.FollowedBy);
        Assert.NotNull(postView.Author.Labels);
        Assert.Empty(postView.Author.Labels);
        Assert.Equal(DateTimeOffset.Parse("2023-04-22T22:44:04.316Z"), postView.Author.CreatedAt);
        Assert.NotNull(postView.Record);
        Assert.IsType<Post>(postView.Record);
        Post pinnedPost = postView.Record;
        Assert.Equal("What doesn't kill you disappoints me.", pinnedPost.Text);
        Assert.Equal(DateTimeOffset.Parse("2024-10-10T16:43:37.262Z"), pinnedPost.CreatedAt);
        Assert.NotNull(pinnedPost.Langs);
        Assert.Equal("en", pinnedPost.Langs.ElementAt(0));
        Assert.Null(pinnedPost.Labels);
        Assert.Empty(postView.Labels);
        Assert.Empty(postView.SelfLabels);
        Assert.Equal(4, postView.BookmarkCount);
        Assert.Equal(14, postView.ReplyCount);
        Assert.Equal(26, postView.RepostCount);
        Assert.Equal(160, postView.LikeCount);
        Assert.Equal(2, postView.QuoteCount);
        Assert.NotNull(postView.Viewer);
        Assert.False(postView.Viewer.Bookmarked);
        Assert.False(postView.Viewer.ThreadMuted);
        Assert.False(postView.Viewer.ReplyDisabled);
        Assert.False(postView.Viewer.EmbeddingDisabled);
        Assert.NotNull(postView.ThreadGate);
        Assert.Equal(new AtUri("at://did:plc:hfgp6pj3akhqxntgqwramlbg/app.bsky.feed.threadgate/3l66cdbste424"), postView.ThreadGate.Uri);
        Assert.Equal(new Cid("bafyreidhzawv3ddhrrnyssjaofz7tadqnlp6txbsybxrrcxsmg4m46sose"), postView.ThreadGate.Cid);
        Assert.IsType<Feed.Gates.ThreadGate>(postView.ThreadGate.Record);
        Assert.Equal(DateTimeOffset.Parse("2025-09-09T14:17:39.972Z"), postView.ThreadGate.Record.CreatedAt);
        Assert.NotNull(postView.ThreadGate.Record.HiddenReplies);
        Assert.Equal(2, postView.ThreadGate.Record.HiddenReplies.Count);
        Assert.Equal(new AtUri("at://did:plc:ya6t4yar5gqysdesbhf6s5hy/app.bsky.feed.post/3lyfvpujsk22h"), postView.ThreadGate.Record.HiddenReplies.ElementAt(0));
        Assert.Equal(new AtUri("at://did:plc:2im2mnjlctr3c5qnp4d5rkek/app.bsky.feed.post/3ldapa4rtqs2w"), postView.ThreadGate.Record.HiddenReplies.ElementAt(1));
        Assert.Equal(new AtUri("at://did:plc:hfgp6pj3akhqxntgqwramlbg/app.bsky.feed.post/3l66cdbste424"), postView.ThreadGate.Record.Post);
        Assert.Empty(postView.ThreadGate.Lists);
    }

    [Fact]
    public void SimplePostViewShouldDeserializeWithBlueskyOptions()
    {
        string json = """
            {
                "uri": "at://did:plc:hfgp6pj3akhqxntgqwramlbg/app.bsky.feed.post/3l66cdbste424",
                "cid": "bafyreibtb55kfqsny3qk4c2puoqom4cu5zkpnp5xdwu5dud264b4efocyq",
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
                "record": {
                    "$type": "app.bsky.feed.post",
                    "createdAt": "2024-10-10T16:43:37.262Z",
                    "langs": [
                        "en"
                    ],
                    "text": "What doesn't kill you disappoints me."
                },
                "bookmarkCount": 4,
                "replyCount": 14,
                "repostCount": 26,
                "likeCount": 160,
                "quoteCount": 2,
                "indexedAt": "2024-10-10T16:43:37.262Z",
                "viewer": {
                    "bookmarked": false,
                    "threadMuted": false,
                    "replyDisabled": false,
                    "embeddingDisabled": false
                },
                "labels": [],
                "threadgate": {
                    "uri": "at://did:plc:hfgp6pj3akhqxntgqwramlbg/app.bsky.feed.threadgate/3l66cdbste424",
                    "cid": "bafyreidhzawv3ddhrrnyssjaofz7tadqnlp6txbsybxrrcxsmg4m46sose",
                    "record": {
                        "$type": "app.bsky.feed.threadgate",
                        "createdAt": "2025-09-09T14:17:39.972Z",
                        "hiddenReplies": [
                            "at://did:plc:ya6t4yar5gqysdesbhf6s5hy/app.bsky.feed.post/3lyfvpujsk22h",
                            "at://did:plc:2im2mnjlctr3c5qnp4d5rkek/app.bsky.feed.post/3ldapa4rtqs2w"
                        ],
                        "post": "at://did:plc:hfgp6pj3akhqxntgqwramlbg/app.bsky.feed.post/3l66cdbste424"
                    },
                    "lists": []
                }
            }
            """;
        PostView? postView = JsonSerializer.Deserialize<PostView>(json, BlueskyJsonSerializerOptions.Options);
        Assert.NotNull(postView);
        Assert.Null(postView.Embed);
        Assert.Equal(new AtUri("at://did:plc:hfgp6pj3akhqxntgqwramlbg/app.bsky.feed.post/3l66cdbste424"), postView.Uri);
        Assert.Equal(new Cid("bafyreibtb55kfqsny3qk4c2puoqom4cu5zkpnp5xdwu5dud264b4efocyq"), postView.Cid);
        Assert.NotNull(postView.Author);
        Assert.Equal(new Did("did:plc:hfgp6pj3akhqxntgqwramlbg"), postView.Author.Did);
        Assert.Equal(new Handle("blowdart.me"), postView.Author.Handle);
        Assert.Equal("Barry Dorrans", postView.Author.DisplayName);
        Assert.Equal("He/Him", postView.Author.Pronouns);
        Assert.Equal(new Uri("https://cdn.bsky.app/img/avatar/plain/did:plc:hfgp6pj3akhqxntgqwramlbg/bafkreicwjaromkjs4jrd5uqznacfgzvhnob2il5fwywxqopbnhfb74n27m"), postView.Author.Avatar);
        Assert.NotNull(postView.Author.Associated);
        Assert.NotNull(postView.Author.Associated.Chat);
        Assert.Equal(Actor.AllowIncomingChat.All, postView.Author.Associated.Chat.AllowIncoming);
        Assert.NotNull(postView.Author.Associated.ActivitySubscription);
        Assert.Equal(Notifications.NotificationAllowedFrom.Followers, postView.Author.Associated.ActivitySubscription.AllowSubscriptions);
        Assert.NotNull(postView.Author.Associated.Germ);
        Assert.Equal(GermNetwork.Com.ShowButtonToKnownValues.UsersIFollow, postView.Author.Associated.Germ.ShowButtonTo);
        Assert.Equal(new Uri("https://landing.ger.mx/newUser"), postView.Author.Associated.Germ.MessageMeUrl);
        Assert.NotNull(postView.Author.Viewer);
        Assert.False(postView.Author.Viewer.Muted);
        Assert.False(postView.Author.Viewer.BlockedBy);
        Assert.Equal(new AtUri("at://did:plc:ec72yg6n2sydzjvtovvdlxrk/app.bsky.graph.follow/3kqxzemnnc425"), postView.Author.Viewer.Following);
        Assert.Equal(new AtUri("at://did:plc:hfgp6pj3akhqxntgqwramlbg/app.bsky.graph.follow/3ko2gwpx37l2m"), postView.Author.Viewer.FollowedBy);
        Assert.NotNull(postView.Author.Labels);
        Assert.Empty(postView.Author.Labels);
        Assert.Equal(DateTimeOffset.Parse("2023-04-22T22:44:04.316Z"), postView.Author.CreatedAt);
        Assert.NotNull(postView.Record);
        Assert.IsType<Post>(postView.Record);
        Post pinnedPost = postView.Record;
        Assert.Equal("What doesn't kill you disappoints me.", pinnedPost.Text);
        Assert.Equal(DateTimeOffset.Parse("2024-10-10T16:43:37.262Z"), pinnedPost.CreatedAt);
        Assert.NotNull(pinnedPost.Langs);
        Assert.Equal("en", pinnedPost.Langs.ElementAt(0));
        Assert.Null(pinnedPost.Labels);
        Assert.Empty(postView.Labels);
        Assert.Empty(postView.SelfLabels);
        Assert.Equal(4, postView.BookmarkCount);
        Assert.Equal(14, postView.ReplyCount);
        Assert.Equal(26, postView.RepostCount);
        Assert.Equal(160, postView.LikeCount);
        Assert.Equal(2, postView.QuoteCount);
        Assert.NotNull(postView.Viewer);
        Assert.False(postView.Viewer.Bookmarked);
        Assert.False(postView.Viewer.ThreadMuted);
        Assert.False(postView.Viewer.ReplyDisabled);
        Assert.False(postView.Viewer.EmbeddingDisabled);
        Assert.NotNull(postView.ThreadGate);
        Assert.Equal(new AtUri("at://did:plc:hfgp6pj3akhqxntgqwramlbg/app.bsky.feed.threadgate/3l66cdbste424"), postView.ThreadGate.Uri);
        Assert.Equal(new Cid("bafyreidhzawv3ddhrrnyssjaofz7tadqnlp6txbsybxrrcxsmg4m46sose"), postView.ThreadGate.Cid);
        Assert.IsType<Feed.Gates.ThreadGate>(postView.ThreadGate.Record);
        Assert.Equal(DateTimeOffset.Parse("2025-09-09T14:17:39.972Z"), postView.ThreadGate.Record.CreatedAt);
        Assert.NotNull(postView.ThreadGate.Record.HiddenReplies);
        Assert.Equal(2, postView.ThreadGate.Record.HiddenReplies.Count);
        Assert.Equal(new AtUri("at://did:plc:ya6t4yar5gqysdesbhf6s5hy/app.bsky.feed.post/3lyfvpujsk22h"), postView.ThreadGate.Record.HiddenReplies.ElementAt(0));
        Assert.Equal(new AtUri("at://did:plc:2im2mnjlctr3c5qnp4d5rkek/app.bsky.feed.post/3ldapa4rtqs2w"), postView.ThreadGate.Record.HiddenReplies.ElementAt(1));
        Assert.Equal(new AtUri("at://did:plc:hfgp6pj3akhqxntgqwramlbg/app.bsky.feed.post/3l66cdbste424"), postView.ThreadGate.Record.Post);
        Assert.Empty(postView.ThreadGate.Lists);
    }

    [Fact]
    public void PostViewWithEmbeddedGalleryShouldDeserialize()
    {
        string json = """
            {
                "uri": "at://did:plc:ec72yg6n2sydzjvtovvdlxrk/app.bsky.feed.post/3mnzf3lvwvq2t",
                "cid": "bafyreihjrdyjgjnl4t6dovnkgegbcd5ch6cuatkj22hq7ghkopfd2jcu6i",
                "author": {
                    "did": "did:plc:ec72yg6n2sydzjvtovvdlxrk",
                    "handle": "bot.idunno.blue",
                    "displayName": "Test Bot",
                    "pronouns": "it/its",
                    "avatar": "https://cdn.bsky.app/img/avatar/plain/did:plc:ec72yg6n2sydzjvtovvdlxrk/bafkreig5ujmxrechgakn4ukf37oj6mlpivukqfbgpuhb3pqjmdkxxtjpnq",
                    "associated": {
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
                    "createdAt": "2024-03-19T13:00:19.046Z"
                },
                "record": {
                    "$type": "app.bsky.feed.post",
                    "createdAt": "2026-06-11T13:58:57.7406626+00:00",
                    "embed": {
                        "$type": "app.bsky.embed.gallery",
                        "items": [
                            {
                                "$type": "app.bsky.embed.gallery#image",
                                "alt": "Long cat is long, sideways",
                                "aspectRatio": {
                                    "height": 138,
                                    "width": 142
                                },
                                "image": {
                                    "$type": "blob",
                                    "ref": {
                                        "$link": "bafkreidsj4g5hv2glhdr5yznsml2njqgeomc4hsx46fdyqlyyj6pzbhheu"
                                    },
                                    "mimeType": "image/webp",
                                    "size": 1432
                                }
                            },
                            {
                                "$type": "app.bsky.embed.gallery#image",
                                "alt": "Long cat is long, sideways",
                                "aspectRatio": {
                                    "height": 138,
                                    "width": 198
                                },
                                "image": {
                                    "$type": "blob",
                                    "ref": {
                                        "$link": "bafkreierjvtgtcqt53o5rfrlahctdeiczekegsxpo5nyxhn4qywgghiln4"
                                    },
                                    "mimeType": "image/webp",
                                    "size": 1260
                                }
                            },
                            {
                                "$type": "app.bsky.embed.gallery#image",
                                "alt": "Long cat is long, sideways",
                                "aspectRatio": {
                                    "height": 138,
                                    "width": 198
                                },
                                "image": {
                                    "$type": "blob",
                                    "ref": {
                                        "$link": "bafkreierjvtgtcqt53o5rfrlahctdeiczekegsxpo5nyxhn4qywgghiln4"
                                    },
                                    "mimeType": "image/webp",
                                    "size": 1260
                                }
                            },
                            {
                                "$type": "app.bsky.embed.gallery#image",
                                "alt": "Long cat is long, sideways",
                                "aspectRatio": {
                                    "height": 138,
                                    "width": 198
                                },
                                "image": {
                                    "$type": "blob",
                                    "ref": {
                                        "$link": "bafkreierjvtgtcqt53o5rfrlahctdeiczekegsxpo5nyxhn4qywgghiln4"
                                    },
                                    "mimeType": "image/webp",
                                    "size": 1260
                                }
                            },
                            {
                                "$type": "app.bsky.embed.gallery#image",
                                "alt": "Long cat is long, sideways",
                                "aspectRatio": {
                                    "height": 138,
                                    "width": 198
                                },
                                "image": {
                                    "$type": "blob",
                                    "ref": {
                                        "$link": "bafkreierjvtgtcqt53o5rfrlahctdeiczekegsxpo5nyxhn4qywgghiln4"
                                    },
                                    "mimeType": "image/webp",
                                    "size": 1260
                                }
                            },
                            {
                                "$type": "app.bsky.embed.gallery#image",
                                "alt": "Long cat is long, sideways",
                                "aspectRatio": {
                                    "height": 138,
                                    "width": 198
                                },
                                "image": {
                                    "$type": "blob",
                                    "ref": {
                                        "$link": "bafkreierjvtgtcqt53o5rfrlahctdeiczekegsxpo5nyxhn4qywgghiln4"
                                    },
                                    "mimeType": "image/webp",
                                    "size": 1260
                                }
                            },
                            {
                                "$type": "app.bsky.embed.gallery#image",
                                "alt": "Long cat is long, sideways",
                                "aspectRatio": {
                                    "height": 138,
                                    "width": 198
                                },
                                "image": {
                                    "$type": "blob",
                                    "ref": {
                                        "$link": "bafkreierjvtgtcqt53o5rfrlahctdeiczekegsxpo5nyxhn4qywgghiln4"
                                    },
                                    "mimeType": "image/webp",
                                    "size": 1260
                                }
                            },
                            {
                                "$type": "app.bsky.embed.gallery#image",
                                "alt": "Long cat is long, sideways",
                                "aspectRatio": {
                                    "height": 138,
                                    "width": 198
                                },
                                "image": {
                                    "$type": "blob",
                                    "ref": {
                                        "$link": "bafkreierjvtgtcqt53o5rfrlahctdeiczekegsxpo5nyxhn4qywgghiln4"
                                    },
                                    "mimeType": "image/webp",
                                    "size": 1260
                                }
                            },
                            {
                                "$type": "app.bsky.embed.gallery#image",
                                "alt": "Long cat is long, sideways",
                                "aspectRatio": {
                                    "height": 138,
                                    "width": 164
                                },
                                "image": {
                                    "$type": "blob",
                                    "ref": {
                                        "$link": "bafkreigfkwp7thnpirniwjombepq2iyuqqtauu6qazr3yeqgrd567d5vue"
                                    },
                                    "mimeType": "image/webp",
                                    "size": 1398
                                }
                            }
                        ]
                    },
                    "text": "Long cat is long, sideways"
                },
                "embed": {
                    "items": [
                        {
                            "thumbnail": "https://cdn.bsky.app/img/feed_thumbnail/plain/did:plc:ec72yg6n2sydzjvtovvdlxrk/bafkreidsj4g5hv2glhdr5yznsml2njqgeomc4hsx46fdyqlyyj6pzbhheu",
                            "fullsize": "https://cdn.bsky.app/img/feed_fullsize/plain/did:plc:ec72yg6n2sydzjvtovvdlxrk/bafkreidsj4g5hv2glhdr5yznsml2njqgeomc4hsx46fdyqlyyj6pzbhheu",
                            "alt": "Long cat is long, sideways",
                            "aspectRatio": {
                                "height": 138,
                                "width": 142
                            },
                            "$type": "app.bsky.embed.gallery#viewImage"
                        },
                        {
                            "thumbnail": "https://cdn.bsky.app/img/feed_thumbnail/plain/did:plc:ec72yg6n2sydzjvtovvdlxrk/bafkreierjvtgtcqt53o5rfrlahctdeiczekegsxpo5nyxhn4qywgghiln4",
                            "fullsize": "https://cdn.bsky.app/img/feed_fullsize/plain/did:plc:ec72yg6n2sydzjvtovvdlxrk/bafkreierjvtgtcqt53o5rfrlahctdeiczekegsxpo5nyxhn4qywgghiln4",
                            "alt": "Long cat is long, sideways",
                            "aspectRatio": {
                                "height": 138,
                                "width": 198
                            },
                            "$type": "app.bsky.embed.gallery#viewImage"
                        },
                        {
                            "thumbnail": "https://cdn.bsky.app/img/feed_thumbnail/plain/did:plc:ec72yg6n2sydzjvtovvdlxrk/bafkreierjvtgtcqt53o5rfrlahctdeiczekegsxpo5nyxhn4qywgghiln4",
                            "fullsize": "https://cdn.bsky.app/img/feed_fullsize/plain/did:plc:ec72yg6n2sydzjvtovvdlxrk/bafkreierjvtgtcqt53o5rfrlahctdeiczekegsxpo5nyxhn4qywgghiln4",
                            "alt": "Long cat is long, sideways",
                            "aspectRatio": {
                                "height": 138,
                                "width": 198
                            },
                            "$type": "app.bsky.embed.gallery#viewImage"
                        },
                        {
                            "thumbnail": "https://cdn.bsky.app/img/feed_thumbnail/plain/did:plc:ec72yg6n2sydzjvtovvdlxrk/bafkreierjvtgtcqt53o5rfrlahctdeiczekegsxpo5nyxhn4qywgghiln4",
                            "fullsize": "https://cdn.bsky.app/img/feed_fullsize/plain/did:plc:ec72yg6n2sydzjvtovvdlxrk/bafkreierjvtgtcqt53o5rfrlahctdeiczekegsxpo5nyxhn4qywgghiln4",
                            "alt": "Long cat is long, sideways",
                            "aspectRatio": {
                                "height": 138,
                                "width": 198
                            },
                            "$type": "app.bsky.embed.gallery#viewImage"
                        },
                        {
                            "thumbnail": "https://cdn.bsky.app/img/feed_thumbnail/plain/did:plc:ec72yg6n2sydzjvtovvdlxrk/bafkreierjvtgtcqt53o5rfrlahctdeiczekegsxpo5nyxhn4qywgghiln4",
                            "fullsize": "https://cdn.bsky.app/img/feed_fullsize/plain/did:plc:ec72yg6n2sydzjvtovvdlxrk/bafkreierjvtgtcqt53o5rfrlahctdeiczekegsxpo5nyxhn4qywgghiln4",
                            "alt": "Long cat is long, sideways",
                            "aspectRatio": {
                                "height": 138,
                                "width": 198
                            },
                            "$type": "app.bsky.embed.gallery#viewImage"
                        },
                        {
                            "thumbnail": "https://cdn.bsky.app/img/feed_thumbnail/plain/did:plc:ec72yg6n2sydzjvtovvdlxrk/bafkreierjvtgtcqt53o5rfrlahctdeiczekegsxpo5nyxhn4qywgghiln4",
                            "fullsize": "https://cdn.bsky.app/img/feed_fullsize/plain/did:plc:ec72yg6n2sydzjvtovvdlxrk/bafkreierjvtgtcqt53o5rfrlahctdeiczekegsxpo5nyxhn4qywgghiln4",
                            "alt": "Long cat is long, sideways",
                            "aspectRatio": {
                                "height": 138,
                                "width": 198
                            },
                            "$type": "app.bsky.embed.gallery#viewImage"
                        },
                        {
                            "thumbnail": "https://cdn.bsky.app/img/feed_thumbnail/plain/did:plc:ec72yg6n2sydzjvtovvdlxrk/bafkreierjvtgtcqt53o5rfrlahctdeiczekegsxpo5nyxhn4qywgghiln4",
                            "fullsize": "https://cdn.bsky.app/img/feed_fullsize/plain/did:plc:ec72yg6n2sydzjvtovvdlxrk/bafkreierjvtgtcqt53o5rfrlahctdeiczekegsxpo5nyxhn4qywgghiln4",
                            "alt": "Long cat is long, sideways",
                            "aspectRatio": {
                                "height": 138,
                                "width": 198
                            },
                            "$type": "app.bsky.embed.gallery#viewImage"
                        },
                        {
                            "thumbnail": "https://cdn.bsky.app/img/feed_thumbnail/plain/did:plc:ec72yg6n2sydzjvtovvdlxrk/bafkreierjvtgtcqt53o5rfrlahctdeiczekegsxpo5nyxhn4qywgghiln4",
                            "fullsize": "https://cdn.bsky.app/img/feed_fullsize/plain/did:plc:ec72yg6n2sydzjvtovvdlxrk/bafkreierjvtgtcqt53o5rfrlahctdeiczekegsxpo5nyxhn4qywgghiln4",
                            "alt": "Long cat is long, sideways",
                            "aspectRatio": {
                                "height": 138,
                                "width": 198
                            },
                            "$type": "app.bsky.embed.gallery#viewImage"
                        },
                        {
                            "thumbnail": "https://cdn.bsky.app/img/feed_thumbnail/plain/did:plc:ec72yg6n2sydzjvtovvdlxrk/bafkreigfkwp7thnpirniwjombepq2iyuqqtauu6qazr3yeqgrd567d5vue",
                            "fullsize": "https://cdn.bsky.app/img/feed_fullsize/plain/did:plc:ec72yg6n2sydzjvtovvdlxrk/bafkreigfkwp7thnpirniwjombepq2iyuqqtauu6qazr3yeqgrd567d5vue",
                            "alt": "Long cat is long, sideways",
                            "aspectRatio": {
                                "height": 138,
                                "width": 164
                            },
                            "$type": "app.bsky.embed.gallery#viewImage"
                        }
                    ],
                    "$type": "app.bsky.embed.gallery#view"
                },
                "bookmarkCount": 0,
                "replyCount": 0,
                "repostCount": 0,
                "likeCount": 0,
                "quoteCount": 1,
                "indexedAt": "2026-06-11T13:58:59.170Z",
                "viewer": {
                    "bookmarked": false,
                    "threadMuted": false,
                    "embeddingDisabled": false,
                    "pinned": false
                },
                "labels": []
            }
            """;

        PostView? postView = JsonSerializer.Deserialize<PostView>(json, _options);
        Assert.NotNull(postView);
        Assert.IsType<Embed.Gallery.View>(postView.Embed);
    }

    [Fact]
    public void PostViewWithEmbeddedGalleryShouldDeserializeWithBlueskyOptions()
    {
        string json = """
            {
                "uri": "at://did:plc:ec72yg6n2sydzjvtovvdlxrk/app.bsky.feed.post/3mnzf3lvwvq2t",
                "cid": "bafyreihjrdyjgjnl4t6dovnkgegbcd5ch6cuatkj22hq7ghkopfd2jcu6i",
                "author": {
                    "did": "did:plc:ec72yg6n2sydzjvtovvdlxrk",
                    "handle": "bot.idunno.blue",
                    "displayName": "Test Bot",
                    "pronouns": "it/its",
                    "avatar": "https://cdn.bsky.app/img/avatar/plain/did:plc:ec72yg6n2sydzjvtovvdlxrk/bafkreig5ujmxrechgakn4ukf37oj6mlpivukqfbgpuhb3pqjmdkxxtjpnq",
                    "associated": {
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
                    "createdAt": "2024-03-19T13:00:19.046Z"
                },
                "record": {
                    "$type": "app.bsky.feed.post",
                    "createdAt": "2026-06-11T13:58:57.7406626+00:00",
                    "embed": {
                        "$type": "app.bsky.embed.gallery",
                        "items": [
                            {
                                "$type": "app.bsky.embed.gallery#image",
                                "alt": "Long cat is long, sideways",
                                "aspectRatio": {
                                    "height": 138,
                                    "width": 142
                                },
                                "image": {
                                    "$type": "blob",
                                    "ref": {
                                        "$link": "bafkreidsj4g5hv2glhdr5yznsml2njqgeomc4hsx46fdyqlyyj6pzbhheu"
                                    },
                                    "mimeType": "image/webp",
                                    "size": 1432
                                }
                            },
                            {
                                "$type": "app.bsky.embed.gallery#image",
                                "alt": "Long cat is long, sideways",
                                "aspectRatio": {
                                    "height": 138,
                                    "width": 198
                                },
                                "image": {
                                    "$type": "blob",
                                    "ref": {
                                        "$link": "bafkreierjvtgtcqt53o5rfrlahctdeiczekegsxpo5nyxhn4qywgghiln4"
                                    },
                                    "mimeType": "image/webp",
                                    "size": 1260
                                }
                            },
                            {
                                "$type": "app.bsky.embed.gallery#image",
                                "alt": "Long cat is long, sideways",
                                "aspectRatio": {
                                    "height": 138,
                                    "width": 198
                                },
                                "image": {
                                    "$type": "blob",
                                    "ref": {
                                        "$link": "bafkreierjvtgtcqt53o5rfrlahctdeiczekegsxpo5nyxhn4qywgghiln4"
                                    },
                                    "mimeType": "image/webp",
                                    "size": 1260
                                }
                            },
                            {
                                "$type": "app.bsky.embed.gallery#image",
                                "alt": "Long cat is long, sideways",
                                "aspectRatio": {
                                    "height": 138,
                                    "width": 198
                                },
                                "image": {
                                    "$type": "blob",
                                    "ref": {
                                        "$link": "bafkreierjvtgtcqt53o5rfrlahctdeiczekegsxpo5nyxhn4qywgghiln4"
                                    },
                                    "mimeType": "image/webp",
                                    "size": 1260
                                }
                            },
                            {
                                "$type": "app.bsky.embed.gallery#image",
                                "alt": "Long cat is long, sideways",
                                "aspectRatio": {
                                    "height": 138,
                                    "width": 198
                                },
                                "image": {
                                    "$type": "blob",
                                    "ref": {
                                        "$link": "bafkreierjvtgtcqt53o5rfrlahctdeiczekegsxpo5nyxhn4qywgghiln4"
                                    },
                                    "mimeType": "image/webp",
                                    "size": 1260
                                }
                            },
                            {
                                "$type": "app.bsky.embed.gallery#image",
                                "alt": "Long cat is long, sideways",
                                "aspectRatio": {
                                    "height": 138,
                                    "width": 198
                                },
                                "image": {
                                    "$type": "blob",
                                    "ref": {
                                        "$link": "bafkreierjvtgtcqt53o5rfrlahctdeiczekegsxpo5nyxhn4qywgghiln4"
                                    },
                                    "mimeType": "image/webp",
                                    "size": 1260
                                }
                            },
                            {
                                "$type": "app.bsky.embed.gallery#image",
                                "alt": "Long cat is long, sideways",
                                "aspectRatio": {
                                    "height": 138,
                                    "width": 198
                                },
                                "image": {
                                    "$type": "blob",
                                    "ref": {
                                        "$link": "bafkreierjvtgtcqt53o5rfrlahctdeiczekegsxpo5nyxhn4qywgghiln4"
                                    },
                                    "mimeType": "image/webp",
                                    "size": 1260
                                }
                            },
                            {
                                "$type": "app.bsky.embed.gallery#image",
                                "alt": "Long cat is long, sideways",
                                "aspectRatio": {
                                    "height": 138,
                                    "width": 198
                                },
                                "image": {
                                    "$type": "blob",
                                    "ref": {
                                        "$link": "bafkreierjvtgtcqt53o5rfrlahctdeiczekegsxpo5nyxhn4qywgghiln4"
                                    },
                                    "mimeType": "image/webp",
                                    "size": 1260
                                }
                            },
                            {
                                "$type": "app.bsky.embed.gallery#image",
                                "alt": "Long cat is long, sideways",
                                "aspectRatio": {
                                    "height": 138,
                                    "width": 164
                                },
                                "image": {
                                    "$type": "blob",
                                    "ref": {
                                        "$link": "bafkreigfkwp7thnpirniwjombepq2iyuqqtauu6qazr3yeqgrd567d5vue"
                                    },
                                    "mimeType": "image/webp",
                                    "size": 1398
                                }
                            }
                        ]
                    },
                    "text": "Long cat is long, sideways"
                },
                "embed": {
                    "items": [
                        {
                            "thumbnail": "https://cdn.bsky.app/img/feed_thumbnail/plain/did:plc:ec72yg6n2sydzjvtovvdlxrk/bafkreidsj4g5hv2glhdr5yznsml2njqgeomc4hsx46fdyqlyyj6pzbhheu",
                            "fullsize": "https://cdn.bsky.app/img/feed_fullsize/plain/did:plc:ec72yg6n2sydzjvtovvdlxrk/bafkreidsj4g5hv2glhdr5yznsml2njqgeomc4hsx46fdyqlyyj6pzbhheu",
                            "alt": "Long cat is long, sideways",
                            "aspectRatio": {
                                "height": 138,
                                "width": 142
                            },
                            "$type": "app.bsky.embed.gallery#viewImage"
                        },
                        {
                            "thumbnail": "https://cdn.bsky.app/img/feed_thumbnail/plain/did:plc:ec72yg6n2sydzjvtovvdlxrk/bafkreierjvtgtcqt53o5rfrlahctdeiczekegsxpo5nyxhn4qywgghiln4",
                            "fullsize": "https://cdn.bsky.app/img/feed_fullsize/plain/did:plc:ec72yg6n2sydzjvtovvdlxrk/bafkreierjvtgtcqt53o5rfrlahctdeiczekegsxpo5nyxhn4qywgghiln4",
                            "alt": "Long cat is long, sideways",
                            "aspectRatio": {
                                "height": 138,
                                "width": 198
                            },
                            "$type": "app.bsky.embed.gallery#viewImage"
                        },
                        {
                            "thumbnail": "https://cdn.bsky.app/img/feed_thumbnail/plain/did:plc:ec72yg6n2sydzjvtovvdlxrk/bafkreierjvtgtcqt53o5rfrlahctdeiczekegsxpo5nyxhn4qywgghiln4",
                            "fullsize": "https://cdn.bsky.app/img/feed_fullsize/plain/did:plc:ec72yg6n2sydzjvtovvdlxrk/bafkreierjvtgtcqt53o5rfrlahctdeiczekegsxpo5nyxhn4qywgghiln4",
                            "alt": "Long cat is long, sideways",
                            "aspectRatio": {
                                "height": 138,
                                "width": 198
                            },
                            "$type": "app.bsky.embed.gallery#viewImage"
                        },
                        {
                            "thumbnail": "https://cdn.bsky.app/img/feed_thumbnail/plain/did:plc:ec72yg6n2sydzjvtovvdlxrk/bafkreierjvtgtcqt53o5rfrlahctdeiczekegsxpo5nyxhn4qywgghiln4",
                            "fullsize": "https://cdn.bsky.app/img/feed_fullsize/plain/did:plc:ec72yg6n2sydzjvtovvdlxrk/bafkreierjvtgtcqt53o5rfrlahctdeiczekegsxpo5nyxhn4qywgghiln4",
                            "alt": "Long cat is long, sideways",
                            "aspectRatio": {
                                "height": 138,
                                "width": 198
                            },
                            "$type": "app.bsky.embed.gallery#viewImage"
                        },
                        {
                            "thumbnail": "https://cdn.bsky.app/img/feed_thumbnail/plain/did:plc:ec72yg6n2sydzjvtovvdlxrk/bafkreierjvtgtcqt53o5rfrlahctdeiczekegsxpo5nyxhn4qywgghiln4",
                            "fullsize": "https://cdn.bsky.app/img/feed_fullsize/plain/did:plc:ec72yg6n2sydzjvtovvdlxrk/bafkreierjvtgtcqt53o5rfrlahctdeiczekegsxpo5nyxhn4qywgghiln4",
                            "alt": "Long cat is long, sideways",
                            "aspectRatio": {
                                "height": 138,
                                "width": 198
                            },
                            "$type": "app.bsky.embed.gallery#viewImage"
                        },
                        {
                            "thumbnail": "https://cdn.bsky.app/img/feed_thumbnail/plain/did:plc:ec72yg6n2sydzjvtovvdlxrk/bafkreierjvtgtcqt53o5rfrlahctdeiczekegsxpo5nyxhn4qywgghiln4",
                            "fullsize": "https://cdn.bsky.app/img/feed_fullsize/plain/did:plc:ec72yg6n2sydzjvtovvdlxrk/bafkreierjvtgtcqt53o5rfrlahctdeiczekegsxpo5nyxhn4qywgghiln4",
                            "alt": "Long cat is long, sideways",
                            "aspectRatio": {
                                "height": 138,
                                "width": 198
                            },
                            "$type": "app.bsky.embed.gallery#viewImage"
                        },
                        {
                            "thumbnail": "https://cdn.bsky.app/img/feed_thumbnail/plain/did:plc:ec72yg6n2sydzjvtovvdlxrk/bafkreierjvtgtcqt53o5rfrlahctdeiczekegsxpo5nyxhn4qywgghiln4",
                            "fullsize": "https://cdn.bsky.app/img/feed_fullsize/plain/did:plc:ec72yg6n2sydzjvtovvdlxrk/bafkreierjvtgtcqt53o5rfrlahctdeiczekegsxpo5nyxhn4qywgghiln4",
                            "alt": "Long cat is long, sideways",
                            "aspectRatio": {
                                "height": 138,
                                "width": 198
                            },
                            "$type": "app.bsky.embed.gallery#viewImage"
                        },
                        {
                            "thumbnail": "https://cdn.bsky.app/img/feed_thumbnail/plain/did:plc:ec72yg6n2sydzjvtovvdlxrk/bafkreierjvtgtcqt53o5rfrlahctdeiczekegsxpo5nyxhn4qywgghiln4",
                            "fullsize": "https://cdn.bsky.app/img/feed_fullsize/plain/did:plc:ec72yg6n2sydzjvtovvdlxrk/bafkreierjvtgtcqt53o5rfrlahctdeiczekegsxpo5nyxhn4qywgghiln4",
                            "alt": "Long cat is long, sideways",
                            "aspectRatio": {
                                "height": 138,
                                "width": 198
                            },
                            "$type": "app.bsky.embed.gallery#viewImage"
                        },
                        {
                            "thumbnail": "https://cdn.bsky.app/img/feed_thumbnail/plain/did:plc:ec72yg6n2sydzjvtovvdlxrk/bafkreigfkwp7thnpirniwjombepq2iyuqqtauu6qazr3yeqgrd567d5vue",
                            "fullsize": "https://cdn.bsky.app/img/feed_fullsize/plain/did:plc:ec72yg6n2sydzjvtovvdlxrk/bafkreigfkwp7thnpirniwjombepq2iyuqqtauu6qazr3yeqgrd567d5vue",
                            "alt": "Long cat is long, sideways",
                            "aspectRatio": {
                                "height": 138,
                                "width": 164
                            },
                            "$type": "app.bsky.embed.gallery#viewImage"
                        }
                    ],
                    "$type": "app.bsky.embed.gallery#view"
                },
                "bookmarkCount": 0,
                "replyCount": 0,
                "repostCount": 0,
                "likeCount": 0,
                "quoteCount": 1,
                "indexedAt": "2026-06-11T13:58:59.170Z",
                "viewer": {
                    "bookmarked": false,
                    "threadMuted": false,
                    "embeddingDisabled": false,
                    "pinned": false
                },
                "labels": []
            }
            """;

        PostView? postView = JsonSerializer.Deserialize<PostView>(json, BlueskyJsonSerializerOptions.Options);
        Assert.NotNull(postView);
        Assert.IsType<Embed.Gallery.View>(postView.Embed);
    }
}