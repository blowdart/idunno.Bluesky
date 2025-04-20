// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;

using idunno.Bluesky.Labeler;

namespace idunno.Bluesky.Serialization.Test
{
    [ExcludeFromCodeCoverage]
    public class LabelerTests
    {
        [Fact]
        public void LabelerViewDetailedDeserializesCorrectlyWithTypeResolver()
        {
            string json = """
                {
                    "uri": "at://did:plc:ar7c4by46qjdydhdevvrndac/app.bsky.labeler.service/self",
                    "cid": "bafyreigu3v6uxtmx3rv7emh4yvuvz2tom7ib7a4yl3x2eimard2brqeaei",
                    "creator": {
                        "did": "did:plc:ar7c4by46qjdydhdevvrndac",
                        "handle": "moderation.bsky.app",
                        "displayName": "Bluesky Moderation Service",
                        "associated": {
                            "labeler": true
                        },
                        "viewer": {
                            "muted": false,
                            "blockedBy": false
                        },
                        "labels": [],
                        "createdAt": "2023-04-11T17:29:51.242Z",
                        "description": "Official Bluesky moderation service. https://bsky.social/about/support/community-guidelines",
                        "indexedAt": "2024-03-19T17:14:40.208Z"
                    },
                    "likeCount": 8,
                    "viewer": {},
                    "indexedAt": "2024-03-13T15:52:53.522Z",
                    "labels": [],
                    "policies": {
                        "labelValueDefinitions": [
                            {
                                "adultOnly": false,
                                "blurs": "content",
                                "defaultSetting": "hide",
                                "identifier": "spam",
                                "locales": [
                                    {
                                        "description": "Unwanted, repeated, or unrelated actions that bother users.",
                                        "lang": "en",
                                        "name": "Spam"
                                    }
                                ],
                                "severity": "inform"
                            },
                            {
                                "adultOnly": false,
                                "blurs": "none",
                                "defaultSetting": "hide",
                                "identifier": "impersonation",
                                "locales": [
                                    {
                                        "description": "Pretending to be someone else without permission.",
                                        "lang": "en",
                                        "name": "Impersonation"
                                    }
                                ],
                                "severity": "inform"
                            },
                            {
                                "adultOnly": false,
                                "blurs": "content",
                                "defaultSetting": "hide",
                                "identifier": "scam",
                                "locales": [
                                    {
                                        "description": "Scams, phishing & fraud.",
                                        "lang": "en",
                                        "name": "Scam"
                                    }
                                ],
                                "severity": "alert"
                            },
                            {
                                "adultOnly": false,
                                "blurs": "content",
                                "defaultSetting": "warn",
                                "identifier": "intolerant",
                                "locales": [
                                    {
                                        "description": "Discrimination against protected groups.",
                                        "lang": "en",
                                        "name": "Intolerance"
                                    }
                                ],
                                "severity": "alert"
                            },
                            {
                                "adultOnly": false,
                                "blurs": "content",
                                "defaultSetting": "warn",
                                "identifier": "self-harm",
                                "locales": [
                                    {
                                        "description": "Promotes self-harm, including graphic images, glorifying discussions, or triggering stories.",
                                        "lang": "en",
                                        "name": "Self-Harm"
                                    }
                                ],
                                "severity": "alert"
                            },
                            {
                                "adultOnly": false,
                                "blurs": "content",
                                "defaultSetting": "hide",
                                "identifier": "security",
                                "locales": [
                                    {
                                        "description": "May be unsafe and could harm your device, steal your info, or get your account hacked.",
                                        "lang": "en",
                                        "name": "Security Concerns"
                                    }
                                ],
                                "severity": "alert"
                            },
                            {
                                "adultOnly": false,
                                "blurs": "content",
                                "defaultSetting": "warn",
                                "identifier": "misleading",
                                "locales": [
                                    {
                                        "description": "Altered images/videos, deceptive links, or false statements.",
                                        "lang": "en",
                                        "name": "Misleading"
                                    }
                                ],
                                "severity": "alert"
                            },
                            {
                                "adultOnly": false,
                                "blurs": "content",
                                "defaultSetting": "hide",
                                "identifier": "threat",
                                "locales": [
                                    {
                                        "description": "Promotes violence or harm towards others, including threats, incitement, or advocacy of harm.",
                                        "lang": "en",
                                        "name": "Threats"
                                    }
                                ],
                                "severity": "inform"
                            },
                            {
                                "adultOnly": false,
                                "blurs": "content",
                                "defaultSetting": "hide",
                                "identifier": "unsafe-link",
                                "locales": [
                                    {
                                        "description": "Links to harmful sites with malware, phishing, or violating content that risk security and privacy.",
                                        "lang": "en",
                                        "name": "Unsafe link"
                                    }
                                ],
                                "severity": "alert"
                            },
                            {
                                "adultOnly": false,
                                "blurs": "content",
                                "defaultSetting": "hide",
                                "identifier": "illicit",
                                "locales": [
                                    {
                                        "description": "Promoting or selling potentially illicit goods, services, or activities.",
                                        "lang": "en",
                                        "name": "Illicit"
                                    }
                                ],
                                "severity": "alert"
                            },
                            {
                                "adultOnly": false,
                                "blurs": "content",
                                "defaultSetting": "warn",
                                "identifier": "misinformation",
                                "locales": [
                                    {
                                        "description": "Spreading false or misleading info, including unverified claims and harmful conspiracy theories.",
                                        "lang": "en",
                                        "name": "Misinformation"
                                    }
                                ],
                                "severity": "inform"
                            },
                            {
                                "adultOnly": false,
                                "blurs": "none",
                                "defaultSetting": "warn",
                                "identifier": "rumor",
                                "locales": [
                                    {
                                        "description": "This claim has not been confirmed by a credible source yet.",
                                        "lang": "en",
                                        "name": "Unconfirmed"
                                    }
                                ],
                                "severity": "inform"
                            },
                            {
                                "adultOnly": false,
                                "blurs": "content",
                                "defaultSetting": "hide",
                                "identifier": "rude",
                                "locales": [
                                    {
                                        "description": "Rude or impolite, including crude language and disrespectful comments, without constructive purpose.",
                                        "lang": "en",
                                        "name": "Rude"
                                    }
                                ],
                                "severity": "inform"
                            },
                            {
                                "adultOnly": false,
                                "blurs": "content",
                                "defaultSetting": "hide",
                                "identifier": "extremist",
                                "locales": [
                                    {
                                        "description": "Radical views advocating violence, hate, or discrimination against individuals or groups.",
                                        "lang": "en",
                                        "name": "Extremist"
                                    }
                                ],
                                "severity": "alert"
                            },
                            {
                                "adultOnly": false,
                                "blurs": "content",
                                "defaultSetting": "warn",
                                "identifier": "sensitive",
                                "locales": [
                                    {
                                        "description": "May be upsetting, covering topics like substance abuse or mental health issues, cautioning sensitive viewers.",
                                        "lang": "en",
                                        "name": "Sensitive"
                                    }
                                ],
                                "severity": "alert"
                            },
                            {
                                "adultOnly": false,
                                "blurs": "content",
                                "defaultSetting": "hide",
                                "identifier": "engagement-farming",
                                "locales": [
                                    {
                                        "description": "Insincere content or bulk actions aimed at gaining followers, including frequent follows, posts, and likes.",
                                        "lang": "en",
                                        "name": "Engagement Farming"
                                    }
                                ],
                                "severity": "alert"
                            },
                            {
                                "adultOnly": false,
                                "blurs": "content",
                                "defaultSetting": "hide",
                                "identifier": "inauthentic",
                                "locales": [
                                    {
                                        "description": "Bot or a person pretending to be someone else.",
                                        "lang": "en",
                                        "name": "Inauthentic Account"
                                    }
                                ],
                                "severity": "alert"
                            },
                            {
                                "adultOnly": true,
                                "blurs": "media",
                                "defaultSetting": "show",
                                "identifier": "sexual-figurative",
                                "locales": [
                                    {
                                        "description": "Art with explicit or suggestive sexual themes, including provocative imagery or partial nudity.",
                                        "lang": "en",
                                        "name": "Sexually Suggestive (Cartoon)"
                                    }
                                ],
                                "severity": "none"
                            }
                        ],
                        "labelValues": [
                            "!hide",
                            "!warn",
                            "porn",
                            "sexual",
                            "nudity",
                            "sexual-figurative",
                            "graphic-media",
                            "self-harm",
                            "sensitive",
                            "extremist",
                            "intolerant",
                            "threat",
                            "rude",
                            "illicit",
                            "security",
                            "unsafe-link",
                            "impersonation",
                            "misinformation",
                            "scam",
                            "engagement-farming",
                            "spam",
                            "rumor",
                            "misleading",
                            "inauthentic"
                        ]
                    },
                    "$type": "app.bsky.labeler.defs#labelerViewDetailed"
                }
                """;

            LabelerView? labelerView = JsonSerializer.Deserialize<LabelerView>(json, BlueskyServer.BlueskyJsonSerializerOptions);

            Assert.NotNull(labelerView);
            Assert.Equal("at://did:plc:ar7c4by46qjdydhdevvrndac/app.bsky.labeler.service/self", labelerView.Uri);
            Assert.Equal("bafyreigu3v6uxtmx3rv7emh4yvuvz2tom7ib7a4yl3x2eimard2brqeaei", labelerView.Cid);
            Assert.Equal("did:plc:ar7c4by46qjdydhdevvrndac", labelerView.Creator.Did);
            Assert.Equal("moderation.bsky.app", labelerView.Creator.Handle);
            Assert.Equal("Bluesky Moderation Service", labelerView.Creator.DisplayName);
            Assert.NotNull(labelerView.Creator.Associated);
            Assert.True(labelerView.Creator.Associated.Labeler);

            Assert.IsType<LabelerViewDetailed>(labelerView);

            LabelerViewDetailed labelerViewDetailed = (LabelerViewDetailed)labelerView;

            Assert.NotEmpty(labelerViewDetailed.Policies.LabelValueDefinitions);

            //LabelValueDefinition spam = labelerViewDetailed.Policies.LabelValueDefinitions.Where(lv => lv.Identifier == "spam").First();
        }

        [Fact]
        public void LabelerViewDeserializesCorrectlyWithTypeResolver()
        {
            string json = """
                {
                    "uri": "at://did:plc:ar7c4by46qjdydhdevvrndac/app.bsky.labeler.service/self",
                    "cid": "bafyreigu3v6uxtmx3rv7emh4yvuvz2tom7ib7a4yl3x2eimard2brqeaei",
                    "creator": {
                        "did": "did:plc:ar7c4by46qjdydhdevvrndac",
                        "handle": "moderation.bsky.app",
                        "displayName": "Bluesky Moderation Service",
                        "associated": {
                            "labeler": true
                        },
                        "viewer": {
                            "muted": false,
                            "blockedBy": false
                        },
                        "labels": [],
                        "createdAt": "2023-04-11T17:29:51.242Z",
                        "description": "Official Bluesky moderation service. https://bsky.social/about/support/community-guidelines",
                        "indexedAt": "2024-03-19T17:14:40.208Z"
                    },
                    "viewer": {},
                    "indexedAt": "2024-03-13T15:52:53.522Z",
                    "labels": [],
                    "$type": "app.bsky.labeler.defs#labelerView"
                }
                """;

            LabelerView? labelerView = JsonSerializer.Deserialize<LabelerView>(json, BlueskyServer.BlueskyJsonSerializerOptions);
            Assert.IsType<LabelerView>(labelerView);

            Assert.NotNull(labelerView);
            Assert.Equal("at://did:plc:ar7c4by46qjdydhdevvrndac/app.bsky.labeler.service/self", labelerView.Uri);
            Assert.Equal("bafyreigu3v6uxtmx3rv7emh4yvuvz2tom7ib7a4yl3x2eimard2brqeaei", labelerView.Cid);
            Assert.Equal("did:plc:ar7c4by46qjdydhdevvrndac", labelerView.Creator.Did);
            Assert.Equal("moderation.bsky.app", labelerView.Creator.Handle);
            Assert.Equal("Bluesky Moderation Service", labelerView.Creator.DisplayName);
            Assert.NotNull(labelerView.Creator.Associated);
            Assert.True(labelerView.Creator.Associated.Labeler);
        }
    }
}
