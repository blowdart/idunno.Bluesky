// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;

using idunno.Bluesky.Graph;
using idunno.Bluesky.Graph.Model;
using idunno.Bluesky.Record;

namespace idunno.Bluesky.Serialization.Test;

[ExcludeFromCodeCoverage]
public class ListTests
{
    [Fact]
    public void BlueskyListDeserializesCorrectlyWithSourceGeneratedJsonContext()
    {
        string json = """
            {
                "name": "Super Bean Fans",
                "$type": "app.bsky.graph.list",
                "purpose": "app.bsky.graph.defs#curatelist",
                "createdAt": "2025-05-01T03:50:05.7760765+00:00",
                "description": "People who realise the glory of Heinz Baked Beans.",
                "descriptionFacets": []
            }
            """;

        BlueskyList? actual = JsonSerializer.Deserialize<BlueskyList>(json, BlueskyServer.BlueskyJsonSerializerOptions);

        Assert.NotNull(actual);
        Assert.Equal("Super Bean Fans", actual.Name);
        Assert.Equal(ListPurpose.CurateList, actual.Purpose);
    }

    [Fact]
    public void GetListResponseDeserializesCorrectlyWithSourceGeneratedJsonContext()
    {
        string json = """
            {
                "list": {
                    "uri": "at://did:plc:hgrpyurhswze7rt7z2smeu2q/app.bsky.graph.list/3mcdzw2ptry2m",
                    "cid": "bafyreihszwqbe7nxh2mv4o2wk365yanh3p3zimbunfrjdtq7gcmmzohx6i",
                    "name": "People with awesome Starter Packs 10",
                    "purpose": "app.bsky.graph.defs#referencelist",
                    "listItemCount": 102,
                    "indexedAt": "2026-01-14T02:36:01.535Z",
                    "labels": [],
                    "viewer": {
                        "muted": false
                    },
                    "creator": {
                        "did": "did:plc:hgrpyurhswze7rt7z2smeu2q",
                        "handle": "adventurer56.bsky.social",
                        "displayName": "@Adventurer56 - Don’t Major in Minor Things!",
                        "avatar": "https://cdn.bsky.app/img/avatar/plain/did:plc:hgrpyurhswze7rt7z2smeu2q/bafkreibolysg7oocsydjf6smucasfmwlb2oebf36ds4iaanrnyrh27iuo4@jpeg",
                        "associated": {
                            "chat": {
                                "allowIncoming": "following"
                            },
                            "activitySubscription": {
                                "allowSubscriptions": "followers"
                            }
                        },
                        "viewer": {
                            "muted": false,
                            "blockedBy": false,
                            "following": "at://did:plc:jr67biy54ndfhbarhocxpl77/app.bsky.graph.follow/3llro6jl4ov2q",
                            "followedBy": "at://did:plc:hgrpyurhswze7rt7z2smeu2q/app.bsky.graph.follow/3llsbu7qyet2j"
                        },
                        "labels": [],
                        "createdAt": "2024-07-08T01:23:22.679Z",
                        "description": "USMC & Gov. Ret. Immigrant in Japan\n\nPROBLEM: #BillionairesClassWarfareAgainstUs\nhttps://youtu.be/8K6-cEAJZlE?si=66SwmDL5HmuD8WAn\n \n#SlavaUkraini\n\n#UniversalHealthCare\n\n#EqualRightsAndJustice4All\n\n#ReleaseUnredactedEpsteinFilesNow",
                        "indexedAt": "2026-01-29T03:55:21.745Z"
                    }
                },
                "items": [
                    {
                        "uri": "at://did:plc:hgrpyurhswze7rt7z2smeu2q/app.bsky.graph.listitem/3mdozvtu5sp26",
                        "subject": {
                            "did": "did:plc:5ldqhnk4quyil4mie2yjg2po",
                            "handle": "habsmtlcanadiens.bsky.social",
                            "displayName": "Frank 🇨🇦",
                            "avatar": "https://cdn.bsky.app/img/avatar/plain/did:plc:5ldqhnk4quyil4mie2yjg2po/bafkreihqedf7schqeqqrp2xvs4pw6rqkpycljkqqoq3ilalfzybkywwflu@jpeg",
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
                                "blockedBy": false,
                                "following": "at://did:plc:jr67biy54ndfhbarhocxpl77/app.bsky.graph.follow/3ltigy37bgh27",
                                "followedBy": "at://did:plc:5ldqhnk4quyil4mie2yjg2po/app.bsky.graph.follow/3ltkgap5pvg2r"
                            },
                            "labels": [
                                {
                                    "cts": "2025-09-30T10:45:42.982Z",
                                    "src": "did:plc:e4elbtctnfqocyfcml6h2lf7",
                                    "uri": "did:plc:5ldqhnk4quyil4mie2yjg2po",
                                    "val": "follow-farming",
                                    "ver": 1
                                },
                                {
                                    "src": "did:plc:5ldqhnk4quyil4mie2yjg2po",
                                    "uri": "at://did:plc:5ldqhnk4quyil4mie2yjg2po/app.bsky.actor.profile/self",
                                    "cid": "bafyreige7ztqav2ff6rey7q5zuq7avvmjhgxccsiuwcg4jtpa56et7u65y",
                                    "val": "!no-unauthenticated",
                                    "cts": "2024-02-11T18:35:34.611Z"
                                }
                            ],
                            "createdAt": "2024-02-11T18:35:34.027Z",
                            "description": "#CANADA 🇨🇦\n#CANADASTRONG\n#CANADIAN\n#NEVER51\n#ELBOWSUP\nSAFE SPACE #LGBTQ+ 🏳️‍🌈🏳️‍⚧️\n#BLM\nNO DM \n#STANDWITHUKRAINE 🇺🇦",
                            "indexedAt": "2026-01-31T12:39:34.645Z"
                        }
                    }
                ],
                "cursor": "3mdozvtu5sp26"
            }
            """;

        GetListResponse? actual = JsonSerializer.Deserialize<GetListResponse>(json, BlueskyServer.BlueskyJsonSerializerOptions);
        Assert.NotNull(actual);
    }

    [Fact]
    public void GetListsWithMembershipResponseDeserializesCorrectlyWithSourceGeneratedJsonContext()
    {
        string json = """
            {
                "listsWithMembership": [
                    {
                        "list": {
                            "uri": "at://did:plc:ec72yg6n2sydzjvtovvdlxrk/app.bsky.graph.list/3lnxvrbetf32n",
                            "cid": "bafyreidjq6nxblwalpk7ua57777y4jgpu5lusoobhwv4g7v4n4rf3tepp4",
                            "name": "Test moderation list",
                            "purpose": "app.bsky.graph.defs#modlist",
                            "listItemCount": 3,
                            "indexedAt": "2025-04-29T18:25:56.288Z",
                            "labels": [],
                            "viewer": {
                                "muted": false
                            },
                            "creator": {
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
                                "createdAt": "2024-03-19T13:00:19.046Z",
                                "description": "idunno.Bluesky Test Bot!",
                                "indexedAt": "2026-03-20T00:56:57.255Z"
                            },
                            "description": "Test moderation list description"
                        },
                        "listItem": {
                            "uri": "at://did:plc:ec72yg6n2sydzjvtovvdlxrk/app.bsky.graph.listitem/3lnymbiftjm2r",
                            "subject": {
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
                                "createdAt": "2023-04-22T22:44:04.316Z",
                                "description": "Security Curmudgeon for Microsoft .NET\n\nDo you really think work wants my social media opinions?\n\nNot nice, but kind - @medus4.com\n\n🇮🇪 🇬🇧 🇺🇸 ",
                                "indexedAt": "2026-03-09T16:31:29.647Z"
                            }
                        }
                    }
                ]
            }
            """;

        GetListsWithMembershipResponse? actual = JsonSerializer.Deserialize<GetListsWithMembershipResponse>(json, BlueskyServer.BlueskyJsonSerializerOptions);
        Assert.NotNull(actual);
    }
}
