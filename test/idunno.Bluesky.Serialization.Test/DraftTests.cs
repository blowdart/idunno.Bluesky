// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;
using System.Text.Json.Nodes;

using idunno.AtProto.Repo;
using idunno.Bluesky.Drafts;
using idunno.Bluesky.Feed.Gates;

namespace idunno.Bluesky.Serialization.Test
{
    [ExcludeFromCodeCoverage]
    public class DraftTests
    {
        [Fact]
        public void DraftViewWithEmbeddedRecordDeserializesCorrectly()
        {
            string json = """
                {
                    "id": "3meiuav4lyk2t",
                    "draft": {
                        "$type": "app.bsky.draft.defs#draft",
                        "deviceId": "5c76194c-fc19-4413-ac45-bf851a289459",
                        "deviceName": "Web",
                        "posts": [
                            {
                                "$type": "app.bsky.draft.defs#draftPost",
                                "text": "Draft with an embedded quote record",
                                "embedRecords": [
                                    {
                                        "$type": "app.bsky.draft.defs#draftEmbedRecord",
                                        "record": {
                                            "uri": "at://did:plc:hfgp6pj3akhqxntgqwramlbg/app.bsky.feed.post/3mei4u5pu6226",
                                            "cid": "bafyreiehul32p4srxi32ztf3dyp7yeffsbv7654uotlssqaenjite3wt2y"
                                        }
                                    }
                                ]
                            }
                        ]
                    },
                    "createdAt": "2026-02-10T11:28:24.168Z",
                    "updatedAt": "2026-02-10T11:28:24.168Z"
                }
                """;

            DraftView? actual = JsonSerializer.Deserialize<DraftView>(json, BlueskyJsonSerializerOptions.Options);

            Assert.NotNull(actual);
            Assert.Equal("3meiuav4lyk2t", actual.Id);
            Assert.Equal(new Guid("5c76194c-fc19-4413-ac45-bf851a289459"), actual.Draft!.DeviceId);
            Assert.Equal("Web", actual.Draft.DeviceName);
            Assert.Single(actual.Draft.Posts!);
            Assert.Equal("Draft with an embedded quote record", actual.Draft.Posts[0].Text);
            Assert.Single(actual.Draft.Posts[0].EmbedRecords!);
            Assert.Equal(new StrongReference("at://did:plc:hfgp6pj3akhqxntgqwramlbg/app.bsky.feed.post/3mei4u5pu6226", "bafyreiehul32p4srxi32ztf3dyp7yeffsbv7654uotlssqaenjite3wt2y"),
                actual.Draft.Posts[0].EmbedRecords![0].Record!);
            Assert.Equal(DateTimeOffset.Parse("2026-02-10T11:28:24.168Z"), actual.CreatedAt);
            Assert.Equal(DateTimeOffset.Parse("2026-02-10T11:28:24.168Z"), actual.UpdatedAt);
        }

        [Fact]
        public void DraftViewWithMultiplePostsAndThreadAndPostGatesDeserializesCorrectly()
        {
            string json = """
                {
                    "id": "3meitowig4k2x",
                    "draft": {
                        "$type": "app.bsky.draft.defs#draft",
                        "deviceId": "5c76194c-fc19-4413-ac45-bf851a289459",
                        "deviceName": "Web",
                        "posts": [
                            {
                                "$type": "app.bsky.draft.defs#draftPost",
                                "text": "This is a draft thread with a thread gate, and a post gate"
                            },
                            {
                                "$type": "app.bsky.draft.defs#draftPost",
                                "text": "Reply 1"
                            },
                            {
                                "$type": "app.bsky.draft.defs#draftPost",
                                "text": "Reply 2"
                            }
                        ],
                        "threadgateAllow": [
                            {
                                "$type": "app.bsky.feed.threadgate#followingRule"
                            }
                        ],
                        "postgateEmbeddingRules": [
                            {
                                "$type": "app.bsky.feed.postgate#disableRule"
                            }
                        ]
                    },
                    "createdAt": "2026-02-10T11:18:21.780Z",
                    "updatedAt": "2026-02-10T11:18:21.780Z"
                }
                """;

            DraftView? actual = JsonSerializer.Deserialize<DraftView>(json, BlueskyJsonSerializerOptions.Options);

            Assert.NotNull(actual);
            Assert.Equal("3meitowig4k2x", actual.Id);
            Assert.Equal(new Guid("5c76194c-fc19-4413-ac45-bf851a289459"), actual.Draft!.DeviceId);
            Assert.Equal("Web", actual.Draft.DeviceName);
            Assert.Equal(3, actual.Draft.Posts!.Count);
            Assert.Equal("This is a draft thread with a thread gate, and a post gate", actual.Draft.Posts[0].Text);
            Assert.Equal("Reply 1", actual.Draft.Posts[1].Text);
            Assert.Equal("Reply 2", actual.Draft.Posts[2].Text);
            Assert.Single(actual.Draft.ThreadGateAllowRules!);
            Assert.IsType<FollowingRule>(actual.Draft.ThreadGateAllowRules![0]);
            Assert.Single(actual.Draft.PostGateEmbeddingRules!);
            Assert.IsType<DisableEmbeddingRule>(actual.Draft.PostGateEmbeddingRules![0]);
            Assert.Equal(DateTimeOffset.Parse("2026-02-10T11:18:21.780Z"), actual.CreatedAt);
            Assert.Equal(DateTimeOffset.Parse("2026-02-10T11:18:21.780Z"), actual.UpdatedAt);
        }

        [Fact]
        public void DraftViewWithImageAndLabelsDeserializesCorrectly()
        {
            string json = """
                {
                    "id": "3meitnaeyts25",
                    "draft": {
                        "$type": "app.bsky.draft.defs#draft",
                        "deviceId": "5c76194c-fc19-4413-ac45-bf851a289459",
                        "deviceName": "Web",
                        "posts": [
                            {
                                "$type": "app.bsky.draft.defs#draftPost",
                                "text": "This is a self-labelled draft",
                                "labels": {
                                    "$type": "com.atproto.label.defs#selfLabels",
                                    "values": [
                                        {
                                            "val": "graphic-media"
                                        },
                                        {
                                            "val": "sexual"
                                        }
                                    ]
                                },
                                "embedImages": [
                                    {
                                        "$type": "app.bsky.draft.defs#draftEmbedImage",
                                        "localRef": {
                                            "$type": "app.bsky.draft.defs#draftEmbedLocalRef",
                                            "path": "image:Og3ev8pkVSWamiLpEyU9Y"
                                        },
                                        "alt": "Butterfly"
                                    }
                                ]
                            }
                        ]
                    },
                    "createdAt": "2026-02-10T11:17:25.045Z",
                    "updatedAt": "2026-02-10T11:17:25.045Z"
                }
                """;

            DraftView? actual = JsonSerializer.Deserialize<DraftView>(json, BlueskyJsonSerializerOptions.Options);

            Assert.NotNull(actual);
            Assert.Equal("3meitnaeyts25", actual.Id);
            Assert.Equal(new Guid("5c76194c-fc19-4413-ac45-bf851a289459"), actual.Draft!.DeviceId);
            Assert.Equal("Web", actual.Draft.DeviceName);
            Assert.Single(actual.Draft.Posts);
            Assert.Equal("This is a self-labelled draft", actual.Draft.Posts[0].Text);
            Assert.Equal(2, actual.Draft.Posts[0].Labels!.Values.Count);
            Assert.Equal("graphic-media", actual.Draft.Posts[0].Labels!.Values[0]!.Value);
            Assert.Equal("sexual", actual.Draft.Posts[0].Labels!.Values[1]!.Value);
            Assert.Single(actual.Draft.Posts[0].EmbedImages!);
            Assert.Equal("image:Og3ev8pkVSWamiLpEyU9Y", (actual.Draft.Posts[0].EmbedImages![0].LocalRef!).Path);
            Assert.Equal("Butterfly", actual.Draft.Posts[0].EmbedImages![0].AltText);
            Assert.Equal(DateTimeOffset.Parse("2026-02-10T11:17:25.045Z"), actual.CreatedAt);
            Assert.Equal(DateTimeOffset.Parse("2026-02-10T11:17:25.045Z"), actual.UpdatedAt);
        }

        [Fact]
        public void DraftViewWithEmbeddedExternalDeserializesCorrectly()
        {
            string json = """
                {
                    "id": "3meitjzyfbs2x",
                    "draft": {
                        "$type": "app.bsky.draft.defs#draft",
                        "deviceId": "5c76194c-fc19-4413-ac45-bf851a289459",
                        "deviceName": "Web",
                        "posts": [
                            {
                                "$type": "app.bsky.draft.defs#draftPost",
                                "text": "This is a draft with an external link\n\nhttps://www.heinz.com\n",
                                "embedExternals": [
                                    {
                                        "$type": "app.bsky.draft.defs#draftEmbedExternal",
                                        "uri": "https://www.heinz.com"
                                    }
                                ]
                            }
                        ]
                    },
                    "createdAt": "2026-02-10T11:15:37.715Z",
                    "updatedAt": "2026-02-10T11:15:37.715Z"
                }
                """;

            DraftView? actual = JsonSerializer.Deserialize<DraftView>(json, BlueskyJsonSerializerOptions.Options);

            Assert.NotNull(actual);
            Assert.Equal("3meitjzyfbs2x", actual.Id);
            Assert.Equal(new Guid("5c76194c-fc19-4413-ac45-bf851a289459"), actual.Draft!.DeviceId);
            Assert.Equal("Web", actual.Draft.DeviceName);
            Assert.Single(actual.Draft.Posts);
            Assert.Equal("This is a draft with an external link\n\nhttps://www.heinz.com\n", actual.Draft.Posts[0].Text);
            Assert.Single(actual.Draft.Posts[0].EmbedExternals!);
            Assert.Equal(new Uri("https://www.heinz.com"), actual.Draft.Posts[0].EmbedExternals![0].Uri!);
            Assert.Equal(DateTimeOffset.Parse("2026-02-10T11:15:37.715Z"), actual.CreatedAt);
            Assert.Equal(DateTimeOffset.Parse("2026-02-10T11:15:37.715Z"), actual.UpdatedAt);
        }

        [Fact]
        public void DraftViewWithVideoDeserializesCorrectly()
        {
            string json = """
                {
                    "id": "3meitiszczs2d",
                    "draft": {
                        "$type": "app.bsky.draft.defs#draft",
                        "deviceId": "5c76194c-fc19-4413-ac45-bf851a289459",
                        "deviceName": "Web",
                        "posts": [
                            {
                                "$type": "app.bsky.draft.defs#draftPost",
                                "text": "This is a video draft",
                                "embedVideos": [
                                    {
                                        "$type": "app.bsky.draft.defs#draftEmbedVideo",
                                        "localRef": {
                                            "$type": "app.bsky.draft.defs#draftEmbedLocalRef",
                                            "path": "video:video/mp4:wXYnx9IohEuF1QD_AfWhv.mp4"
                                        },
                                        "alt": "Traffic",
                                        "captions": [
                                            {
                                                "$type": "app.bsky.draft.defs#draftEmbedCaption",
                                                "lang": "en",
                                                "content": "WEBVTT\r\n\r\n00:00.000 --> 00:05.000\r\nTraffic makes noise in the background"
                                            }
                                        ]
                                    }
                                ]
                            }
                        ]
                    },
                    "createdAt": "2026-02-10T11:14:56.820Z",
                    "updatedAt": "2026-02-10T11:14:56.820Z"
                }
                """;

            DraftView? actual = JsonSerializer.Deserialize<DraftView>(json, BlueskyJsonSerializerOptions.Options);

            Assert.NotNull(actual);
            Assert.Equal("3meitiszczs2d", actual.Id);
            Assert.Equal(new Guid("5c76194c-fc19-4413-ac45-bf851a289459"), actual.Draft!.DeviceId);
            Assert.Equal("Web", actual.Draft.DeviceName);
            Assert.Single(actual.Draft.Posts);
            Assert.Equal("This is a video draft", actual.Draft.Posts[0].Text);
            Assert.Single(actual.Draft.Posts[0].EmbedVideos!);
            Assert.Equal("video:video/mp4:wXYnx9IohEuF1QD_AfWhv.mp4", (actual.Draft.Posts[0].EmbedVideos![0].LocalRef!).Path);
            Assert.Equal("Traffic", actual.Draft.Posts[0].EmbedVideos![0].AltText);
            Assert.Single(actual.Draft.Posts[0].EmbedVideos![0].Captions!);
            Assert.Equal("en", actual.Draft.Posts[0].EmbedVideos![0].Captions![0].Lang);
            Assert.Equal("WEBVTT\r\n\r\n00:00.000 --> 00:05.000\r\nTraffic makes noise in the background", actual.Draft.Posts[0].EmbedVideos![0].Captions![0].Content);
            Assert.Equal(DateTimeOffset.Parse("2026-02-10T11:14:56.820Z"), actual.CreatedAt);
            Assert.Equal(DateTimeOffset.Parse("2026-02-10T11:14:56.820Z"), actual.UpdatedAt);
        }

        [Fact]
        public void DraftViewWithImageDeserializesCorrectly()
        {
            string json = """
                {
                    "id": "3mehe45pqfk2q",
                    "draft": {
                        "$type": "app.bsky.draft.defs#draft",
                        "deviceId": "c34a6403-4bf2-49bd-8578-21ad32567ec4",
                        "deviceName": "iPhone",
                        "posts": [
                            {
                                "$type": "app.bsky.draft.defs#draftPost",
                                "text": "This is a photo draft ",
                                "embedImages": [
                                    {
                                        "$type": "app.bsky.draft.defs#draftEmbedImage",
                                        "localRef": {
                                            "$type": "app.bsky.draft.defs#draftEmbedLocalRef",
                                            "path": "image:2qSiBW-zfD1wijwSJydpl"
                                        },
                                        "alt": "Alt text"
                                    }
                                ]
                            }
                        ]
                    },
                    "createdAt": "2026-02-09T21:06:45.977Z",
                    "updatedAt": "2026-02-09T21:06:45.977Z"
                }
                """;

            DraftView? actual = JsonSerializer.Deserialize<DraftView>(json, BlueskyJsonSerializerOptions.Options);

            Assert.NotNull(actual);
            Assert.Equal("3mehe45pqfk2q", actual.Id);
            Assert.Equal(new Guid("c34a6403-4bf2-49bd-8578-21ad32567ec4"), actual.Draft!.DeviceId);
            Assert.Equal("iPhone", actual.Draft.DeviceName);
            Assert.Single(actual.Draft.Posts);
            Assert.Equal("This is a photo draft ", actual.Draft.Posts[0].Text);
            Assert.Single(actual.Draft.Posts[0].EmbedImages!);
            Assert.Equal("image:2qSiBW-zfD1wijwSJydpl", (actual.Draft.Posts[0].EmbedImages![0].LocalRef!).Path);
            Assert.Equal("Alt text", actual.Draft.Posts[0].EmbedImages![0].AltText);
            Assert.Equal(DateTimeOffset.Parse("2026-02-09T21:06:45.977Z"), actual.CreatedAt);
            Assert.Equal(DateTimeOffset.Parse("2026-02-09T21:06:45.977Z"), actual.UpdatedAt);
        }

        [Fact]
        public void TextOnlyDraftSerializesCorrectly()
        {
            Guid expectedDeviceId = Guid.NewGuid();
            string expectedDeviceName = "test harness";
            string expectedDraftPostText = "Draft Post";
            List<string> expectedLangs = ["en", "fr"];
            DraftPost expectedDraftPost = new(expectedDraftPostText);
            Draft expectedDraft = new(
                [expectedDraftPost],
                langs: expectedLangs,
                deviceId: expectedDeviceId,
                deviceName: expectedDeviceName);

            string json = JsonSerializer.Serialize(expectedDraft, BlueskyServer.BlueskyJsonSerializerOptions);

            JsonNode? actual = JsonNode.Parse(json);

            Assert.Equal("app.bsky.draft.defs#draft", actual!["$type"]!.GetValue<string>());
            Assert.Equal(expectedDeviceId, actual["deviceId"]!.GetValue<Guid>());
            Assert.Equal(expectedDeviceName, actual["deviceName"]!.GetValue<string>());

            JsonArray actualDraftPostsArray = actual["posts"]!.AsArray();
            Assert.Equal(expectedDraft.Posts.Count, actualDraftPostsArray.Count);

            int offset = 0;
            foreach (JsonNode actualDraftPostNode in actualDraftPostsArray!)
            {
                Assert.Equal("app.bsky.draft.defs#draftPost", actualDraftPostNode["$type"]!.GetValue<string>());
                Assert.Equal(expectedDraft.Posts[offset].Text, actualDraftPostNode["text"]!.GetValue<string>());
                offset++;
            }

            JsonArray actualLangsArray = actual["langs"]!.AsArray();
            Assert.Equal(expectedLangs.Count, actualLangsArray.Count);
            offset = 0;
            foreach (JsonNode actualLanguage in actualLangsArray!)
            {
                Assert.Equal(expectedLangs[offset], actualLanguage.GetValue<string>());
                offset++;
            }
           
        }

        [Fact]
        public void MultipleTextOnlyDraftSerializesCorrectly()
        {
            Guid expectedDeviceId = Guid.NewGuid();
            string expectedDeviceName = "test harness";

            List<DraftPost> expectedDraftPosts = [];
            for (int i = 0; i < 10; i++)
            {
                expectedDraftPosts.Add(new DraftPost($"Draft post {i}"));
            }

            Draft expectedDraft = new(
                expectedDraftPosts,
                deviceId: expectedDeviceId,
                deviceName: expectedDeviceName);

            string json = JsonSerializer.Serialize(expectedDraft, BlueskyServer.BlueskyJsonSerializerOptions);

            JsonNode? actual = JsonNode.Parse(json);

            Assert.Equal("app.bsky.draft.defs#draft", actual!["$type"]!.GetValue<string>());
            Assert.Equal(expectedDeviceId, actual["deviceId"]!.GetValue<Guid>());
            Assert.Equal(expectedDeviceName, actual["deviceName"]!.GetValue<string>());

            JsonArray actualPostsArray = actual["posts"]!.AsArray();
            Assert.Equal(expectedDraft.Posts.Count, actualPostsArray.Count);

            int offset = 0;
            foreach (JsonNode actualDraftPostNode in actualPostsArray!)
            {
                Assert.Equal("app.bsky.draft.defs#draftPost", actualDraftPostNode["$type"]!.GetValue<string>());
                Assert.Equal(expectedDraft.Posts[offset].Text, actualDraftPostNode["text"]!.GetValue<string>());
                offset++;
            }
        }

        [Fact]
        public void DraftWithGatesSerializesCorrectly()
        {
            Guid expectedDeviceId = Guid.NewGuid();
            string expectedDeviceName = "test harness";
            string expectedDraftPostText = "Draft Post";
            List<string> expectedLangs = ["en", "fr"];
            List<PostGateRule> expectedPostGates = [new DisableEmbeddingRule()];
            List<ThreadGateRule> expectedThreadGates = [new MentionRule(), new FollowerRule(), new FollowingRule()];

            DraftPost expectedDraftPost = new(expectedDraftPostText);
            Draft expectedDraft = new(
                [expectedDraftPost],
                deviceId: expectedDeviceId,
                deviceName: expectedDeviceName,
                langs: expectedLangs,
                postGateEmbeddingRules: expectedPostGates,
                threadGateAllowRules: expectedThreadGates);

            string json = JsonSerializer.Serialize(expectedDraft, BlueskyServer.BlueskyJsonSerializerOptions);

            JsonNode? actual = JsonNode.Parse(json);

            JsonArray actualPostGates = actual!["postgateEmbeddingRules"]!.AsArray();
            Assert.Equal(expectedPostGates.Count, actualPostGates.Count);
            Assert.Equal("app.bsky.feed.postgate#disableRule", actualPostGates[0]!.AsObject()["$type"]!.GetValue<string>());

            JsonArray actualThreadGates = actual!["threadgateAllow"]!.AsArray();
            Assert.Equal(expectedThreadGates.Count, actualThreadGates.Count);
            Assert.Equal("app.bsky.feed.threadgate#mentionRule", actualThreadGates[0]!.AsObject()["$type"]!.GetValue<string>());
            Assert.Equal("app.bsky.feed.threadgate#followerRule", actualThreadGates[1]!.AsObject()["$type"]!.GetValue<string>());
            Assert.Equal("app.bsky.feed.threadgate#followingRule", actualThreadGates[2]!.AsObject()["$type"]!.GetValue<string>());
        }

        [Fact]
        public void DraftWithLabelsSerializesCorrectly()
        {
            Guid expectedDeviceId = Guid.NewGuid();
            string expectedDeviceName = "test harness";
            string expectedDraftPostText = "Draft Post";
            PostSelfLabels expectedSelfLabels = new()
            {
                Nudity = true
            };

            DraftPost expectedDraftPost = new (
                expectedDraftPostText,
                postSelfLabels: expectedSelfLabels,
                embedImages: null,
                embedVideos: null,
                embedExternals: null,
                embedRecords: null);

            Draft expectedDraft = new(
                [expectedDraftPost],
                deviceId: expectedDeviceId,
                deviceName: expectedDeviceName);

            string json = JsonSerializer.Serialize(expectedDraft, BlueskyServer.BlueskyJsonSerializerOptions);

            JsonNode? actual = JsonNode.Parse(json);

            JsonArray actualDraftPosts = actual!["posts"]!.AsArray();
            JsonNode actualDraftPost = actualDraftPosts[0]!;

            JsonNode actualLabels = actualDraftPost!["labels"]!;
            Assert.Equal("com.atproto.label.defs#selfLabels", actualLabels["$type"]!.GetValue<string>());

            JsonArray actualLabelsValuesArray = actualDraftPost["labels"]!["values"]!.AsArray();
            Assert.Single(actualLabelsValuesArray);

            Assert.Equal("{\"val\":\"nudity\"}", actualLabelsValuesArray!.First()!.ToJsonString());
        }

        [Fact]
        public void DraftWithMultipleImagesSerializesCorrectly()
        {
            Guid expectedDeviceId = Guid.NewGuid();
            string expectedDeviceName = "test harness";
            string expectedDraftPostText = "Draft Post";
            List<DraftEmbedImage> expectedDraftEmbedImages = [];
            for (int i = 0; i < 4; i++)
            {
                expectedDraftEmbedImages.Add(new DraftEmbedImage(new DraftEmbedLocalRef($"image:image{i}.png"), "{i}"));
            }
            DraftPost expectedDraftPost = new(expectedDraftPostText, embedImages: expectedDraftEmbedImages);

            Draft expectedDraft = new(
                expectedDraftPost,
                deviceId: expectedDeviceId,
                deviceName: expectedDeviceName);

            string json = JsonSerializer.Serialize(expectedDraft, BlueskyServer.BlueskyJsonSerializerOptions);

            JsonNode? actual = JsonNode.Parse(json);

            Assert.Equal("app.bsky.draft.defs#draft", actual!["$type"]!.GetValue<string>());
            Assert.Equal(expectedDeviceId, actual["deviceId"]!.GetValue<Guid>());
            Assert.Equal(expectedDeviceName, actual["deviceName"]!.GetValue<string>());

            JsonArray actualDraftPosts = actual["posts"]!.AsArray();
            Assert.Equal(expectedDraft.Posts.Count, actualDraftPosts.Count);

            int offset = 0;
            foreach (JsonNode actualDraftPostNode in actualDraftPosts!)
            {
                Assert.Equal("app.bsky.draft.defs#draftPost", actualDraftPostNode["$type"]!.GetValue<string>());
                Assert.Equal(expectedDraft.Posts[offset].Text, actualDraftPostNode["text"]!.GetValue<string>());

                JsonArray actualDraftEmbeddedImagesArray = actualDraftPostNode["embedImages"]!.AsArray();
                Assert.Equal(expectedDraft.Posts[offset].EmbedImages!.Count, actualDraftEmbeddedImagesArray!.Count);

                int imageOffset = 0;

                foreach (JsonNode actualDraftEmbeddedImage in actualDraftEmbeddedImagesArray!)
                {
                    Assert.Equal("app.bsky.draft.defs#draftEmbedImage", actualDraftEmbeddedImage!["$type"]!.GetValue<string>());
                    Assert.Equal(expectedDraft.Posts[offset].EmbedImages![imageOffset].AltText, actualDraftEmbeddedImage["alt"]!.GetValue<string>());

                    JsonNode actualDraftEmbeddedImageLocalRef = actualDraftEmbeddedImage!["localRef"]!;

                    Assert.Equal("app.bsky.draft.defs#draftEmbedLocalRef", actualDraftEmbeddedImageLocalRef!["$type"]!.GetValue<string>());
                    Assert.Equal(expectedDraft.Posts[offset].EmbedImages![imageOffset].LocalRef.Path, actualDraftEmbeddedImageLocalRef!["path"]!.GetValue<string>());

                    imageOffset++;
                }

                offset++;
            }
        }
    }
}
