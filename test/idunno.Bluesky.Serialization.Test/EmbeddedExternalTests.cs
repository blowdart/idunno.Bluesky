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
    public void ExternalCardWithQuotedRecordRoundTrips()
    {
        StrongReference reference = new(
            new AtUri("at://did:plc:3jpt2mvvsumj2r7eqk4gzzjz/app.bsky.feed.post/3mloolvzj2jsy"),
            new Cid("bafyreibhvcdzstnjcktsdaiyjy7f2msthllikx3k3eem2rfqbmgbeniwc4"));
        EmbeddedExternal card = new("https://example.com", "Card title", "Card description", associatedRefs: [reference]);
        EmbeddedRecordWithMedia embed = new(new EmbeddedRecord(reference), card);
        Assert.Same(card.External, Assert.IsType<EmbeddedExternalMedia>(embed.Media).External);
        Post post = new(string.Empty, DateTimeOffset.UtcNow, embeddedRecord: embed);

        string json = JsonSerializer.Serialize(post, BlueskyServer.BlueskyJsonSerializerOptions);
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement media = document.RootElement.GetProperty("embed").GetProperty("media");
        Assert.Equal("app.bsky.embed.external", media.GetProperty("$type").GetString());
        Assert.Equal("https://example.com", media.GetProperty("external").GetProperty("uri").GetString());

        Post? deserialized = JsonSerializer.Deserialize<Post>(json, BlueskyServer.BlueskyJsonSerializerOptions);
        Assert.NotNull(deserialized);
        Assert.Equal(string.Empty, deserialized.Text);
        EmbeddedRecordWithMedia actualEmbed = Assert.IsType<EmbeddedRecordWithMedia>(deserialized.EmbeddedRecord);
        Assert.Equal(reference, actualEmbed.Record.Record);
        EmbeddedExternalMedia actualCard = Assert.IsType<EmbeddedExternalMedia>(actualEmbed.Media);
        Assert.Equal(card.External.Uri, actualCard.External.Uri);
        Assert.Equal(card.External.Title, actualCard.External.Title);
        Assert.Equal(card.External.Description, actualCard.External.Description);
        Assert.Equal(reference, Assert.Single(actualCard.External.AssociatedRefs!));
    }

    [Fact]
    public void ExternalMediaRoundTripsThroughMediaBase()
    {
        EmbeddedMediaBase media = new EmbeddedExternalMedia(new EmbeddedExternal("https://example.com", "Title", "Description").External);
        string json = JsonSerializer.Serialize(media, BlueskyServer.BlueskyJsonSerializerOptions);
        EmbeddedMediaBase? deserialized = JsonSerializer.Deserialize<EmbeddedMediaBase>(json, BlueskyServer.BlueskyJsonSerializerOptions);
        EmbeddedExternalMedia card = Assert.IsType<EmbeddedExternalMedia>(deserialized);
        Assert.Equal("https://example.com", card.External.Uri);
        Assert.Equal("Title", card.External.Title);
        Assert.Equal("Description", card.External.Description);
    }

    [Fact]
    public void ExternalCardPreservesShippedInheritanceAndEquality()
    {
        EmbeddedExternal card = new("https://example.com", "Title", "Description");
        EmbeddedBase other = card with { };

        Assert.Equal(typeof(EmbeddedBase), typeof(EmbeddedExternal).BaseType);
        Assert.True(card.Equals(other));
        Assert.True(other.Equals(card));
        Assert.False(card.Equals(new EmbeddedBase()));
        Assert.False(card.Equals((EmbeddedBase?)null));
    }

    [Fact]
    public void RecordWithMediaConstructorPreservesNullCallsAndValidatesMedia()
    {
        EmbeddedRecord record = new(new StrongReference(
            new AtUri("at://did:plc:3jpt2mvvsumj2r7eqk4gzzjz/app.bsky.feed.post/3mloolvzj2jsy"),
            new Cid("bafyreibhvcdzstnjcktsdaiyjy7f2msthllikx3k3eem2rfqbmgbeniwc4")));

        Assert.Throws<ArgumentNullException>("media", () => new EmbeddedRecordWithMedia(record, null!));
        Assert.Throws<ArgumentNullException>("media", () => new EmbeddedRecordWithMedia(record, (EmbeddedBase)null!));
        Assert.Throws<ArgumentException>("media", () => new EmbeddedRecordWithMedia(record, new EmbeddedBase()));
        Assert.Throws<ArgumentNullException>("record", () => new EmbeddedRecordWithMedia(null!, new EmbeddedExternal("https://example.com", "Title", "Description")));
    }

    [Fact]
    public void ExternalEmbedDeserializesCorrectlyWithALeafletPublication()
    {
        // at://did:plc:sdeg7lksnp2fusabh5lt5d2w/app.bsky.feed.post/3mlookfem2c26

        string json = """
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

        EmbeddedBase? embeddedBase = JsonSerializer.Deserialize<EmbeddedBase>(json, BlueskyServer.BlueskyJsonSerializerOptions);

        Assert.NotNull(embeddedBase);

        EmbeddedExternal embeddedExternal = Assert.IsType<EmbeddedExternal>(embeddedBase);
        Assert.NotNull(embeddedExternal.External);
        Assert.Equal("https://esb-lol-test.leaflet.pub/", embeddedExternal.External.Uri);
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
    public void ExternalEmbedDeserializesCorrectlyWithPkctPublicationData()
    {
        string json = """
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

        EmbeddedBase? embeddedBase = JsonSerializer.Deserialize<EmbeddedBase>(json, BlueskyServer.BlueskyJsonSerializerOptions);

        Assert.NotNull(embeddedBase);

        EmbeddedExternal embeddedExternal = Assert.IsType<EmbeddedExternal>(embeddedBase);
        Assert.NotNull(embeddedExternal.External);
        Assert.Equal("https://esb-lol-test.pckt.blog/", embeddedExternal.External.Uri);
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
    public void ExternalEmbedDeserializesCorrectlyWithAPkctPublicationAndAssociatedRefs()
    {
        string json = """
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

        EmbeddedBase? embeddedBase = JsonSerializer.Deserialize<EmbeddedBase>(json, BlueskyServer.BlueskyJsonSerializerOptions);

        Assert.NotNull(embeddedBase);

        EmbeddedExternal embeddedExternal = Assert.IsType<EmbeddedExternal>(embeddedBase);
        Assert.NotNull(embeddedExternal.External);
        Assert.Equal("https://estrattonbailey.pckt.blog/test-post-bn5bcy2", embeddedExternal.External.Uri);
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
