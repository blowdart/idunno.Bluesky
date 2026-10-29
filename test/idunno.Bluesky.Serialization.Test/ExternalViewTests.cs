// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

using idunno.Bluesky.Embed;
using idunno.Bluesky.Embed.External;

namespace idunno.Bluesky.Serialization.Test;

[ExcludeFromCodeCoverage]
public class ExternalViewTests
{
    [Fact]
    public void ArticleViewDeserializesAndSerializesTypedData()
    {
        const string json = """
            {
              "$type": "app.bsky.embed.external#viewArticle",
              "uri": "https://example.com/article",
              "title": "Article title",
              "description": "Article description",
              "image": "https://example.com/article.jpg",
              "readingTime": 6,
              "likeCount": 3,
              "publisher": {
                "$type": "app.bsky.embed.external#viewArticlePublication",
                "uri": "https://example.com",
                "title": "Publication",
                "description": "Publication description",
                "theme": {
                  "$type": "app.bsky.embed.external#viewArticlePublicationTheme",
                  "background": "#ffffff",
                  "futureThemeField": "preserved"
                }
              },
              "labels": [],
              "associatedRefs": [],
              "associatedProfiles": [],
              "likers": [],
              "futureField": { "preserved": true }
            }
            """;

        using JsonDocument document = JsonDocument.Parse(json);
        EmbeddedExternalViewResponse response = CreateResponse(document.RootElement);

        JsonTypeInfo<ArticleView> articleTypeInfo = GetTypeInfo<ArticleView>();
        ArticleView article = Assert.IsType<ArticleView>(response.DeserializeData(articleTypeInfo));
        Assert.Equal("https://example.com/article", article.Identity);
        Assert.Equal("Article title", article.Title);
        Assert.Equal(new Uri("https://example.com/article.jpg"), article.Image);
        Assert.Equal(6, article.ReadingTime);
        Assert.Equal(3, article.LikeCount);
        Assert.Equal("Publication", article.Publisher?.Title);
        Assert.Equal("#ffffff", article.Publisher?.Theme?.Background);

        string serialized = JsonSerializer.Serialize(article, articleTypeInfo);
        using JsonDocument serializedDocument = JsonDocument.Parse(serialized);
        Assert.Equal("app.bsky.embed.external#viewArticle", serializedDocument.RootElement.GetProperty("$type").GetString());
        Assert.True(serializedDocument.RootElement.GetProperty("futureField").GetProperty("preserved").GetBoolean());
        Assert.Equal("app.bsky.embed.external#viewArticle", response.Data?.GetProperty("$type").GetString());
    }

    [Fact]
    public void ArticlePublicationAndThemeRoundTripTheirLexiconDiscriminators()
    {
        ArticlePublicationView? publication = JsonSerializer.Deserialize(
            """
            {
              "$type": "app.bsky.embed.external#viewArticlePublication",
              "uri": "https://example.com",
              "title": "Publication",
              "description": "Description",
              "logo": "https://example.com/logo.png",
              "image": "https://example.com/image.png",
              "subscriptionCount": 12,
              "subscribers": [],
              "theme": {
                "$type": "app.bsky.embed.external#viewArticlePublicationTheme",
                "foreground": "#222222",
                "accentForeground": "#ffffff",
                "futureThemeField": "preserved"
              }
            }
            """,
            GetTypeInfo<ArticlePublicationView>());

        Assert.NotNull(publication);
        Assert.Equal(12, publication.SubscriptionCount);
        Assert.Equal("#222222", publication.Theme?.Foreground);
        Assert.Equal("#ffffff", publication.Theme?.AccentForeground);

        using JsonDocument publicationDocument = JsonDocument.Parse(JsonSerializer.Serialize(publication, GetTypeInfo<ArticlePublicationView>()));
        Assert.Equal("app.bsky.embed.external#viewArticlePublication", publicationDocument.RootElement.GetProperty("$type").GetString());
        Assert.Equal(
            "app.bsky.embed.external#viewArticlePublicationTheme",
            publicationDocument.RootElement.GetProperty("theme").GetProperty("$type").GetString());
        Assert.Equal(
            "preserved",
            publicationDocument.RootElement.GetProperty("theme").GetProperty("futureThemeField").GetString());
    }

    [Fact]
    public void GalleryRetainsUnrecognizedItemsWhenRoundTripped()
    {
        const string json = """
            {
              "$type": "app.bsky.embed.external#viewGallery",
              "uri": "https://example.com/gallery",
              "title": "Gallery",
              "description": "Description",
              "items": [
                {
                  "$type": "app.bsky.embed.external#viewGalleryImage",
                  "thumbnail": "https://example.com/thumb.jpg",
                  "fullsize": "https://example.com/full.jpg",
                  "alt": "Image description",
                  "aspectRatio": { "width": 4, "height": 3 },
                  "futureImageField": { "retained": true }
                },
                {
                  "$type": "app.bsky.embed.external#futureGalleryItem",
                  "value": { "kept": true }
                }
              ]
            }
            """;

        JsonTypeInfo<GalleryView> galleryTypeInfo = GetTypeInfo<GalleryView>();
        GalleryView? gallery = JsonSerializer.Deserialize(json, galleryTypeInfo);

        Assert.NotNull(gallery);
        Assert.Equal(2, gallery.Items.Count);
        GalleryImageView image = Assert.IsType<GalleryImageView>(gallery.Items.First().Deserialize(GetTypeInfo<GalleryImageView>()));
        Assert.Equal(new Uri("https://example.com/full.jpg"), image.FullSize);
        Assert.Equal(4, image.AspectRatio?.Width);

        string serialized = JsonSerializer.Serialize(gallery, galleryTypeInfo);
        using JsonDocument document = JsonDocument.Parse(serialized);
        JsonElement[] items = [.. document.RootElement.GetProperty("items").EnumerateArray()];
        Assert.Equal("app.bsky.embed.external#futureGalleryItem", items[1].GetProperty("$type").GetString());
        Assert.True(items[1].GetProperty("value").GetProperty("kept").GetBoolean());
        Assert.True(items[0].GetProperty("futureImageField").GetProperty("retained").GetBoolean());
    }

    [Fact]
    public void LivestreamViewPreservesRequiredAndOptionalFields()
    {
        LivestreamView? livestream = JsonSerializer.Deserialize(
            """
            {
              "$type": "app.bsky.embed.external#viewLivestream",
              "uri": "https://example.com/live",
              "title": "Live now",
              "description": "Description",
              "active": true,
              "image": "https://example.com/live.jpg",
              "startedAt": "2026-10-15T12:00:00Z"
            }
            """,
            GetTypeInfo<LivestreamView>());

        Assert.NotNull(livestream);
        Assert.True(livestream.Active);
        Assert.Equal(new DateTimeOffset(2026, 10, 15, 12, 0, 0, TimeSpan.Zero), livestream.StartedAt);

        using JsonDocument document = JsonDocument.Parse(JsonSerializer.Serialize(livestream, GetTypeInfo<LivestreamView>()));
        Assert.Equal("app.bsky.embed.external#viewLivestream", document.RootElement.GetProperty("$type").GetString());
        Assert.True(document.RootElement.GetProperty("active").GetBoolean());
    }

    [Fact]
    public void TypedProjectionLeavesUnknownDiscriminatorDataAvailable()
    {
        const string json = """
            {
              "$type": "app.bsky.embed.external#futureView",
              "value": { "kept": true }
            }
            """;

        using JsonDocument document = JsonDocument.Parse(json);
        EmbeddedExternalViewResponse response = CreateResponse(document.RootElement);

        Assert.Throws<JsonException>(() => response.DeserializeData(GetTypeInfo<ArticleView>()));

        using JsonDocument retained = JsonDocument.Parse(response.Data?.GetRawText() ?? throw new InvalidOperationException());
        Assert.Equal("app.bsky.embed.external#futureView", retained.RootElement.GetProperty("$type").GetString());
        Assert.True(retained.RootElement.GetProperty("value").GetProperty("kept").GetBoolean());
    }

    [Theory]
    [MemberData(nameof(NullCollectionEntries))]
    public void ViewModelsRejectNullCollectionElements(string type, string propertyName)
    {
        string json = CreateJsonWithNullCollectionEntry(type, propertyName);

        Assert.Throws<JsonException>(() => DeserializeView(type, json));
    }

    [Theory]
    [MemberData(nameof(NullRequiredFields))]
    public void ViewModelsRejectNullRequiredFields(string type, string propertyName)
    {
        string json = CreateJsonWithNullRequiredField(type, propertyName);

        Assert.Throws<JsonException>(() => DeserializeView(type, json));
    }

    [Theory]
    [MemberData(nameof(IdentityUris))]
    public void ViewModelsPreserveHttpAndAtUriIdentityValues(string type, string uri)
    {
        string json = CreateJsonWithIdentityUri(type, uri);

        object? view = DeserializeView(type, json);
        Assert.NotNull(view);
        Assert.Equal(uri, view switch
        {
            ArticleView article => article.Identity,
            ArticlePublicationView publication => publication.Identity,
            GalleryView gallery => gallery.Identity,
            LivestreamView livestream => livestream.Identity,
            _ => throw new ArgumentOutOfRangeException(nameof(type))
        });

        using JsonDocument serializedDocument = JsonDocument.Parse(SerializeView(type, view));
        JsonElement serialized = serializedDocument.RootElement;
        Assert.Equal(uri, serialized.GetProperty("uri").GetString());
    }

    [Fact]
    public void GalleryRejectsNullUnionItems()
    {
        const string json = """
            {
              "$type": "app.bsky.embed.external#viewGallery",
              "uri": "https://example.com/gallery",
              "title": "Gallery",
              "description": "Description",
              "items": [null]
            }
            """;

        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize(json, GetTypeInfo<GalleryView>()));
    }

    public static IEnumerable<TheoryDataRow<string, string>> NullCollectionEntries()
    {
        foreach (string property in new[] { "labels", "associatedRefs", "associatedProfiles", "likers" })
        {
            yield return new("article", property);
            yield return new("gallery", property);
        }

        foreach (string property in new[] { "labels", "associatedRefs", "associatedProfiles", "subscribers" })
        {
            yield return new("publication", property);
        }

        foreach (string property in new[] { "labels", "associatedRefs", "associatedProfiles" })
        {
            yield return new("livestream", property);
        }
    }

    public static IEnumerable<TheoryDataRow<string, string>> NullRequiredFields()
    {
        foreach (string type in new[] { "article", "publication", "gallery", "livestream" })
        {
            yield return new(type, "uri");
            yield return new(type, "title");
            yield return new(type, "description");
        }

        yield return new("galleryImage", "thumbnail");
        yield return new("galleryImage", "fullsize");
    }

    public static IEnumerable<TheoryDataRow<string, string>> IdentityUris()
    {
        string[] types = ["article", "publication", "gallery", "livestream"];
        string[] uris = ["at://did:plc:7rkdalcyovirwkpzgzk74cje/social.grain.gallery/3mwvtii4k5ddt", "https://example.com/item"];

        foreach (string type in types)
        {
            foreach (string uri in uris)
            {
                yield return new(type, uri);
            }
        }
    }

    private static EmbeddedExternalViewResponse CreateResponse(JsonElement data)
    {
        return new EmbeddedExternalViewResponse(null, data.Clone(), null, null, null, null);
    }

    private static JsonTypeInfo<T> GetTypeInfo<T>()
    {
        return (JsonTypeInfo<T>)BlueskyJsonSerializerOptions.Default.GetTypeInfo(typeof(T));
    }

    private static string CreateJsonWithNullCollectionEntry(string type, string propertyName)
    {
        string discriminator = type switch
        {
            "article" => "app.bsky.embed.external#viewArticle",
            "publication" => "app.bsky.embed.external#viewArticlePublication",
            "gallery" => "app.bsky.embed.external#viewGallery",
            "livestream" => "app.bsky.embed.external#viewLivestream",
            _ => throw new ArgumentOutOfRangeException(nameof(type))
        };

        string required = type == "gallery" && propertyName != "items" ? ", \"items\": []" : string.Empty;

        return $$"""
            {
              "$type": "{{discriminator}}",
              "uri": "https://example.com",
              "title": "Title",
              "description": "Description",
              "{{propertyName}}": [null]{{required}}
              {{(type == "livestream" ? ", \"active\": true" : string.Empty)}}
            }
            """;
    }

    private static string CreateJsonWithNullRequiredField(string type, string propertyName)
    {
        string discriminator = type switch
        {
            "article" => "app.bsky.embed.external#viewArticle",
            "publication" => "app.bsky.embed.external#viewArticlePublication",
            "gallery" => "app.bsky.embed.external#viewGallery",
            "galleryImage" => "app.bsky.embed.external#viewGalleryImage",
            "livestream" => "app.bsky.embed.external#viewLivestream",
            _ => throw new ArgumentOutOfRangeException(nameof(type))
        };

        if (type == "galleryImage")
        {
            string thumbnail = propertyName == "thumbnail" ? "null" : "\"https://example.com/thumb.jpg\"";
            string fullsize = propertyName == "fullsize" ? "null" : "\"https://example.com/fullsize.jpg\"";

            return $$"""
                {
                  "$type": "{{discriminator}}",
                  "thumbnail": {{thumbnail}},
                  "fullsize": {{fullsize}}
                }
                """;
        }

        string uri = propertyName == "uri" ? "null" : "\"https://example.com\"";
        string title = propertyName == "title" ? "null" : "\"Title\"";
        string description = propertyName == "description" ? "null" : "\"Description\"";
        string required = type switch
        {
            "gallery" => ", \"items\": []",
            "livestream" => ", \"active\": true",
            _ => string.Empty
        };

        return $$"""
            {
              "$type": "{{discriminator}}",
              "uri": {{uri}},
              "title": {{title}},
              "description": {{description}}{{required}}
            }
            """;
    }

    private static string CreateJsonWithIdentityUri(string type, string uri)
    {
        string discriminator = type switch
        {
            "article" => "app.bsky.embed.external#viewArticle",
            "publication" => "app.bsky.embed.external#viewArticlePublication",
            "gallery" => "app.bsky.embed.external#viewGallery",
            "livestream" => "app.bsky.embed.external#viewLivestream",
            _ => throw new ArgumentOutOfRangeException(nameof(type))
        };
        string required = type switch
        {
            "gallery" => ", \"items\": []",
            "livestream" => ", \"active\": true",
            _ => string.Empty
        };

        return $$"""
            {
              "$type": "{{discriminator}}",
              "uri": "{{uri}}",
              "title": "Title",
              "description": "Description"{{required}}
            }
            """;
    }

    private static string SerializeView(string type, object view)
    {
        return type switch
        {
            "article" => JsonSerializer.Serialize((ArticleView)view, GetTypeInfo<ArticleView>()),
            "publication" => JsonSerializer.Serialize((ArticlePublicationView)view, GetTypeInfo<ArticlePublicationView>()),
            "gallery" => JsonSerializer.Serialize((GalleryView)view, GetTypeInfo<GalleryView>()),
            "livestream" => JsonSerializer.Serialize((LivestreamView)view, GetTypeInfo<LivestreamView>()),
            _ => throw new ArgumentOutOfRangeException(nameof(type))
        };
    }

    private static object? DeserializeView(string type, string json)
    {
        return type switch
        {
            "article" => JsonSerializer.Deserialize(json, GetTypeInfo<ArticleView>()),
            "publication" => JsonSerializer.Deserialize(json, GetTypeInfo<ArticlePublicationView>()),
            "gallery" => JsonSerializer.Deserialize(json, GetTypeInfo<GalleryView>()),
            "galleryImage" => JsonSerializer.Deserialize(json, GetTypeInfo<GalleryImageView>()),
            "livestream" => JsonSerializer.Deserialize(json, GetTypeInfo<LivestreamView>()),
            _ => throw new ArgumentOutOfRangeException(nameof(type))
        };
    }
}
