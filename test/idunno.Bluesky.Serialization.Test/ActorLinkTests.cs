// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;

using idunno.AtProto;
using idunno.AtProto.Labels;
using idunno.AtProto.Repo;
using idunno.Bluesky.Actor;
using idunno.Bluesky.Record;

namespace idunno.Bluesky.Serialization.Test;

[ExcludeFromCodeCoverage]
public class ActorLinkTests
{
    [Fact]
    public void LinkRecordSerializesItsDiscriminatorAndSelfLabels()
    {
        Link link = new(
            new Uri("https://example.com"),
            "Example",
            icon: null,
            labels: new SelfLabels(new SelfLabel("test")),
            createdAt: DateTimeOffset.Parse("2025-04-21T10:49:31.969Z"));

        string json = JsonSerializer.Serialize(link, BlueskyServer.BlueskyJsonSerializerOptions);
        using JsonDocument document = JsonDocument.Parse(json);

        Assert.Equal("app.bsky.actor.link", document.RootElement.GetProperty("$type").GetString());
        Assert.Equal("https://example.com", document.RootElement.GetProperty("url").GetString());
        Assert.Equal("test", document.RootElement.GetProperty("labels").GetProperty("values")[0].GetProperty("val").GetString());
    }

    [Fact]
    public void LinkRepositoryRecordDeserializesWithSourceGeneratedJsonContext()
    {
        string json = """
            {
                "uri": "at://did:plc:ewvi7nxzyoun6zhxrhs64oiz/app.bsky.actor.link/test",
                "cid": "bafyreihszwqbe7nxh2mv4o2wk365yanh3p3zimbunfrjdtq7gcmmzohx6i",
                "value": {
                    "$type": "app.bsky.actor.link",
                    "url": "https://example.com",
                    "title": "Example",
                    "createdAt": "2025-04-21T10:49:31.969Z"
                }
            }
            """;

        AtProtoRepositoryRecord<Link>? actual =
            JsonSerializer.Deserialize<AtProtoRepositoryRecord<Link>>(json, BlueskyServer.BlueskyJsonSerializerOptions);

        Assert.NotNull(actual);
        Assert.Equal("https://example.com/", actual.Value.Url.ToString());
        Assert.Equal("Example", actual.Value.Title);
    }

    [Fact]
    public void DetailedProfileDeserializesProfileLinksAndLabels()
    {
        string json = """
            {
                "did": "did:plc:ewvi7nxzyoun6zhxrhs64oiz",
                "handle": "example.com",
                "links": [
                    {
                        "uri": "at://did:plc:ewvi7nxzyoun6zhxrhs64oiz/app.bsky.actor.link/test",
                        "cid": "bafyreihszwqbe7nxh2mv4o2wk365yanh3p3zimbunfrjdtq7gcmmzohx6i",
                        "url": "https://example.com",
                        "title": "Example",
                        "labels": [
                            {
                                "src": "did:plc:ewvi7nxzyoun6zhxrhs64oiz",
                                "uri": "at://did:plc:ewvi7nxzyoun6zhxrhs64oiz/app.bsky.actor.link/test",
                                "val": "test",
                                "cts": "2025-04-21T10:49:31.969Z"
                            }
                        ]
                    }
                ]
            }
            """;

        ProfileViewDetailed? actual =
            JsonSerializer.Deserialize<ProfileViewDetailed>(json, BlueskyServer.BlueskyJsonSerializerOptions);

        Assert.NotNull(actual);
        ProfileLinkView link = Assert.Single(actual.Links!);
        Assert.Equal("https://example.com/", link.Url.ToString());
        Assert.Equal("Example", link.Title);
        Assert.Equal("test", Assert.Single(link.Labels!).Value);
    }

    [Theory]
    [InlineData(40, true)]
    [InlineData(41, false)]
    public void LinkTitleEnforcesGraphemeAndByteLimits(int length, bool valid)
    {
        string title = string.Concat(Enumerable.Repeat("\U0001F1FA\U0001F1F8", length));
        Link link = new(new Uri("https://example.com"));

        if (valid)
        {
            Assert.Equal(title, (link with { Title = title }).Title);
        }
        else
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => link with { Title = title });
        }
    }

    [Fact]
    public void LinkTitleRejectsTooManyBytesEvenWithinGraphemeLimit()
    {
        string title = "a" + new string('\u0301', 160);
        Assert.Throws<ArgumentOutOfRangeException>(() => new Link(new Uri("https://example.com")) { Title = title });
    }

    [Theory]
    [InlineData("image/png", 100000, true)]
    [InlineData("image/jpeg", 100000, true)]
    [InlineData("image/webp", 100000, true)]
    [InlineData("image/png", 100001, false)]
    [InlineData("image/svg+xml", 100, false)]
    public void LinkIconEnforcesLexiconConstraints(string mimeType, int size, bool valid)
    {
        Blob blob = new(
            new CidLink(new Cid("bafyreihszwqbe7nxh2mv4o2wk365yanh3p3zimbunfrjdtq7gcmmzohx6i")),
            mimeType,
            size);
        if (valid)
        {
            Assert.Same(blob, new Link(new Uri("https://example.com")) { Icon = blob }.Icon);
        }
        else if (size > 100000)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new Link(new Uri("https://example.com")) { Icon = blob });
        }
        else
        {
            Assert.Throws<ArgumentException>(() => new Link(new Uri("https://example.com")) { Icon = blob });
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(10)]
    [InlineData(11)]
    public void ProfileLinksEnforceMaximumAndRetainOrder(int count)
    {
        StrongReference[] links = Enumerable.Range(0, count).Select(index => new StrongReference(
            new AtUri($"at://did:plc:ewvi7nxzyoun6zhxrhs64oiz/app.bsky.actor.link/{index}"),
            new Cid("bafyreihszwqbe7nxh2mv4o2wk365yanh3p3zimbunfrjdtq7gcmmzohx6i"))).ToArray();
        Profile profile = new();
        if (count > 10)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => profile.Links = links);
        }
        else
        {
            profile.Links = links;
            Assert.Equal(links, profile.Links);
            string json = JsonSerializer.Serialize(profile, BlueskyServer.BlueskyJsonSerializerOptions);
            Profile actual = JsonSerializer.Deserialize<Profile>(json, BlueskyServer.BlueskyJsonSerializerOptions)!;
            Assert.Equal(links, actual.Links);
            if (count > 0)
            {
                links[0] = null!;
                Assert.NotNull(profile.Links.First());
            }
        }
    }

    [Fact]
    public void ProfileLinksRejectNullEntries()
    {
        Profile profile = new();
        Assert.Throws<ArgumentNullException>(() => profile.Links = new StrongReference[] { null! });
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ProfileLinkViewsRejectNullEntriesAtEachCollectionLevel(bool nullLink)
    {
        string item = nullLink
            ? "null"
            : """
              {"uri":"at://did:plc:ewvi7nxzyoun6zhxrhs64oiz/app.bsky.actor.link/test",
               "cid":"bafyreihszwqbe7nxh2mv4o2wk365yanh3p3zimbunfrjdtq7gcmmzohx6i",
               "url":"https://example.com","labels":[null]}
              """;
        string json = $$"""
            {"did":"did:plc:ewvi7nxzyoun6zhxrhs64oiz","handle":"example.com","links":[{{item}}]}
            """;

        Assert.Throws<ArgumentNullException>(() =>
            JsonSerializer.Deserialize<ProfileViewDetailed>(json, BlueskyServer.BlueskyJsonSerializerOptions));
    }

    [Fact]
    public void LinkDeserializesThroughBothRecordBases()
    {
        string json = """
            {"$type":"app.bsky.actor.link","url":"https://example.com","createdAt":"2026-10-15T00:00:00Z"}
            """;
        Assert.IsType<Link>(JsonSerializer.Deserialize<BlueskyRecord>(json, BlueskyServer.BlueskyJsonSerializerOptions));
        Assert.IsType<Link>(JsonSerializer.Deserialize<BlueskyTimestampedRecord>(json, BlueskyServer.BlueskyJsonSerializerOptions));
    }
}
