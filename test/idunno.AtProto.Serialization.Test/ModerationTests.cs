// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;

using idunno.AtProto.Admin;
using idunno.AtProto.Moderation;
using idunno.AtProto.Moderation.Model;
using idunno.AtProto.Repo;

namespace idunno.AtProto.Serialization.Test
{
    [ExcludeFromCodeCoverage]
    public class ModerationTests
    {
        private readonly JsonSerializerOptions _jsonSerializerOptions = new(JsonSerializerDefaults.Web);

        [Fact]
        public void CreateReportRequestSerializesProperlyWithStrongReferenceAndSourceGeneratedJsonContext()
        {
            const string expectedReasonType = "ReasonType";
            const string expectedReason = "Reason";
            AtUri atUri = new("at://did:plc:identifier/test.idunno.lexiconType/rkey");
            Cid cid = "bafyreievgu2ty7qbiaaom5zhmkznsnajuzideek3lo7e65dwqlrvrxnmo4";

            CreateReportRequest createReportRequest = new()
            {
                ReasonType = expectedReasonType,
                Reason = expectedReason,
                Subject = new StrongReference(atUri, cid)
            };

            string reportAsJson = JsonSerializer.Serialize(createReportRequest, AtProtoServer.AtProtoJsonSerializerOptions);

            Assert.Equal(
                "{\"reasonType\":\"ReasonType\",\"reason\":\"Reason\",\"subject\":{\"$type\":\"com.atproto.repo.strongRef\",\"uri\":\"at://did:plc:identifier/test.idunno.lexiconType/rkey\",\"cid\":\"bafyreievgu2ty7qbiaaom5zhmkznsnajuzideek3lo7e65dwqlrvrxnmo4\"}}",
                reportAsJson);
        }

        [Fact]
        public void CreateReportRequestSerializesProperlyWithStrongReferenceAndNoConfiguredTypeResolver()
        {
            const string expectedReasonType = "ReasonType";
            const string expectedReason = "Reason";
            AtUri atUri = new("at://did:plc:identifier/test.idunno.lexiconType/rkey");
            Cid cid = "bafyreievgu2ty7qbiaaom5zhmkznsnajuzideek3lo7e65dwqlrvrxnmo4";

            CreateReportRequest createReportRequest = new()
            {
                ReasonType = expectedReasonType,
                Reason = expectedReason,
                Subject = new StrongReference(atUri, cid)
            };

            string reportAsJson = JsonSerializer.Serialize(createReportRequest, _jsonSerializerOptions);

            Assert.Equal(
                "{\"reasonType\":\"ReasonType\",\"reason\":\"Reason\",\"subject\":{\"$type\":\"com.atproto.repo.strongRef\",\"uri\":\"at://did:plc:identifier/test.idunno.lexiconType/rkey\",\"cid\":\"bafyreievgu2ty7qbiaaom5zhmkznsnajuzideek3lo7e65dwqlrvrxnmo4\"}}",
                reportAsJson);
        }

        [Fact]
        public void CreateReportRequestSerializesProperlyWithDidAndSourceGeneratedJsonContext()
        {
            const string expectedReasonType = "ReasonType";
            const string expectedReason = "Reason";
            RepoReference repoRef = new() { Did = "did:plc:cl36izgkuk57kn6ulbliqssk" };

            CreateReportRequest createReportRequest = new()
            {
                ReasonType = expectedReasonType,
                Reason = expectedReason,
                Subject = repoRef
            };

            string reportAsJson = JsonSerializer.Serialize(createReportRequest, AtProtoServer.AtProtoJsonSerializerOptions);

            Assert.Equal(
                "{\"reasonType\":\"ReasonType\",\"reason\":\"Reason\",\"subject\":{\"$type\":\"com.atproto.admin.defs#repoRef\",\"did\":\"did:plc:cl36izgkuk57kn6ulbliqssk\"}}",
                reportAsJson);
        }

        [Fact]
        public void CreateReportRequestSerializesProperlyWithDidAndNoConfiguredResolver()
        {
            const string expectedReasonType = "ReasonType";
            const string expectedReason = "Reason";
            RepoReference repoRef = new() { Did = "did:plc:cl36izgkuk57kn6ulbliqssk" };

            CreateReportRequest createReportRequest = new()
            {
                ReasonType = expectedReasonType,
                Reason = expectedReason,
                Subject = repoRef
            };

            string reportAsJson = JsonSerializer.Serialize(createReportRequest, _jsonSerializerOptions);

            Assert.Equal(
                "{\"reasonType\":\"ReasonType\",\"reason\":\"Reason\",\"subject\":{\"$type\":\"com.atproto.admin.defs#repoRef\",\"did\":\"did:plc:cl36izgkuk57kn6ulbliqssk\"}}",
                reportAsJson);
        }

        [Fact]
        public void RepoRefModerationReportDeserializesWithConfiguredResolver()
        {
            const string json = """
                {
                    "id": 48558138,
                    "createdAt": "2025-04-19T14:50:44.685Z",
                    "reasonType": "reasonType",
                    "reason": "reason",
                    "reportedBy": "did:plc:hfgp6pj3akhqxntgqwramlbg",
                    "subject": {
                        "$type": "com.atproto.admin.defs#repoRef",
                        "did": "did:plc:cl36izgkuk57kn6ulbliqssk"
                    }
                }
                """;

            ModerationReport? actual = JsonSerializer.Deserialize<ModerationReport>(json, AtProtoServer.AtProtoJsonSerializerOptions);

            Assert.NotNull(actual);

            Assert.Equal(48558138U, actual.Id);
            Assert.Equal(DateTimeOffset.Parse("2025-04-19T14:50:44.685Z"), actual.CreatedAt);
            Assert.Equal("reasonType", actual.ReasonType);
            Assert.Equal("reason", actual.Reason);
            Assert.Equal("did:plc:hfgp6pj3akhqxntgqwramlbg", actual.ReportedBy);
            Assert.IsType<RepoReference>(actual.Subject);

            RepoReference? actualRepoReference = actual.Subject as RepoReference;
            Assert.NotNull(actualRepoReference);
            Assert.Equal("did:plc:cl36izgkuk57kn6ulbliqssk", actualRepoReference.Did);
        }

        [Fact]
        public void RepoRefModerationReportDeserializesWithNoConfiguredResolver()
        {
            const string json = """
                {
                    "id": 48558138,
                    "createdAt": "2025-04-19T14:50:44.685Z",
                    "reasonType": "reasonType",
                    "reason": "reason",
                    "reportedBy": "did:plc:hfgp6pj3akhqxntgqwramlbg",
                    "subject": {
                        "$type": "com.atproto.admin.defs#repoRef",
                        "did": "did:plc:cl36izgkuk57kn6ulbliqssk"
                    }
                }
                """;

            ModerationReport? actual = JsonSerializer.Deserialize<ModerationReport>(json, _jsonSerializerOptions);

            Assert.NotNull(actual);

            Assert.Equal(48558138U, actual.Id);
            Assert.Equal(DateTimeOffset.Parse("2025-04-19T14:50:44.685Z"), actual.CreatedAt);
            Assert.Equal("reasonType", actual.ReasonType);
            Assert.Equal("reason", actual.Reason);
            Assert.Equal("did:plc:hfgp6pj3akhqxntgqwramlbg", actual.ReportedBy);
            Assert.IsType<RepoReference>(actual.Subject);

            RepoReference? actualRepoReference = actual.Subject as RepoReference;
            Assert.NotNull(actualRepoReference);
            Assert.Equal("did:plc:cl36izgkuk57kn6ulbliqssk", actualRepoReference.Did);
        }

        [Fact]
        public void StrongReferenceModerationReportDeserializesWithConfiguredResolver()
        {
            const string json = """
                {
                    "id": 48558138,
                    "createdAt": "2025-04-19T14:50:44.685Z",
                    "reasonType": "reasonType",
                    "reason": "reason",
                    "reportedBy": "did:plc:hfgp6pj3akhqxntgqwramlbg",
                    "subject": {
                        "$type": "com.atproto.repo.strongRef",
                        "uri": "at://did:plc:g2p3bnbzcvcbohfpvjxfazqv/app.bsky.feed.post/3lmawgzjbc22v",
                        "cid": "bafyreify2biwc5pv4ymit7s4aq2xtgd6v5slbj7kl4nokhhwjo7nuzfov4"
                    }
                }
                """;

            ModerationReport? actual = JsonSerializer.Deserialize<ModerationReport>(json, AtProtoServer.AtProtoJsonSerializerOptions);

            Assert.NotNull(actual);

            Assert.Equal(48558138U, actual.Id);
            Assert.Equal(DateTimeOffset.Parse("2025-04-19T14:50:44.685Z"), actual.CreatedAt);
            Assert.Equal("reasonType", actual.ReasonType);
            Assert.Equal("reason", actual.Reason);
            Assert.Equal("did:plc:hfgp6pj3akhqxntgqwramlbg", actual.ReportedBy);
            Assert.IsType<StrongReference>(actual.Subject);

            StrongReference? actualRepoReference = actual.Subject as StrongReference;
            Assert.NotNull(actualRepoReference);
            Assert.Equal("at://did:plc:g2p3bnbzcvcbohfpvjxfazqv/app.bsky.feed.post/3lmawgzjbc22v", actualRepoReference.Uri);
            Assert.Equal("bafyreify2biwc5pv4ymit7s4aq2xtgd6v5slbj7kl4nokhhwjo7nuzfov4", actualRepoReference.Cid);
        }

        [Fact]
        public void StrongReferenceModerationReportDeserializesWithNoConfiguredResolver()
        {
            const string json = """
                {
                    "id": 48558138,
                    "createdAt": "2025-04-19T14:50:44.685Z",
                    "reasonType": "reasonType",
                    "reason": "reason",
                    "reportedBy": "did:plc:hfgp6pj3akhqxntgqwramlbg",
                    "subject": {
                        "$type": "com.atproto.repo.strongRef",
                        "uri": "at://did:plc:g2p3bnbzcvcbohfpvjxfazqv/app.bsky.feed.post/3lmawgzjbc22v",
                        "cid": "bafyreify2biwc5pv4ymit7s4aq2xtgd6v5slbj7kl4nokhhwjo7nuzfov4"
                    }
                }
                """;

            ModerationReport? actual = JsonSerializer.Deserialize<ModerationReport>(json, _jsonSerializerOptions);

            Assert.NotNull(actual);

            Assert.Equal(48558138U, actual.Id);
            Assert.Equal(DateTimeOffset.Parse("2025-04-19T14:50:44.685Z"), actual.CreatedAt);
            Assert.Equal("reasonType", actual.ReasonType);
            Assert.Equal("reason", actual.Reason);
            Assert.Equal("did:plc:hfgp6pj3akhqxntgqwramlbg", actual.ReportedBy);
            Assert.IsType<StrongReference>(actual.Subject);

            StrongReference? actualRepoReference = actual.Subject as StrongReference;
            Assert.NotNull(actualRepoReference);
            Assert.Equal("at://did:plc:g2p3bnbzcvcbohfpvjxfazqv/app.bsky.feed.post/3lmawgzjbc22v", actualRepoReference.Uri);
            Assert.Equal("bafyreify2biwc5pv4ymit7s4aq2xtgd6v5slbj7kl4nokhhwjo7nuzfov4", actualRepoReference.Cid);
        }
    }
}
