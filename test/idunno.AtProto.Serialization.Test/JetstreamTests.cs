// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;
using idunno.AtProto.Jetstream;

namespace idunno.AtProto.Serialization.Test
{
    [ExcludeFromCodeCoverage]
    public class JetstreamTests
    {
        [Fact]
        public void LikeMessageDeserializesCorrectly()
        {
            string json = """
                {
                  "did": "did:plc:eygmaihciaxprqvxpfvl6flk",
                  "time_us": 1725911162329308,
                  "kind": "commit",
                  "commit": {
                    "rev": "3l3qo2vutsw2b",
                    "operation": "create",
                    "collection": "app.bsky.feed.like",
                    "rkey": "3l3qo2vuowo2b",
                    "record": {
                      "$type": "app.bsky.feed.like",
                      "createdAt": "2024-09-09T19:46:02.102Z",
                      "subject": {
                        "cid": "bafyreidc6sydkkbchcyg62v77wbhzvb2mvytlmsychqgwf2xojjtirmzj4",
                        "uri": "at://did:plc:wa7b35aakoll7hugkrjtf3xf/app.bsky.feed.post/3l3pte3p2e325"
                      }
                    },
                    "cid": "bafyreidwaivazkwu67xztlmuobx35hs2lnfh3kolmgfmucldvhd3sgzcqi"
                  }
                }
                """;

            AtJetstreamEvent? actual = JsonSerializer.Deserialize<AtJetstreamEvent>(json, SourceGenerationContext.Default.AtJetstreamEvent);

            Assert.NotNull(actual);
            Assert.Equal("did:plc:eygmaihciaxprqvxpfvl6flk", actual.Did);
            Assert.Equal(JetStreamEventKind.Commit, actual.Kind);
            Assert.Equal(1725911162329308, actual.TimeStamp);
            Assert.Equal(2024, actual.DateTimeOffset.Year);
            Assert.Equal(9, actual.DateTimeOffset.Month);
            Assert.Equal(9, actual.DateTimeOffset.Day);
            Assert.Equal(19, actual.DateTimeOffset.Hour);
            Assert.Equal(46, actual.DateTimeOffset.Minute);
            Assert.Equal(2, actual.DateTimeOffset.Second);
            Assert.Equal(329, actual.DateTimeOffset.Millisecond);

            var jetStream = new AtProtoJetstream();
            AtJetstreamEvent? derived = jetStream.DeriveEvent(actual);

            Assert.NotNull(derived);
            Assert.IsType<AtJetstreamCommitEvent>(derived);

            AtJetstreamCommitEvent? derivedCast = derived as AtJetstreamCommitEvent;
            Assert.NotNull(derivedCast);

            Assert.Equal("3l3qo2vutsw2b", derivedCast.Commit.Rev);
            Assert.Equal("create", derivedCast.Commit.Operation);
            Assert.Equal("app.bsky.feed.like", derivedCast.Commit.Collection);
            Assert.Equal("3l3qo2vuowo2b", derivedCast.Commit.RKey);
            Assert.NotNull(derivedCast.Commit.Record);
            Assert.Equal("bafyreidwaivazkwu67xztlmuobx35hs2lnfh3kolmgfmucldvhd3sgzcqi", derivedCast.Commit.Cid);
        }

        [Fact]
        public void DeletePostMessageDeserializesCorrectly()
        {
            string json = """
                {
                  "did":"did:plc:ec72yg6n2sydzjvtovvdlxrk",
                  "time_us":1746659188088518,
                  "kind": "commit",
                  "commit":
                  {
                    "rev":"3lomj6b3nd42r",
                    "operation":"delete",
                    "collection":"app.bsky.feed.post",
                    "rkey":"3lomj5uz4ms2y"
                  }
                }
                """;

            AtJetstreamEvent? actual = JsonSerializer.Deserialize<AtJetstreamEvent>(json, SourceGenerationContext.Default.AtJetstreamEvent);

            Assert.NotNull(actual);
            Assert.Equal("did:plc:ec72yg6n2sydzjvtovvdlxrk", actual.Did);
            Assert.Equal(JetStreamEventKind.Commit, actual.Kind);

            var jetStream = new AtProtoJetstream();
            AtJetstreamEvent? derived = jetStream.DeriveEvent(actual);

            Assert.NotNull(derived);
            Assert.IsType<AtJetstreamCommitEvent>(derived);

            AtJetstreamCommitEvent? derivedCast = derived as AtJetstreamCommitEvent;
            Assert.NotNull(derivedCast);

            Assert.Equal("delete", derivedCast.Commit.Operation);
            Assert.Null(derivedCast.Commit.Cid);
        }

        [Fact]
        public void EditProfileMessageDeserializesCorrectly()
        {
            string json = """
                {
                  "did": "did:plc:ec72yg6n2sydzjvtovvdlxrk",
                  "time_us": 1746659559354914,
                  "kind": "commit",
                  "commit": {
                    "rev": "3lomjjcyfbb2g",
                    "operation": "update",
                    "collection": "app.bsky.actor.profile",
                    "rkey": "self",
                    "record": {
                      "$type": "app.bsky.actor.profile",
                      "avatar": {
                        "$type": "blob",
                        "ref": {
                          "$link": "bafkreig5ujmxrechgakn4ukf37oj6mlpivukqfbgpuhb3pqjmdkxxtjpnq"
                        },
                        "mimeType": "image/jpeg",
                        "size": 297502
                      },
                      "banner": {
                        "$type": "blob",
                        "ref": {
                          "$link": "bafkreicxefpc7lmktbjt7mdvnm5kfvdfcvkf2i266upxvi54gbywbt2wim"
                        },
                        "mimeType": "image/jpeg",
                        "size": 205105
                      },
                      "description": "idunno.Bluesky Test Bot!",
                      "displayName": "Test Bot",
                      "labels": {
                        "$type": "com.atproto.label.defs#selfLabels",
                        "values": []
                      }
                    },
                    "cid": "bafyreiaczmnb4aftyufz6ke3nsfi6qey46cut2aiorcfpai36ktmd35ifu"
                  }
                }
                """;

            AtJetstreamEvent? actual = JsonSerializer.Deserialize<AtJetstreamEvent>(json, SourceGenerationContext.Default.AtJetstreamEvent);

            Assert.NotNull(actual);
            Assert.Equal("did:plc:ec72yg6n2sydzjvtovvdlxrk", actual.Did);
            Assert.Equal(JetStreamEventKind.Commit, actual.Kind);

            var jetStream = new AtProtoJetstream();
            AtJetstreamEvent? derived = jetStream.DeriveEvent(actual);

            Assert.NotNull(derived);
            Assert.IsType<AtJetstreamCommitEvent>(derived);

            AtJetstreamCommitEvent? derivedCast = derived as AtJetstreamCommitEvent;
            Assert.NotNull(derivedCast);

            Assert.Equal("update", derivedCast.Commit.Operation);
        }

        [Fact]
        public void AccountTakedownMessageDeserializesCorrectly()
        {
            string json = """
                {
                "did":"did:plc:lfpx2poegjaojca4u3ctkrja",
                "time_us":1746660784089138,
                "kind":"account",
                "account": {
                  "active":false,
                  "did":"did:plc:lfpx2poegjaojca4u3ctkrja",
                  "seq":8946705608,
                  "status":"takendown",
                  "time":"2025-05-07T23:33:03.840Z"
                  }
                }
                """;

            AtJetstreamEvent? actual = JsonSerializer.Deserialize<AtJetstreamEvent>(json, SourceGenerationContext.Default.AtJetstreamEvent);

            Assert.NotNull(actual);
            Assert.Equal("did:plc:lfpx2poegjaojca4u3ctkrja", actual.Did);
            Assert.Equal(JetStreamEventKind.Account, actual.Kind);

            var jetStream = new AtProtoJetstream();
            AtJetstreamEvent? derived = jetStream.DeriveEvent(actual);

            Assert.NotNull(derived);
            Assert.IsType<AtJetstreamAccountEvent>(derived);

            AtJetstreamAccountEvent? derivedCast = derived as AtJetstreamAccountEvent;
            Assert.NotNull(derivedCast);

            Assert.False(derivedCast.Account.Active);
            Assert.Equal("did:plc:lfpx2poegjaojca4u3ctkrja", derivedCast.Account.Did);
            Assert.Equal(AccountStatus.Takendown, derivedCast.Account.Status);
        }

        [Fact]
        public void AccountActivateMessageDeserializesCorrectly()
        {
            string json = """
                {
                  "did": "did:plc:ufbl4k27gp6kzas5glhz7fim",
                  "time_us": 1725516665333808,
                  "kind": "account",
                  "account": {
                    "active": true,
                    "did": "did:plc:ufbl4k27gp6kzas5glhz7fim",
                    "seq": 1409753013,
                    "time": "2024-09-05T06:11:04.870Z"
                  }
                }
                """;

            AtJetstreamEvent? actual = JsonSerializer.Deserialize<AtJetstreamEvent>(json, SourceGenerationContext.Default.AtJetstreamEvent);

            Assert.NotNull(actual);
            Assert.Equal("did:plc:ufbl4k27gp6kzas5glhz7fim", actual.Did);
            Assert.Equal(JetStreamEventKind.Account, actual.Kind);

            var jetStream = new AtProtoJetstream();
            AtJetstreamEvent? derived = jetStream.DeriveEvent(actual);

            Assert.NotNull(derived);
            Assert.IsType<AtJetstreamAccountEvent>(derived);

            AtJetstreamAccountEvent? derivedCast = derived as AtJetstreamAccountEvent;
            Assert.NotNull(derivedCast);

            Assert.True(derivedCast.Account.Active);
            Assert.Equal("did:plc:ufbl4k27gp6kzas5glhz7fim", derivedCast.Account.Did);
            Assert.Equal(1409753013U, derivedCast.Account.Sequence);
            Assert.Null(derivedCast.Account.Status);
        }

        [Fact]
        public void IdentityMessageDeserializesCorrectly()
        {
            string json = """
                {
                  "did":"did:plc:g6ylltenitt4tp27bpwalh7b",
                  "time_us":1746663645473657,
                  "kind":"identity",
                  "identity": {
                    "did":"did:plc:g6ylltenitt4tp27bpwalh7b",
                    "handle":"miyakotubaki.bsky.social",
                    "seq":8948066763,
                    "time":"2025-05-08T00:20:44.859Z"}
                }
                """;

            AtJetstreamEvent? actual = JsonSerializer.Deserialize<AtJetstreamEvent>(json, SourceGenerationContext.Default.AtJetstreamEvent);

            Assert.NotNull(actual);
            Assert.Equal("did:plc:g6ylltenitt4tp27bpwalh7b", actual.Did);
            Assert.Equal(JetStreamEventKind.Identity, actual.Kind);

            var jetStream = new AtProtoJetstream();
            AtJetstreamEvent? derived = jetStream.DeriveEvent(actual);

            Assert.NotNull(derived);
            Assert.IsType<AtJetstreamIdentityEvent>(derived);

            AtJetstreamIdentityEvent? derivedCast = derived as AtJetstreamIdentityEvent;
            Assert.NotNull(derivedCast);

            Assert.Equal("did:plc:g6ylltenitt4tp27bpwalh7b", derivedCast.Identity.Did);
            Assert.Equal("miyakotubaki.bsky.social", derivedCast.Identity.Handle);
            Assert.Equal(8948066763U, derivedCast.Identity.Sequence);
        }
    }
}
