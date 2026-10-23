// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;

using idunno.AtProto;
using idunno.AtProto.Repo;
using idunno.Bluesky.Actor;
using idunno.Bluesky.Record;

namespace idunno.Bluesky.Serialization.Test;

[ExcludeFromCodeCoverage]
public class CapturedProfileLinkTests
{
    // Public blowdart.me responses captured on 2026-10-22. Tests do not contact the network.
    // com.atproto.repo.listRecords on https://porcini.us-east.host.bsky.network
    private const string LinkRecordsJson = """
        {
          "records": [
            {
              "uri": "at://did:plc:hfgp6pj3akhqxntgqwramlbg/app.bsky.actor.link/3myiq3rqwe52t",
              "cid": "bafyreigym5qkku4ar35whduera6iqp7gc6ut3kmxzsuavnjp5sagdfpzjq",
              "value": {
                "url": "https://github.com/blowdart/",
                "icon": {
                  "ref": {"$link": "bafkreie6tiznvm2vey67ob4sjxpp67puvmlpt54yqypitvj4kzten7sloy"},
                  "size": 942,
                  "$type": "blob",
                  "mimeType": "image/png"
                },
                "$type": "app.bsky.actor.link",
                "title": "GitHub",
                "createdAt": "2026-10-22T22:37:50.232Z"
              }
            },
            {
              "uri": "at://did:plc:hfgp6pj3akhqxntgqwramlbg/app.bsky.actor.link/3myiq3rozsf2r",
              "cid": "bafyreiautstyjyp74zocyj5avivh7mcxk75htfcqtvu62ksig7jbmucw3y",
              "value": {
                "url": "https://jerryno.idunno.bot/",
                "icon": {
                  "ref": {"$link": "bafkreidvtsbtzlom43mmmj2l4bgcx5cngfyfcjjdw7ow6tvuenkj3hambu"},
                  "size": 9750,
                  "$type": "blob",
                  "mimeType": "image/png"
                },
                "$type": "app.bsky.actor.link",
                "title": "Jerry No",
                "createdAt": "2026-10-22T22:37:50.232Z"
              }
            },
            {
              "uri": "at://did:plc:hfgp6pj3akhqxntgqwramlbg/app.bsky.actor.link/3myiq3rojcq2i",
              "cid": "bafyreieu4xyqhebqzv2m644clcezg3obddavijncwgl54ogkbkzou7guze",
              "value": {
                "url": "https://idunno.org/",
                "icon": {
                  "ref": {"$link": "bafkreih3ablfr7vcjfxsz6auif4mmwaomqblolay6emuwfrb7twoppikcy"},
                  "size": 1112,
                  "$type": "blob",
                  "mimeType": "image/png"
                },
                "$type": "app.bsky.actor.link",
                "title": "Blog",
                "createdAt": "2026-10-22T22:37:50.231Z"
              }
            }
          ],
          "cursor": "3myiq3rojcq2i"
        }
        """;

    // com.atproto.repo.getRecord for app.bsky.actor.profile/self on the same PDS.
    private const string ProfileRecordJson = """
        {
          "uri": "at://did:plc:hfgp6pj3akhqxntgqwramlbg/app.bsky.actor.profile/self",
          "cid": "bafyreieveta7kj4qv6vty7zbw7qbd5ld546dlmfwb3uqmdk23ll43evafe",
          "value": {
            "$type": "app.bsky.actor.profile",
            "links": [
              {
                "cid": "bafyreieu4xyqhebqzv2m644clcezg3obddavijncwgl54ogkbkzou7guze",
                "uri": "at://did:plc:hfgp6pj3akhqxntgqwramlbg/app.bsky.actor.link/3myiq3rojcq2i"
              },
              {
                "cid": "bafyreigym5qkku4ar35whduera6iqp7gc6ut3kmxzsuavnjp5sagdfpzjq",
                "uri": "at://did:plc:hfgp6pj3akhqxntgqwramlbg/app.bsky.actor.link/3myiq3rqwe52t"
              },
              {
                "cid": "bafyreiautstyjyp74zocyj5avivh7mcxk75htfcqtvu62ksig7jbmucw3y",
                "uri": "at://did:plc:hfgp6pj3akhqxntgqwramlbg/app.bsky.actor.link/3myiq3rozsf2r"
              }
            ],
            "avatar": {
              "ref": {"$link": "bafkreicwjaromkjs4jrd5uqznacfgzvhnob2il5fwywxqopbnhfb74n27m"},
              "size": 156475,
              "$type": "blob",
              "mimeType": "image/jpeg"
            },
            "banner": {
              "ref": {"$link": "bafkreia3ww67kqsgkxy6bfgu4dxxyp52b3e2ghqbpoj7qt4iuupfx6c45a"},
              "size": 356256,
              "$type": "blob",
              "mimeType": "image/jpeg"
            },
            "website": "https://idunno.org",
            "pronouns": "He/Him",
            "pinnedPost": {
              "cid": "bafyreibtb55kfqsny3qk4c2puoqom4cu5zkpnp5xdwu5dud264b4efocyq",
              "uri": "at://did:plc:hfgp6pj3akhqxntgqwramlbg/app.bsky.feed.post/3l66cdbste424"
            },
            "description": "Security Curmudgeon for Microsoft .NET\n\nDo you really think work wants my social media opinions?\n\nNot nice, but kind - @medus4.com\n\n\uD83C\uDDEE\uD83C\uDDEA \uD83C\uDDEC\uD83C\uDDE7 \uD83C\uDDFA\uD83C\uDDF8",
            "displayName": "Barry Dorrans"
          }
        }
        """;

    // https://public.api.bsky.app/xrpc/app.bsky.actor.getProfile?actor=blowdart.me
    private const string DetailedProfileJson = """
        {
          "did": "did:plc:hfgp6pj3akhqxntgqwramlbg",
          "handle": "blowdart.me",
          "displayName": "Barry Dorrans",
          "pronouns": "He/Him",
          "avatar": "https://cdn.bsky.app/img/avatar/plain/did:plc:hfgp6pj3akhqxntgqwramlbg/bafkreicwjaromkjs4jrd5uqznacfgzvhnob2il5fwywxqopbnhfb74n27m",
          "associated": {
            "lists": 4,
            "feedgens": 1,
            "starterPacks": 0,
            "labeler": false,
            "chat": {"allowIncoming": "all", "allowGroupInvites": "following"},
            "activitySubscription": {"allowSubscriptions": "followers"},
            "germ": {"showButtonTo": "usersIFollow", "messageMeUrl": "https://landing.ger.mx/newUser"}
          },
          "labels": [],
          "createdAt": "2023-04-22T22:44:04.316Z",
          "description": "Security Curmudgeon for Microsoft .NET\n\nDo you really think work wants my social media opinions?\n\nNot nice, but kind - @medus4.com\n\n\uD83C\uDDEE\uD83C\uDDEA \uD83C\uDDEC\uD83C\uDDE7 \uD83C\uDDFA\uD83C\uDDF8",
          "indexedAt": "2026-10-22T22:37:51.842Z",
          "website": "https://idunno.org",
          "banner": "https://cdn.bsky.app/img/banner/plain/did:plc:hfgp6pj3akhqxntgqwramlbg/bafkreia3ww67kqsgkxy6bfgu4dxxyp52b3e2ghqbpoj7qt4iuupfx6c45a",
          "followersCount": 9888,
          "followsCount": 993,
          "postsCount": 42677,
          "pinnedPost": {
            "cid": "bafyreibtb55kfqsny3qk4c2puoqom4cu5zkpnp5xdwu5dud264b4efocyq",
            "uri": "at://did:plc:hfgp6pj3akhqxntgqwramlbg/app.bsky.feed.post/3l66cdbste424"
          },
          "links": [
            {
              "uri": "at://did:plc:hfgp6pj3akhqxntgqwramlbg/app.bsky.actor.link/3myiq3rojcq2i",
              "cid": "bafyreieu4xyqhebqzv2m644clcezg3obddavijncwgl54ogkbkzou7guze",
              "url": "https://idunno.org/",
              "title": "Blog",
              "icon": "https://cdn.bsky.app/img/avatar/plain/did:plc:hfgp6pj3akhqxntgqwramlbg/bafkreih3ablfr7vcjfxsz6auif4mmwaomqblolay6emuwfrb7twoppikcy",
              "labels": []
            },
            {
              "uri": "at://did:plc:hfgp6pj3akhqxntgqwramlbg/app.bsky.actor.link/3myiq3rqwe52t",
              "cid": "bafyreigym5qkku4ar35whduera6iqp7gc6ut3kmxzsuavnjp5sagdfpzjq",
              "url": "https://github.com/blowdart/",
              "title": "GitHub",
              "icon": "https://cdn.bsky.app/img/avatar/plain/did:plc:hfgp6pj3akhqxntgqwramlbg/bafkreie6tiznvm2vey67ob4sjxpp67puvmlpt54yqypitvj4kzten7sloy",
              "labels": []
            },
            {
              "uri": "at://did:plc:hfgp6pj3akhqxntgqwramlbg/app.bsky.actor.link/3myiq3rozsf2r",
              "cid": "bafyreiautstyjyp74zocyj5avivh7mcxk75htfcqtvu62ksig7jbmucw3y",
              "url": "https://jerryno.idunno.bot/",
              "title": "Jerry No",
              "icon": "https://cdn.bsky.app/img/avatar/plain/did:plc:hfgp6pj3akhqxntgqwramlbg/bafkreidvtsbtzlom43mmmj2l4bgcx5cngfyfcjjdw7ow6tvuenkj3hambu",
              "labels": []
            }
          ]
        }
        """;

    [Theory]
    [InlineData(0, "GitHub", "https://github.com/blowdart/", 942, 232)]
    [InlineData(1, "Jerry No", "https://jerryno.idunno.bot/", 9750, 232)]
    [InlineData(2, "Blog", "https://idunno.org/", 1112, 231)]
    public void CapturedLinksDeserializeThroughConcreteEnvelopeAndRecordBases(
        int index,
        string title,
        string url,
        int iconSize,
        int milliseconds)
    {
        using JsonDocument document = JsonDocument.Parse(LinkRecordsJson);
        JsonElement record = document.RootElement.GetProperty("records")[index];
        JsonElement value = record.GetProperty("value");
        AtProtoRepositoryRecord<Link>? envelope =
            JsonSerializer.Deserialize<AtProtoRepositoryRecord<Link>>(record, BlueskyServer.BlueskyJsonSerializerOptions);

        Assert.NotNull(envelope);
        Assert.Equal(record.GetProperty("uri").GetString(), envelope.Uri.ToString());
        Assert.Equal(record.GetProperty("cid").GetString(), envelope.Cid.ToString());

        Link[] links =
        [
            JsonSerializer.Deserialize<Link>(value, BlueskyServer.BlueskyJsonSerializerOptions)!,
            envelope.Value,
            Assert.IsType<Link>(JsonSerializer.Deserialize<BlueskyRecord>(value, BlueskyServer.BlueskyJsonSerializerOptions)),
            Assert.IsType<Link>(JsonSerializer.Deserialize<BlueskyTimestampedRecord>(value, BlueskyServer.BlueskyJsonSerializerOptions))
        ];

        foreach (Link link in links)
        {
            Assert.NotNull(link);
            Assert.Equal(title, link.Title);
            Assert.Equal(new Uri(url), link.Url);
            DateTimeOffset expectedCreatedAt = new(
                year: 2026,
                month: 10,
                day: 22,
                hour: 22,
                minute: 37,
                second: 50,
                offset: TimeSpan.Zero);
            Assert.Equal(expectedCreatedAt.AddMilliseconds(milliseconds), link.CreatedAt);
            Assert.NotNull(link.Icon);
            Assert.Equal("image/png", link.Icon.MimeType);
            Assert.Equal(iconSize, link.Icon.Size);
            Assert.Equal(
                new CidLink(value.GetProperty("icon").GetProperty("ref").GetProperty("$link").GetString()!),
                link.Icon.Reference);
            Assert.Null(link.Labels);
        }

        using JsonDocument serialized = JsonDocument.Parse(JsonSerializer.Serialize(links[0], BlueskyServer.BlueskyJsonSerializerOptions));
        Assert.Equal("app.bsky.actor.link", serialized.RootElement.GetProperty("$type").GetString());
        Assert.Equal(title, serialized.RootElement.GetProperty("title").GetString());
        Assert.Equal(iconSize, serialized.RootElement.GetProperty("icon").GetProperty("size").GetInt32());
    }

    [Fact]
    public void CapturedProfileReferencesMatchLinkRecordsInDisplayOrder()
    {
        using JsonDocument document = JsonDocument.Parse(ProfileRecordJson);
        JsonElement value = document.RootElement.GetProperty("value");
        AtProtoRepositoryRecord<Profile>? envelope =
            JsonSerializer.Deserialize<AtProtoRepositoryRecord<Profile>>(ProfileRecordJson, BlueskyServer.BlueskyJsonSerializerOptions);

        Assert.NotNull(envelope);
        Assert.Equal("at://did:plc:hfgp6pj3akhqxntgqwramlbg/app.bsky.actor.profile/self", envelope.Uri.ToString());
        Assert.Equal("bafyreieveta7kj4qv6vty7zbw7qbd5ld546dlmfwb3uqmdk23ll43evafe", envelope.Cid.ToString());

        Profile[] profiles =
        [
            JsonSerializer.Deserialize<Profile>(value, BlueskyServer.BlueskyJsonSerializerOptions)!,
            envelope.Value,
            Assert.IsType<Profile>(JsonSerializer.Deserialize<BlueskyRecord>(value, BlueskyServer.BlueskyJsonSerializerOptions))
        ];
        using JsonDocument linksDocument = JsonDocument.Parse(LinkRecordsJson);
        Dictionary<string, JsonElement> records = linksDocument.RootElement.GetProperty("records")
            .EnumerateArray().ToDictionary(record => record.GetProperty("uri").GetString()!);

        foreach (Profile profile in profiles)
        {
            Assert.NotNull(profile);
            Assert.Equal("Barry Dorrans", profile.DisplayName);
            Assert.NotNull(profile.Links);
            Assert.Equal(
                [
                    "at://did:plc:hfgp6pj3akhqxntgqwramlbg/app.bsky.actor.link/3myiq3rojcq2i",
                    "at://did:plc:hfgp6pj3akhqxntgqwramlbg/app.bsky.actor.link/3myiq3rqwe52t",
                    "at://did:plc:hfgp6pj3akhqxntgqwramlbg/app.bsky.actor.link/3myiq3rozsf2r"
                ],
                profile.Links.Select(link => link.Uri.ToString()));
            Assert.Equal(
                ["Blog", "GitHub", "Jerry No"],
                profile.Links.Select(link => records[link.Uri.ToString()].GetProperty("value").GetProperty("title").GetString()));
            foreach (StrongReference reference in profile.Links)
            {
                Assert.Equal(records[reference.Uri.ToString()].GetProperty("cid").GetString(), reference.Cid.ToString());
            }
        }
    }

    [Fact]
    public void CapturedDetailedProfilePopulatesHydratedLinksInProfileOrder()
    {
        ProfileViewDetailed? profile =
            JsonSerializer.Deserialize<ProfileViewDetailed>(DetailedProfileJson, BlueskyServer.BlueskyJsonSerializerOptions);
        AtProtoRepositoryRecord<Profile>? record =
            JsonSerializer.Deserialize<AtProtoRepositoryRecord<Profile>>(ProfileRecordJson, BlueskyServer.BlueskyJsonSerializerOptions);

        Assert.NotNull(profile);
        Assert.Equal("did:plc:hfgp6pj3akhqxntgqwramlbg", profile.Did.ToString());
        Assert.Equal("blowdart.me", profile.Handle.ToString());
        Assert.NotNull(profile.Links);
        Assert.Equal(["Blog", "GitHub", "Jerry No"], profile.Links.Select(link => link.Title));
        Assert.Equal(
            ["https://idunno.org/", "https://github.com/blowdart/", "https://jerryno.idunno.bot/"],
            profile.Links.Select(link => link.Url.ToString()));
        Assert.NotNull(record);
        Assert.NotNull(record.Value.Links);
        Assert.Equal(record.Value.Links.Select(link => link.Uri), profile.Links.Select(link => link.Uri));
        Assert.Equal(record.Value.Links.Select(link => link.Cid), profile.Links.Select(link => link.Cid));

        using JsonDocument document = JsonDocument.Parse(LinkRecordsJson);
        Dictionary<string, JsonElement> records = document.RootElement.GetProperty("records")
            .EnumerateArray().ToDictionary(link => link.GetProperty("uri").GetString()!);
        foreach (ProfileLinkView link in profile.Links)
        {
            JsonElement value = records[link.Uri.ToString()].GetProperty("value");
            string? iconCid = value.GetProperty("icon").GetProperty("ref").GetProperty("$link").GetString();
            Assert.Equal(
                new Uri($"https://cdn.bsky.app/img/avatar/plain/did:plc:hfgp6pj3akhqxntgqwramlbg/{iconCid}"),
                link.Icon);
            Assert.NotNull(link.Labels);
            Assert.Empty(link.Labels);
        }
    }
}
