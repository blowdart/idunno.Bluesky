// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;

using idunno.AtProto.Repo;
using Standard.Site;

namespace idunno.Bluesky.Serialization.Test;

public class StandardSiteTests
{
    private readonly JsonSerializerOptions _jsonSerializationOptions = new(JsonSerializerDefaults.Web)
    {
        AllowOutOfOrderMetadataProperties = true
    };

    [Fact]
    public void DocumentDeserializesCorrectly()
    {
        string json = """
            {
                "uri": "at://did:plc:ragtjsm2j2vknwkz3zp4oxrd/site.standard.document/3ly4ieizo2c2p",
                "cid": "bafyreig6yrgjt3bhfagxlb7lzxktmpnyd2h5u35jezoysifiqjpxvu2z64",
                "value": {
                    "path": "/3ly4ieizo2c2p",
                    "site": "at://did:plc:ragtjsm2j2vknwkz3zp4oxrd/site.standard.publication/3ly4hnkatvc2p",
                    "$type": "site.standard.document",
                    "title": "Title",
                    "content": {
                        "$type": "pub.leaflet.content",
                        "pages": [
                            {
                                "$type": "pub.leaflet.pages.linearDocument",
                                "blocks": [
                                    {
                                        "$type": "pub.leaflet.pages.linearDocument#block",
                                        "block": {
                                            "$type": "pub.leaflet.blocks.text",
                                            "facets": [],
                                            "plaintext": "Hello world."
                                            }
                                    }
                                ]
                            }
                        ]
                    },
                    "description": "It actually calms me down",
                    "publishedAt": "2025-09-05T20:21:24.309Z"
                }
            }
            """;

        AtProtoRepositoryRecord<Document>? documentRecord = JsonSerializer.Deserialize<AtProtoRepositoryRecord<Document>>(json, options: _jsonSerializationOptions);
        Assert.NotNull(documentRecord);
        Assert.NotNull(documentRecord.Value);
        Assert.Equal("/3ly4ieizo2c2p", documentRecord.Value.Path);
        Assert.Equal("at://did:plc:ragtjsm2j2vknwkz3zp4oxrd/site.standard.publication/3ly4hnkatvc2p", documentRecord.Value.Site);
        Assert.Equal("Title", documentRecord.Value.Title);
        Assert.Equal("It actually calms me down", documentRecord.Value.Description);
        Assert.Equal(DateTimeOffset.Parse("2025-09-05T20:21:24.309Z"), documentRecord.Value.PublishedAt);
        Assert.NotNull(documentRecord.Value.Content);
    }

    [Fact]
    public void DocumentDeserializesCorrectlyWithDeserializationOptions()
    {
        string json = """
            {
                "uri": "at://did:plc:ragtjsm2j2vknwkz3zp4oxrd/site.standard.document/3ly4ieizo2c2p",
                "cid": "bafyreig6yrgjt3bhfagxlb7lzxktmpnyd2h5u35jezoysifiqjpxvu2z64",
                "value": {
                    "path": "/3ly4ieizo2c2p",
                    "site": "at://did:plc:ragtjsm2j2vknwkz3zp4oxrd/site.standard.publication/3ly4hnkatvc2p",
                    "$type": "site.standard.document",
                    "title": "Title",
                    "content": {
                        "$type": "pub.leaflet.content",
                        "pages": [
                            {
                                "$type": "pub.leaflet.pages.linearDocument",
                                "blocks": [
                                    {
                                        "$type": "pub.leaflet.pages.linearDocument#block",
                                        "block": {
                                            "$type": "pub.leaflet.blocks.text",
                                            "facets": [],
                                            "plaintext": "Hello world."
                                            }
                                    }
                                ]
                            }
                        ]
                    },
                    "description": "It actually calms me down",
                    "publishedAt": "2025-09-05T20:21:24.309Z"
                }
            }
            """;

        AtProtoRepositoryRecord<Document>? documentRecord = JsonSerializer.Deserialize<AtProtoRepositoryRecord<Document>>(json, options: BlueskyJsonSerializerOptions.Options);
        Assert.NotNull(documentRecord);
        Assert.NotNull(documentRecord.Value);
        Assert.Equal("/3ly4ieizo2c2p", documentRecord.Value.Path);
        Assert.Equal("at://did:plc:ragtjsm2j2vknwkz3zp4oxrd/site.standard.publication/3ly4hnkatvc2p", documentRecord.Value.Site);
        Assert.Equal("Title", documentRecord.Value.Title);
        Assert.Equal("It actually calms me down", documentRecord.Value.Description);
        Assert.Equal(DateTimeOffset.Parse("2025-09-05T20:21:24.309Z"), documentRecord.Value.PublishedAt);
        Assert.NotNull(documentRecord.Value.Content);
    }

    [Fact]
    public void PublicationDeserializesCorrectly()
    {
        string json = """
            {
                "uri": "at://did:plc:ragtjsm2j2vknwkz3zp4oxrd/pub.leaflet.publication/3ly4hnkatvc2p",
                "cid": "bafyreial6vjrkka54beyged3qlcnwn6mbn3lbcwgm6fr6nujzuoqpbrfqe",
                "value": {
                    "icon": {
                        "ref": {
                            "$link": "bafkreighxlygswtkinvkvcm67igorlys6ldigjftn2zzvy35d6fbnr6rbi"
                        },
                        "size": 273897,
                        "$type": "blob",
                        "mimeType": "image/png"
                    },
                    "name": "Paul's Leaflets",
                    "$type": "pub.leaflet.publication",
                    "url": "https://pfrazee.leaflet.pub",
                    "preferences": {
                        "showComments": true,
                        "showInDiscover": true
                    }
                }
            }
            """;

        AtProtoRepositoryRecord<Publication>? publicationRecord = JsonSerializer.Deserialize<AtProtoRepositoryRecord<Publication>>(json, options: _jsonSerializationOptions);
        Assert.NotNull(publicationRecord);
        Assert.NotNull(publicationRecord.Value);

        Assert.Equal("Paul's Leaflets", publicationRecord.Value.Name);
        Assert.Equal(new Uri("https://pfrazee.leaflet.pub"), publicationRecord.Value.Url);
        Assert.Equal(273897, publicationRecord.Value.Icon!.Size);
        Assert.Equal("image/png", publicationRecord.Value.Icon.MimeType);
        Assert.Equal("bafkreighxlygswtkinvkvcm67igorlys6ldigjftn2zzvy35d6fbnr6rbi", publicationRecord.Value.Icon.Reference!.Link);
        Assert.True(publicationRecord.Value.Preferences!.ShowInDiscover);
        Assert.True(publicationRecord.Value.Preferences!.ExtensionData!.ContainsKey("showComments"));
        Assert.True(publicationRecord.Value.Preferences!.ExtensionData!["showComments"].GetBoolean());
    }

    [Fact]
    public void PublicationDeserializesCorrectlyWithDeserializationOptions()
    {
        string json = """
            {
                "uri": "at://did:plc:ragtjsm2j2vknwkz3zp4oxrd/pub.leaflet.publication/3ly4hnkatvc2p",
                "cid": "bafyreial6vjrkka54beyged3qlcnwn6mbn3lbcwgm6fr6nujzuoqpbrfqe",
                "value": {
                    "icon": {
                        "ref": {
                            "$link": "bafkreighxlygswtkinvkvcm67igorlys6ldigjftn2zzvy35d6fbnr6rbi"
                        },
                        "size": 273897,
                        "$type": "blob",
                        "mimeType": "image/png"
                    },
                    "name": "Paul's Leaflets",
                    "$type": "pub.leaflet.publication",
                    "url": "https://pfrazee.leaflet.pub",
                    "preferences": {
                        "showComments": true,
                        "showInDiscover": true
                    }
                }
            }
            """;

        AtProtoRepositoryRecord<Publication>? publicationRecord = JsonSerializer.Deserialize<AtProtoRepositoryRecord<Publication>>(json, options: BlueskyJsonSerializerOptions.Options);
        Assert.NotNull(publicationRecord);
        Assert.NotNull(publicationRecord.Value);

        Assert.Equal("Paul's Leaflets", publicationRecord.Value.Name);
        Assert.Equal(new Uri("https://pfrazee.leaflet.pub"), publicationRecord.Value.Url);
        Assert.Equal(273897, publicationRecord.Value.Icon!.Size);
        Assert.Equal("image/png", publicationRecord.Value.Icon.MimeType);
        Assert.Equal("bafkreighxlygswtkinvkvcm67igorlys6ldigjftn2zzvy35d6fbnr6rbi", publicationRecord.Value.Icon.Reference!.Link);
        Assert.True(publicationRecord.Value.Preferences!.ShowInDiscover);
        Assert.True(publicationRecord.Value.Preferences!.ExtensionData!.ContainsKey("showComments"));
        Assert.True(publicationRecord.Value.Preferences!.ExtensionData!["showComments"].GetBoolean());
    }

}
