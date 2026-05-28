// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;
using idunno.AtProto;
using idunno.AtProto.Repo;
using idunno.Bluesky.Embed;

namespace idunno.Bluesky.Serialization.Test;

[ExcludeFromCodeCoverage]
public class EmbeddedExternalTests
{
    [Fact]
    public void ExternalEmbedDeserializesCorrectlyWithALeafletPublication()
    {
        // at://did:plc:sdeg7lksnp2fusabh5lt5d2w/app.bsky.feed.post/3mlookfem2c26

        var json = """
            {
                "$type": "app.bsky.embed.external",
                "external": {
                    "uri": "https://esb-lol-test.leaflet.pub/",
                    "thumb": {
                        "ref": {
                            "$link": "bafkreieh4pmve3mz2d6kwoownym2woak4pwuhb4ia74nb6x5u3pfuhv3cm"
                        },
                        "size": 95089,
                        "$type": "blob",
                        "mimeType": "image/jpeg"
                    },
                    "title": "Test Publication",
                    "description": "Test description"
               }
            }
            """;

        var embeddedBase = JsonSerializer.Deserialize<EmbeddedBase>(json, BlueskyServer.BlueskyJsonSerializerOptions);

        Assert.NotNull(embeddedBase);
        Assert.IsType<EmbeddedExternal>(embeddedBase);

        var embeddedExternal = (EmbeddedExternal)embeddedBase;
        Assert.NotNull(embeddedExternal.External);
        Assert.Equal(new Uri("https://esb-lol-test.leaflet.pub/"), embeddedExternal.External.Uri);
        Assert.NotNull(embeddedExternal.External.Thumbnail);
        Assert.NotNull(embeddedExternal.External.Thumbnail.Reference);
        Assert.IsType<CidLink>(embeddedExternal.External.Thumbnail.Reference);
        Assert.NotNull(embeddedExternal.External.Thumbnail.Reference.Link);
        Assert.Equal("bafkreieh4pmve3mz2d6kwoownym2woak4pwuhb4ia74nb6x5u3pfuhv3cm", embeddedExternal.External.Thumbnail.Reference.Link);
        Assert.Equal(95089, embeddedExternal.External.Thumbnail.Size);
        Assert.Equal("image/jpeg", embeddedExternal.External.Thumbnail.MimeType);
        Assert.Equal("Test Publication", embeddedExternal.External.Title);
        Assert.Equal("Test description", embeddedExternal.External.Description);
    }

    [Fact]
    public void ExternalEmbedDeserializesCorrectlyWithAPkctPublication()
    {
        var json = """
            {
                "$type": "app.bsky.embed.external",
                "external": {
                    "uri": "https://esb-lol-test.pckt.blog/",
                    "thumb": {
                        "ref": {
                            "$link": "bafkreieuuwkzutguy5lfdmxockflk7wx5lvf2fi73nlj4trwbrht6f4loi"
                        },
                        "size": 119911,
                        "$type": "blob",
                        "mimeType": "image/jpeg"
                    },
                    "title": "Esb Lol Test",
                    "description": ""
               }
            }
            """;

        var embeddedBase = JsonSerializer.Deserialize<EmbeddedBase>(json, BlueskyServer.BlueskyJsonSerializerOptions);

        Assert.NotNull(embeddedBase);
        Assert.IsType<EmbeddedExternal>(embeddedBase);

        var embeddedExternal = (EmbeddedExternal)embeddedBase;
        Assert.NotNull(embeddedExternal.External);
        Assert.Equal(new Uri("https://esb-lol-test.pckt.blog/"), embeddedExternal.External.Uri);
        Assert.NotNull(embeddedExternal.External.Thumbnail);
        Assert.NotNull(embeddedExternal.External.Thumbnail.Reference);
        Assert.IsType<CidLink>(embeddedExternal.External.Thumbnail.Reference);
        Assert.NotNull(embeddedExternal.External.Thumbnail.Reference.Link);
        Assert.Equal("bafkreieuuwkzutguy5lfdmxockflk7wx5lvf2fi73nlj4trwbrht6f4loi", embeddedExternal.External.Thumbnail.Reference.Link);
        Assert.Equal(119911, embeddedExternal.External.Thumbnail.Size);
        Assert.Equal("image/jpeg", embeddedExternal.External.Thumbnail.MimeType);
        Assert.Equal("Esb Lol Test", embeddedExternal.External.Title);
        Assert.Equal("", embeddedExternal.External.Description);
    }

    [Fact]
    public void ExternalEmbedDeserializesCorreclyWithAPkctPublicationWithAssociatedRefs()
    {
        var json = """
            {
                "$type": "app.bsky.embed.external",
                "external": {
                  "uri": "https://estrattonbailey.pckt.blog/test-post-bn5bcy2",
                  "thumb": {
                    "ref": {
                      "$link": "bafkreialkemyugm7zjxulfifk57md4cawvt4jdvgchsomgqu6hdihltdxi"
                    },
                    "size": 908330,
                    "$type": "blob",
                    "mimeType": "image/jpeg"
                  },
                  "title": "Test Post - Eric's Pckt Blog",
                  "description": "Test post content",
                  "associatedRefs": [
                    {
                      "cid": "bafyreibhvcdzstnjcktsdaiyjy7f2msthllikx3k3eem2rfqbmgbeniwc4",
                      "uri": "at://did:plc:3jpt2mvvsumj2r7eqk4gzzjz/site.standard.document/3mloolvzj2jsy",
                      "$type": "com.atproto.repo.strongRef"
                    },
                    {
                      "cid": "bafyreigzwdefal6ueevleagplq64yadnxwv6ci5t6tizu3sshszwfe3e64",
                      "uri": "at://did:plc:3jpt2mvvsumj2r7eqk4gzzjz/site.standard.publication/3mlooltppoh4a",
                      "$type": "com.atproto.repo.strongRef"
                    }
                  ]
                }
            }
            """;

        var embeddedBase = JsonSerializer.Deserialize<EmbeddedBase>(json, BlueskyServer.BlueskyJsonSerializerOptions);

        Assert.NotNull(embeddedBase);
        Assert.IsType<EmbeddedExternal>(embeddedBase);

        var embeddedExternal = (EmbeddedExternal)embeddedBase;
        Assert.NotNull(embeddedExternal.External);
        Assert.Equal(new Uri("https://estrattonbailey.pckt.blog/test-post-bn5bcy2"), embeddedExternal.External.Uri);
        Assert.NotNull(embeddedExternal.External.Thumbnail);
        Assert.NotNull(embeddedExternal.External.Thumbnail.Reference);
        Assert.IsType<CidLink>(embeddedExternal.External.Thumbnail.Reference);
        Assert.NotNull(embeddedExternal.External.Thumbnail.Reference.Link);
        Assert.Equal("bafkreialkemyugm7zjxulfifk57md4cawvt4jdvgchsomgqu6hdihltdxi", embeddedExternal.External.Thumbnail.Reference.Link);
        Assert.Equal(908330, embeddedExternal.External.Thumbnail.Size);
        Assert.Equal("image/jpeg", embeddedExternal.External.Thumbnail.MimeType);
        Assert.Equal("Test Post - Eric's Pckt Blog", embeddedExternal.External.Title);
        Assert.Equal("Test post content", embeddedExternal.External.Description);
        Assert.NotNull(embeddedExternal.External.AssociatedRefs);
        Assert.Equal(2, embeddedExternal.External.AssociatedRefs.Count);
        Assert.Equal("bafyreibhvcdzstnjcktsdaiyjy7f2msthllikx3k3eem2rfqbmgbeniwc4", embeddedExternal.External.AssociatedRefs.ElementAt(0)!.Cid);
        Assert.Equal(new AtUri("at://did:plc:3jpt2mvvsumj2r7eqk4gzzjz/site.standard.document/3mloolvzj2jsy"), embeddedExternal.External.AssociatedRefs.ElementAt(0).Uri);
        Assert.Equal("bafyreigzwdefal6ueevleagplq64yadnxwv6ci5t6tizu3sshszwfe3e64", embeddedExternal.External.AssociatedRefs.ElementAt(1)!.Cid);
        Assert.Equal(new AtUri("at://did:plc:3jpt2mvvsumj2r7eqk4gzzjz/site.standard.publication/3mlooltppoh4a"), embeddedExternal.External.AssociatedRefs.ElementAt(1).Uri);
    }
}
