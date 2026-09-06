// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;

using idunno.AtProto;
using idunno.Bluesky.Unspecced;
using idunno.Bluesky.Unspecced.Model;

namespace idunno.Bluesky.Serialization.Test;

public class UnspeccedTests
{
    [Fact]
    public void TrendingTopicsWithoutDescriptionDeserializeCorrectly()
    {
        string json = """
            
            
            {
                "topics": [
                {
                    "topic": "Manchester Elections",
                    "link": "/profile/trending.bsky.app/feed/815157883"
                },
                {
                    "topic": "Horror Films",
                    "link": "/profile/trending.bsky.app/feed/815029726"
                },
                {
                    "topic": "B Movie Maniacs",
                    "link": "/profile/trending.bsky.app/feed/815012533"
                },
                {
                    "topic": "Kremer Trade",
                    "link": "/profile/trending.bsky.app/feed/814994662"
                },
                {
                    "topic": "FIFA",
                    "link": "/profile/trending.bsky.app/feed/814821759"
                },
                {
                    "topic": "Gianni Infantino",
                    "link": "/profile/trending.bsky.app/feed/814861202"
                },
                {
                    "topic": "Todd Blanche",
                    "link": "/profile/trending.bsky.app/feed/814798353"
                },
                {
                    "topic": "WWE",
                    "link": "/profile/trending.bsky.app/feed/814885958"
                },
                {
                    "topic": "Cinematography",
                    "link": "/profile/trending.bsky.app/feed/814866039"
                },
                {
                    "topic": "Art Fight",
                    "link": "/profile/trending.bsky.app/feed/814810276"
                }
            ],
            "suggested": [
                {
                    "topic": "Popular with Friends",
                    "link": "/profile/bsky.app/feed/with-friends"
                },
                {
                    "topic": "Quiet Posters",
                    "link": "/profile/why.bsky.team/feed/infreq"
                },
                {
                    "topic": "Sports",
                    "link": "/profile/crevier.bsky.social/feed/aaanstr6k5dvo"
                },
                {
                    "topic": "NFL",
                    "link": "/profile/parkermolloy.com/feed/aaai44jkavvrs"
                },
                {
                    "topic": "NBA",
                    "link": "/profile/davelevitan.bsky.social/feed/aaadvxju4txkk"
                },
                {
                    "topic": "WNBA",
                    "link": "/profile/trollhamels.bsky.social/feed/aaac3xufjdvjg"
                },
                {
                    "topic": "MLB",
                    "link": "/profile/parkermolloy.com/feed/aaap7dpu57ve6"
                },
                {
                    "topic": "NHL",
                    "link": "/profile/hockeyhotline.bsky.social/feed/aaacm5rbitxqa"
                },
                {
                    "topic": "Cats",
                    "link": "/profile/jaz.bsky.social/feed/cv:cat"
                },
                {
                    "topic": "Gardening",
                    "link": "/profile/eepy.bsky.social/feed/aaao6g552b33o"
                },
                {
                    "topic": "Dogs",
                    "link": "/profile/jaz.bsky.social/feed/cv:dog"
                },
                {
                    "topic": "Game Dev",
                    "link": "/profile/trezy.codes/feed/game-dev"
                },
                {
                    "topic": "Web Dev",
                    "link": "/profile/did:plc:m2sjv3wncvsasdapla35hzwj/feed/web-development"
                },
                {
                    "topic": "Video Games",
                    "link": "/profile/wyattswickedgoods.com/feed/aaaaieaxm5v3y"
                },
                {
                    "topic": "Anime",
                    "link": "/profile/anianimals.moe/feed/anime-en-new"
                },
                {
                    "topic": "Music",
                    "link": "/profile/cookieduh.xyz/feed/aaagw7oidihfs"
                },
                {
                    "topic": "Film & TV",
                    "link": "/profile/francesmeh.reviews/feed/aaaotdzmoni2q"
                },
                {
                    "topic": "Taylor Swift",
                    "link": "/profile/heheviolet.bsky.social/feed/aaakqsvp6kke4"
                },
                {
                    "topic": "Fashion",
                    "link": "/profile/sammyouatts.bsky.social/feed/aaacqhe34hlv6"
                },
                {
                    "topic": "Pop Culture",
                    "link": "/profile/nahuel.bsky.social/feed/aaae2qpt4236c"
                },
                {
                    "topic": "Fitness/Health",
                    "link": "/profile/sammyouatts.bsky.social/feed/aaadcogx3hvwc"
                },
                {
                    "topic": "Beauty",
                    "link": "/profile/abmuse.net/feed/aaac256qq7vh4"
                },
                {
                    "topic": "Science",
                    "link": "/profile/bossett.social/feed/for-science"
                },
                {
                    "topic": "Blacksky Trending",
                    "link": "/profile/rudyfraser.com/feed/blacksky-trend"
                }
            ]
        }
        """;

        GetTrendingTopicsResponse? trendingTopics = JsonSerializer.Deserialize<GetTrendingTopicsResponse>(json, BlueskyServer.BlueskyJsonSerializerOptions);

        Assert.NotNull(trendingTopics);
        Assert.NotNull(trendingTopics.Topics);
        Assert.NotNull(trendingTopics.Suggested);

        Assert.Equal(10, trendingTopics.Topics.Count);

        Assert.Equal("Manchester Elections", trendingTopics.Topics.ElementAt(0).Topic);
        Assert.Equal("/profile/trending.bsky.app/feed/815157883", trendingTopics.Topics.ElementAt(0).Link);
        Assert.Null(trendingTopics.Topics.ElementAt(0).Description);
        Assert.Null(trendingTopics.Topics.ElementAt(0).DisplayName);

        Assert.Equal(24, trendingTopics.Suggested.Count);
        Assert.Equal("Popular with Friends", trendingTopics.Suggested.ElementAt(0).Topic);
        Assert.Equal("/profile/bsky.app/feed/with-friends", trendingTopics.Suggested.ElementAt(0).Link);
        Assert.Null(trendingTopics.Suggested.ElementAt(0).Description);
        Assert.Null(trendingTopics.Suggested.ElementAt(0).DisplayName);
    }

    [Fact]
    public void TrendingTopicsWithDescriptionsAndDisplayNamesDeserializeCorrectly()
    {
        string json = """
            {
                "topics": [
                    {
                        "link": "/profile/did:plc:qrz3lhbyuxbeilrc6nekdqme/feed/8ed06fe41219",
                        "description": "Trump's DOJ admitted damage was caused by a contractor's flawed installation, not vandalism by ex-Olympian David Hearn.",
                        "topic": "DOJ drops Reflecting Pool charges",
                        "displayName": "DOJ drops Reflecting Pool charges"
                    },
                    {
                        "link": "/profile/did:plc:qrz3lhbyuxbeilrc6nekdqme/feed/5412a1f74176",
                        "description": "Spain says ~48,000 of 60,000 who crossed have gone back; at least 57 deaths reported.",
                        "topic": "Most Ceuta migrants return to Morocco",
                        "displayName": "Most Ceuta migrants return to Morocco"
                    },
                    {
                        "link": "/profile/did:plc:qrz3lhbyuxbeilrc6nekdqme/feed/30f1646be875",
                        "description": "Infantino reversed course after backlash from UEFA and others within FIFA itself.",
                        "topic": "FIFA scraps World Cup sell-off",
                        "displayName": "FIFA scraps World Cup sell-off"
                    },
                    {
                        "link": "/profile/did:plc:qrz3lhbyuxbeilrc6nekdqme/feed/97d665442b87",
                        "description": "Audience reactions are mixed; Matt Damon's casting divides viewers.",
                        "topic": "Nolan's The Odyssey released",
                        "displayName": "Nolan's The Odyssey released"
                    },
                    {
                        "link": "/profile/did:plc:qrz3lhbyuxbeilrc6nekdqme/feed/f8d735a7d51e",
                        "description": "Over 30 water systems were hit; no contamination reported, but some plants safely shut down.",
                        "topic": "Iran linked to Minnesota water cyberattacks",
                        "displayName": "Iran linked to Minnesota water cyberattacks"
                    },
                    {
                        "link": "/profile/did:plc:qrz3lhbyuxbeilrc6nekdqme/feed/ef70ff1ef2ab",
                        "description": "Over 70 missiles and 280 drones hit multiple cities; at least 8 killed, one missile crossed into Poland.",
                        "topic": "Russia strikes Ukraine with missiles",
                        "displayName": "Russia strikes Ukraine with missiles"
                    },
                    {
                        "link": "/profile/did:plc:qrz3lhbyuxbeilrc6nekdqme/feed/ae5c0c224448",
                        "description": "Work requirements and drug subsidy cuts are pushing millions off coverage, with costs rising in 2027.",
                        "topic": "Trump cuts Medicaid and Medicare",
                        "displayName": "Trump cuts Medicaid and Medicare"
                    },
                    {
                        "link": "/profile/did:plc:qrz3lhbyuxbeilrc6nekdqme/feed/13c71b95ff31",
                        "description": "Labour's Craig nearly doubled Reform UK's vote share, seen as a blow to Farage's party.",
                        "topic": "Bev Craig wins Greater Manchester mayor",
                        "displayName": "Bev Craig wins Greater Manchester mayor"
                    },
                    {
                        "link": "/profile/did:plc:qrz3lhbyuxbeilrc6nekdqme/feed/e024e6e6f115",
                        "description": "Critics dispute Hassett's jobs claims, citing water use, energy costs, and environmental harm.",
                        "topic": "Backlash against AI data centers",
                        "displayName": "Backlash against AI data centers"
                    },
                    {
                        "link": "/profile/did:plc:qrz3lhbyuxbeilrc6nekdqme/feed/21243a347717",
                        "description": "Artists are trading attacks and sharing creations in the annual summer art battle event.",
                        "topic": "ArtFight 2026",
                        "displayName": "ArtFight 2026"
                    }
                ],
                "suggested": [],
                "recIdStr": "761433928885927517"
            }
            """;

        GetTrendingTopicsResponse? trendingTopics = JsonSerializer.Deserialize<GetTrendingTopicsResponse>(json, BlueskyServer.BlueskyJsonSerializerOptions);

        Assert.NotNull(trendingTopics);
        Assert.NotNull(trendingTopics.Topics);
        Assert.NotNull(trendingTopics.Suggested);

        Assert.Equal(10, trendingTopics.Topics.Count);

        Assert.Equal("DOJ drops Reflecting Pool charges", trendingTopics.Topics.ElementAt(0).Topic);
        Assert.Equal("/profile/did:plc:qrz3lhbyuxbeilrc6nekdqme/feed/8ed06fe41219", trendingTopics.Topics.ElementAt(0).Link);
        Assert.Equal("Trump's DOJ admitted damage was caused by a contractor's flawed installation, not vandalism by ex-Olympian David Hearn.", trendingTopics.Topics.ElementAt(0).Description);
        Assert.Equal("DOJ drops Reflecting Pool charges", trendingTopics.Topics.ElementAt(0).DisplayName);

        Assert.Empty(trendingTopics.Suggested);
    }

    [Fact]
    public void GetPostThreadV2ResponseDeserializesCorrectly()
    {
        string json = """
            {
                "hasOtherReplies": false,
                "thread": [
                    {
                        "uri": "at://did:plc:hfgp6pj3akhqxntgqwramlbg/app.bsky.feed.post/3ms2foehqt22h",
                        "depth": 0,
                        "value": {
                            "post": {
                                "uri": "at://did:plc:hfgp6pj3akhqxntgqwramlbg/app.bsky.feed.post/3ms2foehqt22h",
                                "cid": "bafyreifqanje5olvnd6orpjsubxc4oy7pac23plmulicom7nrh6t6axgxi",
                                "author": {
                                    "did": "did:plc:hfgp6pj3akhqxntgqwramlbg",
                                    "handle": "blowdart.me",
                                    "displayName": "Barry Dorrans",
                                    "pronouns": "He/Him",
                                    "avatar": "https://cdn.bsky.app/img/avatar/plain/did:plc:hfgp6pj3akhqxntgqwramlbg/bafkreicwjaromkjs4jrd5uqznacfgzvhnob2il5fwywxqopbnhfb74n27m",
                                    "associated": {
                                        "chat": {
                                            "allowIncoming": "all",
                                            "allowGroupInvites": "following"
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
                                        "mutedOnlyReposts": false,
                                        "mutedOnlyQuoteposts": false,
                                        "blockedBy": false,
                                        "following": "at://did:plc:ec72yg6n2sydzjvtovvdlxrk/app.bsky.graph.follow/3kqxzemnnc425",
                                        "followedBy": "at://did:plc:hfgp6pj3akhqxntgqwramlbg/app.bsky.graph.follow/3ko2gwpx37l2m"
                                    },
                                    "labels": [],
                                    "createdAt": "2023-04-22T22:44:04.316Z"
                                },
                                "record": {
                                    "$type": "app.bsky.feed.post",
                                    "createdAt": "2026-08-01T21:22:56.693Z",
                                    "embed": {
                                        "$type": "app.bsky.embed.external",
                                        "external": {
                                            "description": "Co-authored-by: Claude Fable 5 <noreply@anthropic.com>",
                                            "thumb": {
                                                "$type": "blob",
                                                "ref": {
                                                    "$link": "bafkreieuoeks3sovdtn4lhdls55v3d73f7gxrs6cado5b3gvoc4prmixt4"
                                                },
                                                "mimeType": "image/jpeg",
                                                "size": 164395
                                            },
                                            "title": "Add repost and quotepost-only mutes (#5118) · bluesky-social/atproto@ee4a0cf",
                                            "uri": "https://github.com/bluesky-social/atproto/commit/ee4a0cf0ebb3e078a014bef882720f46aca5cb89#diff-9c7981b0c48bc6877c508c5537b4dd0655f5c487d85df2935a7976809a295298R202-R208"
                                        }
                                    },
                                    "facets": [
                                        {
                                            "features": [
                                                {
                                                    "$type": "app.bsky.richtext.facet#link",
                                                    "uri": "https://github.com/bluesky-social/atproto/commit/ee4a0cf0ebb3e078a014bef882720f46aca5cb89#diff-9c7981b0c48bc6877c508c5537b4dd0655f5c487d85df2935a7976809a295298R202-R208"
                                                }
                                            ],
                                            "index": {
                                                "byteEnd": 98,
                                                "byteStart": 72
                                            }
                                        }
                                    ],
                                    "langs": [
                                        "en"
                                    ],
                                    "text": "Looks like the ability to mute reposts and quote from people is coming\n\ngithub.com/bluesky-soci..."
                                },
                                "embed": {
                                    "external": {
                                        "uri": "https://github.com/bluesky-social/atproto/commit/ee4a0cf0ebb3e078a014bef882720f46aca5cb89#diff-9c7981b0c48bc6877c508c5537b4dd0655f5c487d85df2935a7976809a295298R202-R208",
                                        "title": "Add repost and quotepost-only mutes (#5118) · bluesky-social/atproto@ee4a0cf",
                                        "description": "Co-authored-by: Claude Fable 5 <noreply@anthropic.com>",
                                        "thumb": "https://cdn.bsky.app/img/feed_thumbnail/plain/did:plc:hfgp6pj3akhqxntgqwramlbg/bafkreieuoeks3sovdtn4lhdls55v3d73f7gxrs6cado5b3gvoc4prmixt4"
                                    },
                                    "$type": "app.bsky.embed.external#view"
                                },
                                "bookmarkCount": 1,
                                "replyCount": 2,
                                "repostCount": 7,
                                "likeCount": 39,
                                "quoteCount": 9,
                                "indexedAt": "2026-08-01T21:22:52.366Z",
                                "viewer": {
                                    "bookmarked": false,
                                    "threadMuted": false,
                                    "embeddingDisabled": false
                                },
                                "labels": []
                            },
                            "moreParents": false,
                            "moreReplies": 0,
                            "opThread": true,
                            "opThreadPostIndex" : 1,
                            "opThreadPostCount" : 2,
                            "hiddenByThreadgate": false,
                            "mutedByViewer": false,
                            "$type": "app.bsky.unspecced.defs#threadItemPost"
                        }
                    },
                    {
                        "uri": "at://did:plc:llyvrdjsnfuycjykeaopusbt/app.bsky.feed.post/3ms2g6c7r722l",
                        "depth": 1,
                        "value": {
                            "post": {
                                "uri": "at://did:plc:llyvrdjsnfuycjykeaopusbt/app.bsky.feed.post/3ms2g6c7r722l",
                                "cid": "bafyreigeot2ft3x3mruxd275bihgrb5v6q4kp4ljs2pe7xnq447wtay4uu",
                                "author": {
                                    "did": "did:plc:llyvrdjsnfuycjykeaopusbt",
                                    "handle": "pagnificent.myatproto.social",
                                    "displayName": "Stephanie goes by Stephanie",
                                    "avatar": "https://cdn.bsky.app/img/avatar/plain/did:plc:llyvrdjsnfuycjykeaopusbt/bafkreiezcga3rmddmszeqa43qkhqe3og62d2uxbx3olmab3luu7ehj47ce",
                                    "associated": {
                                        "chat": {
                                            "allowIncoming": "none"
                                        },
                                        "activitySubscription": {
                                            "allowSubscriptions": "followers"
                                        }
                                    },
                                    "viewer": {
                                        "muted": false,
                                        "mutedOnlyReposts": false,
                                        "mutedOnlyQuoteposts": false,
                                        "blockedBy": false
                                    },
                                    "labels": [
                                        {
                                            "src": "did:plc:llyvrdjsnfuycjykeaopusbt",
                                            "uri": "at://did:plc:llyvrdjsnfuycjykeaopusbt/app.bsky.actor.profile/self",
                                            "cid": "bafyreiceprvaqsclm2ixmlqljamba5xx7q3bkt2ndxmyt5rpz6ibtjeq64",
                                            "val": "!no-unauthenticated",
                                            "cts": "1970-01-01T00:00:00.000Z"
                                        }
                                    ],
                                    "createdAt": "2023-05-26T23:17:46.837Z"
                                },
                                "record": {
                                    "$type": "app.bsky.feed.post",
                                    "createdAt": "2026-08-01T21:31:51.204Z",
                                    "facets": [
                                        {
                                            "$type": "app.bsky.richtext.facet",
                                            "features": [
                                                {
                                                    "$type": "app.bsky.richtext.facet#mention",
                                                    "did": "did:plc:3jpt2mvvsumj2r7eqk4gzzjz"
                                                }
                                            ],
                                            "index": {
                                                "byteEnd": 38,
                                                "byteStart": 30
                                            }
                                        }
                                    ],
                                    "langs": [
                                        "en"
                                    ],
                                    "reply": {
                                        "parent": {
                                            "cid": "bafyreifqanje5olvnd6orpjsubxc4oy7pac23plmulicom7nrh6t6axgxi",
                                            "uri": "at://did:plc:hfgp6pj3akhqxntgqwramlbg/app.bsky.feed.post/3ms2foehqt22h"
                                        },
                                        "root": {
                                            "cid": "bafyreifqanje5olvnd6orpjsubxc4oy7pac23plmulicom7nrh6t6axgxi",
                                            "uri": "at://did:plc:hfgp6pj3akhqxntgqwramlbg/app.bsky.feed.post/3ms2foehqt22h"
                                        }
                                    },
                                    "text": "BLESSED BE MY LORD AND SAVIOR @esb.lol"
                                },
                                "bookmarkCount": 0,
                                "replyCount": 1,
                                "repostCount": 0,
                                "likeCount": 8,
                                "quoteCount": 0,
                                "indexedAt": "2026-08-01T21:31:52.464Z",
                                "viewer": {
                                    "bookmarked": false,
                                    "threadMuted": false,
                                    "embeddingDisabled": false
                                },
                                "labels": []
                            },
                            "moreParents": false,
                            "moreReplies": 0,
                            "opThread": false,
                            "hiddenByThreadgate": false,
                            "mutedByViewer": false,
                            "$type": "app.bsky.unspecced.defs#threadItemPost"
                        }
                    },
                    {
                        "uri": "at://did:plc:3jpt2mvvsumj2r7eqk4gzzjz/app.bsky.feed.post/3ms2qnmwyuk26",
                        "depth": 2,
                        "value": {
                            "post": {
                                "uri": "at://did:plc:3jpt2mvvsumj2r7eqk4gzzjz/app.bsky.feed.post/3ms2qnmwyuk26",
                                "cid": "bafyreidzbv2rfecazdjpyrrpoxvpix74n5qboukf4kw6vdv6xaw7cpju3e",
                                "author": {
                                    "did": "did:plc:3jpt2mvvsumj2r7eqk4gzzjz",
                                    "handle": "esb.lol",
                                    "displayName": "Eric",
                                    "avatar": "https://cdn.bsky.app/img/avatar/plain/did:plc:3jpt2mvvsumj2r7eqk4gzzjz/bafkreicmpkhaggfoagqj53jmzfmremae2qcw7oc3ugh5qvq6quncxp6wqq",
                                    "associated": {
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
                                        "mutedOnlyReposts": false,
                                        "mutedOnlyQuoteposts": false,
                                        "blockedBy": false
                                    },
                                    "labels": [],
                                    "createdAt": "2023-05-02T03:39:27.863Z",
                                    "verification": {
                                        "verifications": [
                                            {
                                                "issuer": "did:plc:z72i7hdynmk6r22z27h6tvur",
                                                "issuerDisplayName": "Bluesky",
                                                "issuerHandle": "bsky.app",
                                                "uri": "at://did:plc:z72i7hdynmk6r22z27h6tvur/app.bsky.graph.verification/3ltmvuidftv2f",
                                                "isValid": true,
                                                "createdAt": "2025-07-10T17:59:36.528Z"
                                            }
                                        ],
                                        "verifiedStatus": "valid",
                                        "trustedVerifierStatus": "none"
                                    }
                                },
                                "record": {
                                    "$type": "app.bsky.feed.post",
                                    "createdAt": "2026-08-02T00:39:23.187Z",
                                    "langs": [
                                        "en"
                                    ],
                                    "reply": {
                                        "parent": {
                                            "cid": "bafyreigeot2ft3x3mruxd275bihgrb5v6q4kp4ljs2pe7xnq447wtay4uu",
                                            "uri": "at://did:plc:llyvrdjsnfuycjykeaopusbt/app.bsky.feed.post/3ms2g6c7r722l"
                                        },
                                        "root": {
                                            "cid": "bafyreifqanje5olvnd6orpjsubxc4oy7pac23plmulicom7nrh6t6axgxi",
                                            "uri": "at://did:plc:hfgp6pj3akhqxntgqwramlbg/app.bsky.feed.post/3ms2foehqt22h"
                                        }
                                    },
                                    "text": "🫡"
                                },
                                "bookmarkCount": 0,
                                "replyCount": 2,
                                "repostCount": 0,
                                "likeCount": 8,
                                "quoteCount": 0,
                                "indexedAt": "2026-08-02T00:39:23.573Z",
                                "viewer": {
                                    "bookmarked": false,
                                    "threadMuted": false,
                                    "embeddingDisabled": false
                                },
                                "labels": []
                            },
                            "moreParents": false,
                            "moreReplies": 0,
                            "opThread": false,
                            "hiddenByThreadgate": false,
                            "mutedByViewer": false,
                            "$type": "app.bsky.unspecced.defs#threadItemPost"
                        }
                    },
                    {
                        "uri": "at://did:plc:hfgp6pj3akhqxntgqwramlbg/app.bsky.feed.post/3ms3tzqhmec2e",
                        "depth": 3,
                        "value": {
                            "post": {
                                "uri": "at://did:plc:hfgp6pj3akhqxntgqwramlbg/app.bsky.feed.post/3ms3tzqhmec2e",
                                "cid": "bafyreidaizuww2xviaxrbe53bdyggrrpo7agdyiirqffikpnwfjsy4jvbi",
                                "author": {
                                    "did": "did:plc:hfgp6pj3akhqxntgqwramlbg",
                                    "handle": "blowdart.me",
                                    "displayName": "Barry Dorrans",
                                    "pronouns": "He/Him",
                                    "avatar": "https://cdn.bsky.app/img/avatar/plain/did:plc:hfgp6pj3akhqxntgqwramlbg/bafkreicwjaromkjs4jrd5uqznacfgzvhnob2il5fwywxqopbnhfb74n27m",
                                    "associated": {
                                        "chat": {
                                            "allowIncoming": "all",
                                            "allowGroupInvites": "following"
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
                                        "mutedOnlyReposts": false,
                                        "mutedOnlyQuoteposts": false,
                                        "blockedBy": false,
                                        "following": "at://did:plc:ec72yg6n2sydzjvtovvdlxrk/app.bsky.graph.follow/3kqxzemnnc425",
                                        "followedBy": "at://did:plc:hfgp6pj3akhqxntgqwramlbg/app.bsky.graph.follow/3ko2gwpx37l2m"
                                    },
                                    "labels": [],
                                    "createdAt": "2023-04-22T22:44:04.316Z"
                                },
                                "record": {
                                    "$type": "app.bsky.feed.post",
                                    "createdAt": "2026-08-02T11:12:30.494Z",
                                    "langs": [
                                        "en"
                                    ],
                                    "reply": {
                                        "parent": {
                                            "cid": "bafyreidzbv2rfecazdjpyrrpoxvpix74n5qboukf4kw6vdv6xaw7cpju3e",
                                            "uri": "at://did:plc:3jpt2mvvsumj2r7eqk4gzzjz/app.bsky.feed.post/3ms2qnmwyuk26"
                                        },
                                        "root": {
                                            "cid": "bafyreifqanje5olvnd6orpjsubxc4oy7pac23plmulicom7nrh6t6axgxi",
                                            "uri": "at://did:plc:hfgp6pj3akhqxntgqwramlbg/app.bsky.feed.post/3ms2foehqt22h"
                                        }
                                    },
                                    "text": "Turn it on already, I want to test my api wrapper 😂"
                                },
                                "bookmarkCount": 0,
                                "replyCount": 0,
                                "repostCount": 0,
                                "likeCount": 1,
                                "quoteCount": 0,
                                "indexedAt": "2026-08-02T11:12:30.870Z",
                                "viewer": {
                                    "bookmarked": false,
                                    "threadMuted": false,
                                    "embeddingDisabled": false
                                },
                                "labels": []
                            },
                            "moreParents": false,
                            "moreReplies": 0,
                            "opThread": false,
                            "hiddenByThreadgate": false,
                            "mutedByViewer": false,
                            "$type": "app.bsky.unspecced.defs#threadItemPost"
                        }
                    },
                    {
                        "uri": "at://did:plc:llyvrdjsnfuycjykeaopusbt/app.bsky.feed.post/3ms2swvcb3c2l",
                        "depth": 3,
                        "value": {
                            "post": {
                                "uri": "at://did:plc:llyvrdjsnfuycjykeaopusbt/app.bsky.feed.post/3ms2swvcb3c2l",
                                "cid": "bafyreiediw2ckci4mjquqnojpi6acaj3tb7u2cxy6cusbnzevrjiyd3vta",
                                "author": {
                                    "did": "did:plc:llyvrdjsnfuycjykeaopusbt",
                                    "handle": "pagnificent.myatproto.social",
                                    "displayName": "Stephanie goes by Stephanie",
                                    "avatar": "https://cdn.bsky.app/img/avatar/plain/did:plc:llyvrdjsnfuycjykeaopusbt/bafkreiezcga3rmddmszeqa43qkhqe3og62d2uxbx3olmab3luu7ehj47ce",
                                    "associated": {
                                        "chat": {
                                            "allowIncoming": "none"
                                        },
                                        "activitySubscription": {
                                            "allowSubscriptions": "followers"
                                        }
                                    },
                                    "viewer": {
                                        "muted": false,
                                        "mutedOnlyReposts": false,
                                        "mutedOnlyQuoteposts": false,
                                        "blockedBy": false
                                    },
                                    "labels": [
                                        {
                                            "src": "did:plc:llyvrdjsnfuycjykeaopusbt",
                                            "uri": "at://did:plc:llyvrdjsnfuycjykeaopusbt/app.bsky.actor.profile/self",
                                            "cid": "bafyreiceprvaqsclm2ixmlqljamba5xx7q3bkt2ndxmyt5rpz6ibtjeq64",
                                            "val": "!no-unauthenticated",
                                            "cts": "1970-01-01T00:00:00.000Z"
                                        }
                                    ],
                                    "createdAt": "2023-05-26T23:17:46.837Z"
                                },
                                "record": {
                                    "$type": "app.bsky.feed.post",
                                    "createdAt": "2026-08-02T01:20:21.418Z",
                                    "langs": [
                                        "en"
                                    ],
                                    "reply": {
                                        "parent": {
                                            "cid": "bafyreidzbv2rfecazdjpyrrpoxvpix74n5qboukf4kw6vdv6xaw7cpju3e",
                                            "uri": "at://did:plc:3jpt2mvvsumj2r7eqk4gzzjz/app.bsky.feed.post/3ms2qnmwyuk26"
                                        },
                                        "root": {
                                            "cid": "bafyreifqanje5olvnd6orpjsubxc4oy7pac23plmulicom7nrh6t6axgxi",
                                            "uri": "at://did:plc:hfgp6pj3akhqxntgqwramlbg/app.bsky.feed.post/3ms2foehqt22h"
                                        }
                                    },
                                    "text": "The most I’ve ever loved you"
                                },
                                "bookmarkCount": 0,
                                "replyCount": 0,
                                "repostCount": 0,
                                "likeCount": 1,
                                "quoteCount": 0,
                                "indexedAt": "2026-08-02T01:20:22.471Z",
                                "viewer": {
                                    "bookmarked": false,
                                    "threadMuted": false,
                                    "embeddingDisabled": false
                                },
                                "labels": []
                            },
                            "moreParents": false,
                            "moreReplies": 0,
                            "opThread": false,
                            "hiddenByThreadgate": false,
                            "mutedByViewer": false,
                            "$type": "app.bsky.unspecced.defs#threadItemPost"
                        }
                    },
                    {
                        "uri": "at://did:plc:vvmzsvy52bgafdieysktpibb/app.bsky.feed.post/3ms3aq3gksk2f",
                        "depth": 1,
                        "value": {
                            "post": {
                                "uri": "at://did:plc:vvmzsvy52bgafdieysktpibb/app.bsky.feed.post/3ms3aq3gksk2f",
                                "cid": "bafyreicb5zmmpi3q5qvvex3bqkcbgt2kvk2tetg2kwy5t7yngcckv3grce",
                                "author": {
                                    "did": "did:plc:vvmzsvy52bgafdieysktpibb",
                                    "handle": "thewisenerd.com",
                                    "displayName": "trivial_inanity",
                                    "avatar": "https://cdn.bsky.app/img/avatar/plain/did:plc:vvmzsvy52bgafdieysktpibb/bafkreihqxhqwgv7yn6zflyt2i6kc6ltcsyast3ihzww7jblix5bvlws4hy",
                                    "associated": {
                                        "chat": {
                                            "allowIncoming": "none"
                                        },
                                        "activitySubscription": {
                                            "allowSubscriptions": "followers"
                                        }
                                    },
                                    "viewer": {
                                        "muted": false,
                                        "mutedOnlyReposts": false,
                                        "mutedOnlyQuoteposts": false,
                                        "blockedBy": false
                                    },
                                    "labels": [],
                                    "createdAt": "2024-11-18T07:33:50.946Z"
                                },
                                "record": {
                                    "$type": "app.bsky.feed.post",
                                    "createdAt": "2026-08-02T05:27:05.355Z",
                                    "langs": [
                                        "en"
                                    ],
                                    "reply": {
                                        "parent": {
                                            "cid": "bafyreifqanje5olvnd6orpjsubxc4oy7pac23plmulicom7nrh6t6axgxi",
                                            "uri": "at://did:plc:hfgp6pj3akhqxntgqwramlbg/app.bsky.feed.post/3ms2foehqt22h"
                                        },
                                        "root": {
                                            "cid": "bafyreifqanje5olvnd6orpjsubxc4oy7pac23plmulicom7nrh6t6axgxi",
                                            "uri": "at://did:plc:hfgp6pj3akhqxntgqwramlbg/app.bsky.feed.post/3ms2foehqt22h"
                                        }
                                    },
                                    "text": "the day i return to \"following\" instead of \"only posts\" is near"
                                },
                                "bookmarkCount": 0,
                                "replyCount": 0,
                                "repostCount": 0,
                                "likeCount": 1,
                                "quoteCount": 0,
                                "indexedAt": "2026-08-02T05:27:07.168Z",
                                "viewer": {
                                    "bookmarked": false,
                                    "threadMuted": false,
                                    "embeddingDisabled": false
                                },
                                "labels": []
                            },
                            "moreParents": false,
                            "moreReplies": 0,
                            "opThread": false,
                            "hiddenByThreadgate": false,
                            "mutedByViewer": false,
                            "$type": "app.bsky.unspecced.defs#threadItemPost"
                        }
                    }
                ]
            }
            
            """;

        GetPostThreadV2Response? actual = JsonSerializer.Deserialize<GetPostThreadV2Response>(json, BlueskyServer.BlueskyJsonSerializerOptions);

        Assert.NotNull(actual);
        Assert.False(actual.HasOtherReplies);
        Assert.Null(actual.Threadgate);
        Assert.Equal(6, actual.Thread.Count);

        Assert.Equal(new AtUri("at://did:plc:hfgp6pj3akhqxntgqwramlbg/app.bsky.feed.post/3ms2foehqt22h"), actual.Thread.ElementAt(0).Uri);
        Assert.Equal(0, actual.Thread.ElementAt(0).Depth);
        Assert.IsType<ThreadItemPost>(actual.Thread.ElementAt(0).Value);

        ThreadItemPost post0 = (ThreadItemPost)actual.Thread.ElementAt(0).Value;
        Assert.False(post0.MoreParents);
        Assert.Equal(0, post0.MoreReplies);
        Assert.True(post0.OpThread);
        Assert.False(post0.HiddenByThreadGate);
        Assert.False(post0.MutedByViewer);
        Assert.Equal(1, post0.OpThreadPostIndex);
        Assert.Equal(2, post0.OpThreadPostCount);
    }
}
