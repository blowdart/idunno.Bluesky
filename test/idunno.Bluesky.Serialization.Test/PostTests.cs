// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;

using idunno.AtProto;
using idunno.AtProto.Repo;
using idunno.Bluesky.Embed;
using idunno.Bluesky.Record;
using idunno.Bluesky.RichText;

namespace idunno.Bluesky.Serialization.Test;

[ExcludeFromCodeCoverage]
public class PostTests
{
    private readonly JsonSerializerOptions _jsonSerializerOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public void ReproducedWhitespacePostDeserializesWithProductionMetadata()
    {
        const string json = """
            {
              "text": " ",
              "$type": "app.bsky.feed.post",
              "createdAt": "2026-10-08T15:43:06.08711400Z"
            }
            """;

        Post? post = JsonSerializer.Deserialize<Post>(json, BlueskyServer.BlueskyJsonSerializerOptions);
        Assert.NotNull(post);
        Assert.Equal(" ", post.Text);
        Assert.Equal(DateTimeOffset.Parse("2026-10-08T15:43:06.08711400Z"), post.CreatedAt);
        Assert.Null(post.EmbeddedRecord);

        BlueskyRecord? record = JsonSerializer.Deserialize<BlueskyRecord>(json, BlueskyServer.BlueskyJsonSerializerOptions);
        Assert.Equal(" ", Assert.IsType<Post>(record).Text);

        string envelopeJson = $$"""
            {
              "uri": "at://did:plc:kaxwhrcwrqdxm2ppaaczwhcn/app.bsky.feed.post/3mxesfgzw4q2r",
              "cid": "bafyreievgu2ty7qbiaaom5zhmkznsnajuzideek3lo7e65dwqlrvrxnmo4",
              "value": {{json}}
            }
            """;
        AtProtoRepositoryRecord<Post>? envelope = JsonSerializer.Deserialize<AtProtoRepositoryRecord<Post>>(
            envelopeJson, BlueskyServer.BlueskyJsonSerializerOptions);
        Assert.NotNull(envelope);
        Assert.Equal(" ", envelope.Value.Text);
    }

    [Theory]
    [InlineData(" ", false)]
    [InlineData("   ", false)]
    [InlineData("\t\t", false)]
    [InlineData("\n\n", false)]
    [InlineData("\r\n", false)]
    [InlineData(" \t\r\n ", false)]
    [InlineData(" ", true)]
    [InlineData("\t\t", true)]
    [InlineData("\n\n", true)]
    [InlineData(null, true)]
    [InlineData("", true)]
    public void PostTextRoundTripsUnchanged(string? text, bool hasEmbed)
    {
        string json = CreatePostJson(text, hasEmbed);

        foreach (JsonSerializerOptions options in new[] { _jsonSerializerOptions, BlueskyServer.BlueskyJsonSerializerOptions })
        {
            Post? post = JsonSerializer.Deserialize<Post>(json, options);
            Assert.NotNull(post);
            Assert.Equal(text, post.Text);
            Assert.Equal(hasEmbed, post.EmbeddedRecord is not null);

            string serialized = JsonSerializer.Serialize(post, options);
            using JsonDocument document = JsonDocument.Parse(serialized);
            if (text is not null)
            {
                Assert.Equal(text, document.RootElement.GetProperty("text").GetString());
            }
            else if (document.RootElement.TryGetProperty("text", out JsonElement serializedText))
            {
                Assert.Equal(JsonValueKind.Null, serializedText.ValueKind);
            }
            Assert.Equal("app.bsky.feed.post", document.RootElement.GetProperty("$type").GetString());

            Post? roundTripped = JsonSerializer.Deserialize<Post>(serialized, options);
            Assert.NotNull(roundTripped);
            Assert.Equal(text, roundTripped.Text);
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void NullOrEmptyTextWithoutEmbedStillFailsDeserialization(string? text)
    {
        foreach (JsonSerializerOptions options in new[] { _jsonSerializerOptions, BlueskyServer.BlueskyJsonSerializerOptions })
        {
            ArgumentNullException exception = Assert.Throws<ArgumentNullException>(
                () => JsonSerializer.Deserialize<Post>(CreatePostJson(text, false), options));
            Assert.Equal("text", exception.ParamName);
        }
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void DeserializationStillEnforcesTextLengthLimits(bool testBytes, bool hasEmbed)
    {
        string atLimit = testBytes
            ? "a" + new string('\u0301', (Maximum.PostLengthInBytes - 2) / 2) + "b"
            : new string(' ', Maximum.PostLengthInGraphemes);
        string overLimit = atLimit + (testBytes ? "c" : " ");
        Assert.Equal(testBytes ? Maximum.PostLengthInBytes : Maximum.PostLengthInGraphemes,
            testBytes ? atLimit.GetUtf8Length() : atLimit.GetGraphemeLength());
        Assert.Equal(testBytes ? Maximum.PostLengthInBytes + 1 : Maximum.PostLengthInGraphemes + 1,
            testBytes ? overLimit.GetUtf8Length() : overLimit.GetGraphemeLength());

        foreach (JsonSerializerOptions options in new[] { _jsonSerializerOptions, BlueskyServer.BlueskyJsonSerializerOptions })
        {
            Post? post = JsonSerializer.Deserialize<Post>(CreatePostJson(atLimit, hasEmbed), options);
            Assert.NotNull(post);
            Assert.Equal(atLimit, post.Text);

            ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(
                () => JsonSerializer.Deserialize<Post>(CreatePostJson(overLimit, hasEmbed), options));
            Assert.Equal("text", exception.ParamName);
        }
    }

    private string CreatePostJson(string? text, bool hasEmbed)
    {
        string embed = hasEmbed
            ? """
                ,"embed":{"$type":"app.bsky.embed.external","external":{"uri":"https://example.com","title":"Example","description":"An example card"}}
                """
            : string.Empty;

        return $$"""
            {"$type":"app.bsky.feed.post","text":{{JsonSerializer.Serialize(text, _jsonSerializerOptions)}},"createdAt":"2026-10-08T15:43:06.08711400Z"{{embed}}}
            """;
    }

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

        string language = Assert.Single(post.Langs);
        Assert.Equal("en", language);
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
        string language = Assert.Single(post.Langs);
        Assert.Equal("en", language);
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
        string language = Assert.Single(post.Langs);
        Assert.Equal("en", language);
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

        string language = Assert.Single(post.Langs);
        Assert.Equal("de", language);
        Assert.Equal(DateTimeOffset.Parse("2026-05-21T17:41:14.270Z"), post.CreatedAt);
        Assert.NotNull(post.Facets);
        Facet facet = Assert.Single(post.Facets);
        Assert.Equal(0, facet.Index!.ByteStart);
        Assert.Equal(41, facet.Index!.ByteEnd);
        Assert.Single(facet.Features);

        LinkFacetFeature link = Assert.IsType<LinkFacetFeature>(facet.Features!.ElementAt(0));
        Assert.Equal("https://estrattonbailey.pckt.blog/test-post-bn5bcy2", link.Uri);

        EmbeddedExternal embeddedExternal = Assert.IsType<EmbeddedExternal>(post.EmbeddedRecord);
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

    [Fact]
    public void PostWithEmbeddedGalleryDeserializesCorrectly()
    {
        string json = """
            {
                    "text": "Long cat is long, sideways",
                    "$type": "app.bsky.feed.post",
                    "embed": {
                        "$type": "app.bsky.embed.gallery",
                        "items": [
                            {
                                "alt": "Long cat is long, sideways",
                                "$type": "app.bsky.embed.gallery#image",
                                "image": {
                                    "ref": {
                                        "$link": "bafkreidsj4g5hv2glhdr5yznsml2njqgeomc4hsx46fdyqlyyj6pzbhheu"
                                    },
                                    "size": 1432,
                                    "$type": "blob",
                                    "mimeType": "image/webp"
                                },
                                "aspectRatio": {
                                    "width": 142,
                                    "height": 138
                                }
                            },
                            {
                                "alt": "Long cat is long, sideways",
                                "$type": "app.bsky.embed.gallery#image",
                                "image": {
                                    "ref": {
                                        "$link": "bafkreierjvtgtcqt53o5rfrlahctdeiczekegsxpo5nyxhn4qywgghiln4"
                                    },
                                    "size": 1260,
                                    "$type": "blob",
                                    "mimeType": "image/webp"
                                },
                                "aspectRatio": {
                                    "width": 198,
                                    "height": 138
                                }
                            },
                            {
                                "alt": "Long cat is long, sideways",
                                "$type": "app.bsky.embed.gallery#image",
                                "image": {
                                    "ref": {
                                        "$link": "bafkreierjvtgtcqt53o5rfrlahctdeiczekegsxpo5nyxhn4qywgghiln4"
                                    },
                                    "size": 1260,
                                    "$type": "blob",
                                    "mimeType": "image/webp"
                                },
                                "aspectRatio": {
                                    "width": 198,
                                    "height": 138
                                }
                            },
                            {
                                "alt": "Long cat is long, sideways",
                                "$type": "app.bsky.embed.gallery#image",
                                "image": {
                                    "ref": {
                                        "$link": "bafkreierjvtgtcqt53o5rfrlahctdeiczekegsxpo5nyxhn4qywgghiln4"
                                    },
                                    "size": 1260,
                                    "$type": "blob",
                                    "mimeType": "image/webp"
                                },
                                "aspectRatio": {
                                    "width": 198,
                                    "height": 138
                                }
                            },
                            {
                                "alt": "Long cat is long, sideways",
                                "$type": "app.bsky.embed.gallery#image",
                                "image": {
                                    "ref": {
                                        "$link": "bafkreierjvtgtcqt53o5rfrlahctdeiczekegsxpo5nyxhn4qywgghiln4"
                                    },
                                    "size": 1260,
                                    "$type": "blob",
                                    "mimeType": "image/webp"
                                },
                                "aspectRatio": {
                                    "width": 198,
                                    "height": 138
                                }
                            },
                            {
                                "alt": "Long cat is long, sideways",
                                "$type": "app.bsky.embed.gallery#image",
                                "image": {
                                    "ref": {
                                        "$link": "bafkreierjvtgtcqt53o5rfrlahctdeiczekegsxpo5nyxhn4qywgghiln4"
                                    },
                                    "size": 1260,
                                    "$type": "blob",
                                    "mimeType": "image/webp"
                                },
                                "aspectRatio": {
                                    "width": 198,
                                    "height": 138
                                }
                            },
                            {
                                "alt": "Long cat is long, sideways",
                                "$type": "app.bsky.embed.gallery#image",
                                "image": {
                                    "ref": {
                                        "$link": "bafkreierjvtgtcqt53o5rfrlahctdeiczekegsxpo5nyxhn4qywgghiln4"
                                    },
                                    "size": 1260,
                                    "$type": "blob",
                                    "mimeType": "image/webp"
                                },
                                "aspectRatio": {
                                    "width": 198,
                                    "height": 138
                                }
                            },
                            {
                                "alt": "Long cat is long, sideways",
                                "$type": "app.bsky.embed.gallery#image",
                                "image": {
                                    "ref": {
                                        "$link": "bafkreierjvtgtcqt53o5rfrlahctdeiczekegsxpo5nyxhn4qywgghiln4"
                                    },
                                    "size": 1260,
                                    "$type": "blob",
                                    "mimeType": "image/webp"
                                },
                                "aspectRatio": {
                                    "width": 198,
                                    "height": 138
                                }
                            },
                            {
                                "alt": "Long cat is long, sideways",
                                "$type": "app.bsky.embed.gallery#image",
                                "image": {
                                    "ref": {
                                        "$link": "bafkreigfkwp7thnpirniwjombepq2iyuqqtauu6qazr3yeqgrd567d5vue"
                                    },
                                    "size": 1398,
                                    "$type": "blob",
                                    "mimeType": "image/webp"
                                },
                                "aspectRatio": {
                                    "width": 164,
                                    "height": 138
                                }
                            }
                        ]
                    },
                    "createdAt": "2026-06-11T13:58:57.7406626+00:00"
            }
            """;

        Post? post = JsonSerializer.Deserialize<Post>(json, BlueskyServer.BlueskyJsonSerializerOptions);

        Assert.NotNull(post);
        Assert.IsType<EmbeddedGallery>(post.EmbeddedRecord);
    }
}