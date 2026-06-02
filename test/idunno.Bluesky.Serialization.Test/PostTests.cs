// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;

using idunno.AtProto;
using idunno.Bluesky.Embed;
using idunno.Bluesky.RichText;

namespace idunno.Bluesky.Serialization.Test;

[ExcludeFromCodeCoverage]
public class PostTests
{
    private readonly JsonSerializerOptions _jsonSerializerOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public void SimplePostDeserializesCorrectlyWithSourceGeneratedJsonContext()
    {
        string json = """
            {
                "$type": "app.bsky.feed.post",
                "createdAt": "2025-04-25T17:25:46.3164586+00:00",
                "langs": [
                    "en"
                ],
                "text": "Post text"
            }
            """;

        Post? post = JsonSerializer.Deserialize<Post>(json, BlueskyServer.BlueskyJsonSerializerOptions);

        Assert.NotNull(post);
        Assert.Equal("Post text", post.Text);
        Assert.NotNull(post.Langs);
        Assert.Single(post.Langs);
        Assert.Equal("en", post.Langs.ElementAt(0));
        Assert.Equal(DateTimeOffset.Parse("2025-04-25T17:25:46.3164586+00:00"), post.CreatedAt);
    }

    [Fact]
    public void SimplePostDeserializesCorrectlyWithoutSourceGeneratedJsonContext()
    {
        string json = """
            {
                "$type": "app.bsky.feed.post",
                "createdAt": "2025-04-25T17:25:46.3164586+00:00",
                "langs": [
                    "en"
                ],
                "text": "Post text"
            }
            """;

        Post? post = JsonSerializer.Deserialize<Post>(json, _jsonSerializerOptions);

        Assert.NotNull(post);
        Assert.Equal("Post text", post.Text);
        Assert.NotNull(post.Langs);
        Assert.Single(post.Langs);
        Assert.Equal("en", post.Langs.ElementAt(0));
        Assert.Equal(DateTimeOffset.Parse("2025-04-25T17:25:46.3164586+00:00"), post.CreatedAt);
    }

    [Fact]
    public void SimplePostDeserializesCorrectlyWithJavaScriptDateTimeAndSourceGeneratedJsonContext()
    {
        string json = """
            {
                "$type": "app.bsky.feed.post",
                "createdAt": "2023-08-07T05:49:39.417839Z",
                "langs": [
                    "en"
                ],
                "text": "Post text"
            }
            """;

        Post? post = JsonSerializer.Deserialize<Post>(json, BlueskyServer.BlueskyJsonSerializerOptions);

        Assert.NotNull(post);
        Assert.Equal("Post text", post.Text);
        Assert.NotNull(post.Langs);
        Assert.Single(post.Langs);
        Assert.Equal("en", post.Langs.ElementAt(0));
        Assert.Equal(DateTimeOffset.Parse("2023-08-07T05:49:39.417839Z"), post.CreatedAt);
    }

    [Fact]
    public void PostWithPcktEmbeddedExternalDeserializesCorrectly()
    {
        string json = """
            {
              "text": "estrattonbailey.pckt.blog/test-post-bn...",
              "$type": "app.bsky.feed.post",
              "embed": {
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
              },
              "langs": [
                "de"
              ],
              "facets": [
                {
                  "index": {
                    "byteEnd": 41,
                    "byteStart": 0
                  },
                  "features": [
                    {
                      "uri": "https://estrattonbailey.pckt.blog/test-post-bn5bcy2",
                      "$type": "app.bsky.richtext.facet#link"
                    }
                  ]
                }
              ],
              "createdAt": "2026-05-21T17:41:14.270Z"
            }
            """;

        Post? post = JsonSerializer.Deserialize<Post>(json, BlueskyServer.BlueskyJsonSerializerOptions);

        Assert.NotNull(post);
        Assert.Equal("estrattonbailey.pckt.blog/test-post-bn...", post.Text);
        Assert.NotNull(post.Langs);
        Assert.Single(post.Langs);
        Assert.Equal("de", post.Langs.ElementAt(0));
        Assert.Equal(DateTimeOffset.Parse("2026-05-21T17:41:14.270Z"), post.CreatedAt);
        Assert.NotNull(post.Facets);
        Assert.Single(post.Facets);
        Assert.Equal(0,post.Facets.ElementAt(0)!.Index!.ByteStart);
        Assert.Equal(41, post.Facets.ElementAt(0)!.Index!.ByteEnd);
        Assert.Single(post.Facets.ElementAt(0)!.Features);
        Assert.IsType<LinkFacetFeature>(post.Facets.ElementAt(0)!.Features!.ElementAt(0));
        var link = (LinkFacetFeature)post.Facets.ElementAt(0)!.Features!.ElementAt(0);
        Assert.Equal(new Uri("https://estrattonbailey.pckt.blog/test-post-bn5bcy2"), link.Uri);
        Assert.NotNull(post.EmbeddedRecord);
        Assert.IsType<EmbeddedExternal>(post.EmbeddedRecord);
        var embeddedExternal = (EmbeddedExternal)post.EmbeddedRecord;
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
