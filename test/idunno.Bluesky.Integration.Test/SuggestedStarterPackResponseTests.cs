// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Net;
using System.Text.Json;

using idunno.AtProto;
using idunno.AtProto.Authentication;
using idunno.Bluesky.Graph;
using idunno.Bluesky.Unspecced;
using idunno.Bluesky.Unspecced.Model;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;

namespace idunno.Bluesky.Integration.Test;

#pragma warning disable BSKYUnspecced

[ExcludeFromCodeCoverage]
public class SuggestedStarterPackResponseTests
{
    private const string SyntheticStarterPack = """
        {
            "uri":"at://did:plc:test/app.bsky.graph.starterpack/test",
            "cid":"bafyreiaxqkvwjv5y45xq7huunf6mjvh7tf6ls44imkgjkqi3uthn57vjxu",
            "record":{
                "$type":"app.bsky.graph.starterpack",
                "name":"Synthetic starter pack, not a live response.",
                "list":"at://did:plc:test/app.bsky.graph.list/test",
                "createdAt":"2026-01-01T00:00:00Z"
            },
            "creator":{"did":"did:plc:test","handle":"test.invalid"},
            "indexedAt":"2026-01-01T00:00:00Z",
            "list":{
                "uri":"at://did:plc:test/app.bsky.graph.list/test",
                "cid":"bafyreiaxqkvwjv5y45xq7huunf6mjvh7tf6ls44imkgjkqi3uthn57vjxu",
                "name":"Synthetic list",
                "purpose":"app.bsky.graph.defs#curatelist"
            },
            "listItemsSample":[],
            "feeds":[]
        }
        """;

    [Theory]
    [InlineData(true, "\"recommendation-1\"", "recommendation-1", 1)]
    [InlineData(false, "\"recommendation-1\"", "recommendation-1", 25)]
    [InlineData(true, "\"\"", "", 25)]
    [InlineData(false, "\"\"", "", 1)]
    [InlineData(true, null, null, 1)]
    [InlineData(false, null, null, 25)]
    public async Task SyntheticSuggestionsPreserveRecommendationAndSkipNullPacks(bool useAgent, string? identifierJson, string? expected, int limit)
    {
        string metadata = identifierJson is null ? string.Empty : $",\"recIdStr\":{identifierJson}";
        string body = $$"""{"starterPacks":[null,{{SyntheticStarterPack}},null]{{metadata}}}""";
        Did[] labelers = [new("did:plc:test")];
        using TestServer server = CreateServer(body, request =>
        {
            Assert.Equal(limit.ToString(System.Globalization.CultureInfo.InvariantCulture), request.Query["limit"].ToString());
            Assert.Contains(labelers[0].ToString(), request.Headers["atproto-accept-labelers"].ToString(), StringComparison.Ordinal);
        });
        using HttpClient client = server.CreateClient();
        using BlueskyAgent agent = CreateAgent(server);

        Assert.NotNull(SourceGenerationContext.Default.GetTypeInfo(typeof(GetSuggestedStarterPacksResponse)));
        Assert.NotNull(SourceGenerationContext.Default.GetTypeInfo(typeof(StarterPackView)));
        Assert.NotNull(BlueskyServer.BlueskyJsonSerializerOptions.GetTypeInfo(typeof(GetSuggestedStarterPacksResponse)));
        Assert.NotNull(BlueskyServer.BlueskyJsonSerializerOptions.GetTypeInfo(typeof(StarterPackView)));

        AtProtoHttpResult<RecommendationReadOnlyCollection<StarterPackView>> result = useAgent
            ? await agent.GetSuggestedStarterPacksWithRecommendation(limit: limit, subscribedLabelers: labelers, cancellationToken: TestContext.Current.CancellationToken)
            : await BlueskyServer.GetSuggestedStarterPacksWithRecommendation(
                limit, TestServerBuilder.DefaultUri, null, client,
                subscribedLabelers: labelers,
                cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Equal(expected, result.Result.RecommendationId);
        StarterPackView pack = Assert.Single(result.Result);
        Assert.Equal("at://did:plc:test/app.bsky.graph.starterpack/test", pack.Uri.ToString());
        Assert.Empty(pack.ListItemsSample);
        Assert.Empty(pack.Feeds);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task LegacySuggestionsKeepTheirCollectionReturnContract(bool useAgent)
    {
        using TestServer server = CreateServer($$"""{"starterPacks":[null,{{SyntheticStarterPack}},null],"recIdStr":"recommendation-1"}""");
        using HttpClient client = server.CreateClient();
        using BlueskyAgent agent = CreateAgent(server);

        AtProtoHttpResult<ICollection<StarterPackView>> result = useAgent
            ? await agent.GetSuggestedStarterPacks(cancellationToken: TestContext.Current.CancellationToken)
            : await BlueskyServer.GetSuggestedStarterPacks(
                null, TestServerBuilder.DefaultUri, null, client,
                cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Single(result.Result);
        Assert.False(result.Result.IsReadOnly);
        result.Result.Clear();
        Assert.Empty(result.Result);
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public async Task BothSuggestionSurfacesPreserveErrorsAndResponseHeaders(bool useAgent, bool legacy)
    {
        using TestServer server = CreateServer("""{"error":"InvalidRequest","message":"Synthetic failure."}""", statusCode: HttpStatusCode.BadRequest);
        using HttpClient client = server.CreateClient();
        using BlueskyAgent agent = CreateAgent(server);
        if (legacy)
        {
            AtProtoHttpResult<ICollection<StarterPackView>> result = useAgent
                ? await agent.GetSuggestedStarterPacks(cancellationToken: TestContext.Current.CancellationToken)
                : await BlueskyServer.GetSuggestedStarterPacks(
                    null, TestServerBuilder.DefaultUri, null, client,
                    cancellationToken: TestContext.Current.CancellationToken);
            AssertFailure(result);
        }
        else
        {
            AtProtoHttpResult<RecommendationReadOnlyCollection<StarterPackView>> result = useAgent
                ? await agent.GetSuggestedStarterPacksWithRecommendation(cancellationToken: TestContext.Current.CancellationToken)
                : await BlueskyServer.GetSuggestedStarterPacksWithRecommendation(
                    null, TestServerBuilder.DefaultUri, null, client,
                    cancellationToken: TestContext.Current.CancellationToken);
            AssertFailure(result);
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(26)]
    public async Task BothSuggestionSurfacesRejectInvalidLimits(int limit)
    {
        using HttpClient client = new();
        using BlueskyAgent agent = new() { Service = TestServerBuilder.DefaultUri };
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => agent.GetSuggestedStarterPacks(limit, cancellationToken: cancellationToken));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => agent.GetSuggestedStarterPacksWithRecommendation(limit, cancellationToken: cancellationToken));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => BlueskyServer.GetSuggestedStarterPacks(
            limit, TestServerBuilder.DefaultUri, null, client, cancellationToken: cancellationToken));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => BlueskyServer.GetSuggestedStarterPacksWithRecommendation(
            limit, TestServerBuilder.DefaultUri, null, client, cancellationToken: cancellationToken));
    }

    [Fact]
    public async Task BothSuggestionSurfacesRejectNullServiceAndHttpClient()
    {
        using HttpClient client = new();
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await Assert.ThrowsAsync<ArgumentNullException>(() => BlueskyServer.GetSuggestedStarterPacks(
            null, null!, null, client, cancellationToken: cancellationToken));
        await Assert.ThrowsAsync<ArgumentNullException>(() => BlueskyServer.GetSuggestedStarterPacksWithRecommendation(
            null, null!, null, client, cancellationToken: cancellationToken));
        await Assert.ThrowsAsync<ArgumentNullException>(() => BlueskyServer.GetSuggestedStarterPacks(
            null, TestServerBuilder.DefaultUri, null, null!, cancellationToken: cancellationToken));
        await Assert.ThrowsAsync<ArgumentNullException>(() => BlueskyServer.GetSuggestedStarterPacksWithRecommendation(
            null, TestServerBuilder.DefaultUri, null, null!, cancellationToken: cancellationToken));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task BothSuggestionSurfacesRejectNonPositiveResponseSize(int maximumResponseSize)
    {
        using HttpClient client = new();
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => BlueskyServer.GetSuggestedStarterPacks(
            null, TestServerBuilder.DefaultUri, null, client,
            maximumResponseSize: maximumResponseSize, cancellationToken: cancellationToken));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => BlueskyServer.GetSuggestedStarterPacksWithRecommendation(
            null, TestServerBuilder.DefaultUri, null, client,
            maximumResponseSize: maximumResponseSize, cancellationToken: cancellationToken));
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("""{"starterPacks":null}""")]
    public async Task RichSuggestionsRejectMissingOrNullRequiredCollection(string body)
    {
        using TestServer server = CreateServer(body);
        using HttpClient client = server.CreateClient();

        AtProtoHttpResult<RecommendationReadOnlyCollection<StarterPackView>> result = await BlueskyServer.GetSuggestedStarterPacksWithRecommendation(
            null, TestServerBuilder.DefaultUri, null, client,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Null(result.Result);
    }

    [Fact]
    public async Task CapturedPublicSuggestedStarterPacksReplayThroughRicherApi()
    {
        // Replays an unauthenticated public AppView capture from 2026-10-15; this test makes no live call.
        // Public unauthenticated capture: HTTP 200, 2026-10-15T20:11:47Z.
        // Source: https://public.api.bsky.app/xrpc/app.bsky.unspecced.getSuggestedStarterPacks?limit=1
        // Observed: Starter packs; recIdStr absent. The service returned more than the requested one item.
        const string body = """
            {"starterPacks":[{"uri":"at://did:plc:v7gngzva22rilmcp6fiypowf/app.bsky.graph.starterpack/3lbcrdrart42l","cid":"bafyreidwj5mj4lmqxhjhvkvjgnchiqkdmhd35ctzvfkvdytifbqv5iemfe","record":{"$type":"app.bsky.graph.starterpack","createdAt":"2024-11-19T15:38:34.006Z","description":"A curated (𝗡𝗢 𝗥𝗘𝗤𝗨𝗘𝗦𝗧𝗦, 𝗽𝗹𝘇) list of skilled Bluesky nature photographers that regularly post their own landscape and wildlife photography with few to no human structures. \n\n📣 Use my related FEED (https://bit.ly/4hyJkL0) to see the best of their beautiful work.","descriptionFacets":[{"features":[{"$type":"app.bsky.richtext.facet#link","uri":"https://bit.ly/4hyJkL0"}],"index":{"byteEnd":261,"byteStart":239}}],"feeds":[{"avatar":"https://cdn.bsky.app/img/avatar/plain/did:plc:v7gngzva22rilmcp6fiypowf/bafkreifxs2cl5rgkho7zazftgz3y4zkupmfszf5jdt5qxno3ixsl3yzcqq@jpeg","cid":"bafyreifu2llzuvbtpilnwbvctxjdy6eecgiz4v5ilb2ujnmrwesrqndob4","creator":{"avatar":"https://cdn.bsky.app/img/avatar/plain/did:plc:v7gngzva22rilmcp6fiypowf/bafkreic57hiwvxq5czk2774scqlfojhedrnlcfqhhopjfvernk2fraxkwm@jpeg","createdAt":"2024-11-13T02:58:37.414Z","description":"📷 Landscape + Wildlife Photography\n   ▪️ 𝘚𝘦𝘦 𝘮𝘺 𝙥𝙝𝙤𝙩𝙤𝙜𝙧𝙖𝙥𝙝𝙮 𝙛𝙚𝙚𝙙𝙨 🛜\n🎗️ Navy Veteran Defending Democracy\n   ▪️ 𝘛𝘢𝘬𝘦 𝘢𝘤𝘵𝘪𝘰𝘯: https://civicshout.com/\n🪷 Awakening + Human Rights\n⚡ Rivian + Aptera 𝗘𝗩 𝗳𝗲𝗲𝗱𝘀\n\n📍 Decorah, Iowa 🇺🇲 🆘\n🔗 https://Linktr.ee/nickchillphoto","did":"did:plc:v7gngzva22rilmcp6fiypowf","displayName":"Nick Chill Photo","handle":"nickchillphoto.com","indexedAt":"2025-04-09T18:42:17.143Z","labels":[],"viewer":{"blockedBy":false,"muted":false}},"description":"Landscape photography & wildlife photography by a curated list (𝗡𝗢 𝗥𝗘𝗤𝗨𝗘𝗦𝗧𝗦, 𝗽𝗹𝘇) of Top-Notch Nature Photographers.\r\n\r\nContributors tag #wildlife, #landscape, or #nature + 𝗔𝗟𝗧 𝘁𝗲𝘅𝘁\r\n\r\n💙 Click 𝗟𝗜𝗞𝗘 at top of feed\r\n❓ Info: https://bit.ly/3CnR03V\r\n✔️ Follow @nickchillphoto.com","did":"did:web:blueskyfeedcreator.com","displayName":"Top Nature Photography ⭐","indexedAt":"2025-03-21T19:18:51.922Z","labels":[],"likeCount":57,"uri":"at://did:plc:v7gngzva22rilmcp6fiypowf/app.bsky.feed.generator/topnaturephotos","viewer":{"like":"at://did:plc:v7gngzva22rilmcp6fiypowf/app.bsky.feed.like/3lgshd66yts2i"}}],"list":"at://did:plc:v7gngzva22rilmcp6fiypowf/app.bsky.graph.list/3lbcrdr5d452n","name":"Top-Notch Nature Photographers 📷 Starter Pack","updatedAt":"2025-05-01T11:38:46.953Z"},"creator":{"did":"did:plc:v7gngzva22rilmcp6fiypowf","handle":"nickchillphoto.com","displayName":"Nick Chill Photo","avatar":"https://cdn.bsky.app/img/avatar/plain/did:plc:v7gngzva22rilmcp6fiypowf/bafkreic57hiwvxq5czk2774scqlfojhedrnlcfqhhopjfvernk2fraxkwm","associated":{"chat":{"allowIncoming":"none","allowGroupInvites":"none"},"activitySubscription":{"allowSubscriptions":"followers"}},"labels":[],"createdAt":"2024-11-13T02:58:37.381Z"},"joinedAllTimeCount":3,"joinedWeekCount":0,"labels":[],"indexedAt":"2025-05-01T11:38:45.881Z","feeds":[{"uri":"at://did:plc:v7gngzva22rilmcp6fiypowf/app.bsky.feed.generator/topnaturephotos","cid":"bafyreihblez3bj765rkt37kikx6om3s32dijuqh36ngxno22jzp22u7t3i","did":"did:web:blueskyfeedcreator.com","creator":{"did":"did:plc:v7gngzva22rilmcp6fiypowf","handle":"nickchillphoto.com","displayName":"Nick Chill Photo","avatar":"https://cdn.bsky.app/img/avatar/plain/did:plc:v7gngzva22rilmcp6fiypowf/bafkreic57hiwvxq5czk2774scqlfojhedrnlcfqhhopjfvernk2fraxkwm","associated":{"chat":{"allowIncoming":"none","allowGroupInvites":"none"},"activitySubscription":{"allowSubscriptions":"followers"}},"labels":[],"createdAt":"2024-11-13T02:58:37.381Z","description":"📷 Landscape + Wildlife Photography\n    ▪️ 𝘚𝘦𝘦 𝘮𝘺 𝘱𝘩𝘰𝘵𝘰𝘨𝘳𝘢𝘱𝘩𝘺 𝙛𝙚𝙚𝙙𝙨\n🎗️ Veteran 4 Democracy + Climate\n    ▪️ 𝘛𝘢𝘬𝘦 𝙖𝙘𝙩𝙞𝙤𝙣 𝘣𝘦𝘭𝘰𝘸\n🪷 Self-Realization + Human Rights 🏳️‍🌈\n⚡ Rivian + Aptera EV 𝙛𝙚𝙚𝙙𝙨\n\n📍 Decorah, IA\n🔗 𝗚𝗮𝗹𝗹𝗲𝗿𝘆 + 𝗧𝗮𝗸𝗲 𝗔𝗰𝘁𝗶𝗼𝗻: linktr.ee/nickchillphoto","indexedAt":"2026-05-11T19:28:08.657Z"},"displayName":"Top Nature Photography ⭐","description":"Landscape & wildlife photography by top-notch photographers (𝗡𝗢 𝗥𝗘𝗤𝗨𝗘𝗦𝗧𝗦).\r\n\r\n💸 Please consider donating to feed fees (see info).\r\nContributors tag #wildlife, #landscape, or #nature + 𝗔𝗟𝗧\r\n\r\n💙 Click 𝗟𝗜𝗞𝗘 below\r\n❓ Info: https://bit.ly/3CnR03V\r\n✔️ Follow @nickchillphoto.com","descriptionFacets":[],"avatar":"https://cdn.bsky.app/img/avatar/plain/did:plc:v7gngzva22rilmcp6fiypowf/bafkreifxs2cl5rgkho7zazftgz3y4zkupmfszf5jdt5qxno3ixsl3yzcqq","likeCount":98,"labels":[],"indexedAt":"2026-03-26T18:13:30.457Z"}],"list":{"uri":"at://did:plc:v7gngzva22rilmcp6fiypowf/app.bsky.graph.list/3lbcrdr5d452n","cid":"bafyreiaxmsjw2ebq2x5ea6dfjqi5uzzod56pkm3gavavtuaoavtne7xzhi","name":"Enjoyable Photographers Starter Pack","purpose":"app.bsky.graph.defs#referencelist","listItemCount":95,"indexedAt":"2024-11-19T15:38:35.248Z","labels":[]},"listItemsSample":[{"uri":"at://did:plc:v7gngzva22rilmcp6fiypowf/app.bsky.graph.listitem/3lbiwq6tq3p2h","subject":{"did":"did:plc:7z6ecty3w4vsovvvhlv7nnee","handle":"jocelynanderson.bsky.social","displayName":"Jocelyn Anderson Photography","avatar":"https://cdn.bsky.app/img/avatar/plain/did:plc:7z6ecty3w4vsovvvhlv7nnee/bafkreihmzxhustkaongpsl33plu2csrnnnwa5ytf5ogdf3se45iljlduiy","associated":{"chat":{"allowIncoming":"following"},"activitySubscription":{"allowSubscriptions":"followers"}},"labels":[],"createdAt":"2023-11-01T12:53:55.184Z","description":"Photography who is a big fan of birds","indexedAt":"2024-01-20T08:04:05.484Z"}},{"uri":"at://did:plc:v7gngzva22rilmcp6fiypowf/app.bsky.graph.listitem/3lfsryccxsr2n","subject":{"did":"did:plc:lve6t3ylxfuqge7iqtacaq7t","handle":"tjalamont.bsky.social","displayName":"Tom Astle","avatar":"https://cdn.bsky.app/img/avatar/plain/did:plc:lve6t3ylxfuqge7iqtacaq7t/bafkreigjkrchhzhq7rbozpzksfmspefkjo7dyfkaw5xif3hmns63xzo34e","associated":{"chat":{"allowIncoming":"following"},"activitySubscription":{"allowSubscriptions":"followers"}},"labels":[{"src":"did:plc:lve6t3ylxfuqge7iqtacaq7t","uri":"at://did:plc:lve6t3ylxfuqge7iqtacaq7t/app.bsky.actor.profile/self","cid":"bafyreifo5fvczdm5o4py2yolnk5g42vt5pxnkk32pvm33dlshfflvu6wk4","val":"!no-unauthenticated","cts":"1970-01-01T00:00:00.000Z"}],"createdAt":"2023-05-05T22:51:44.520Z","description":"Writer & nature photographer, especially macro photography of arthropods. Board member, Desert Tortoise Preserve Committee (conservation nonprofit). Fan of California, Montana, the rest of the planet. Photo website: https://www.tomastlephotography.com/","indexedAt":"2026-01-27T00:24:34.547Z"}},{"uri":"at://did:plc:v7gngzva22rilmcp6fiypowf/app.bsky.graph.listitem/3lbhplkgnyz2s","subject":{"did":"did:plc:7j3bplw2whqhrlsnayfsuo53","handle":"pkilleenphoto.bsky.social","displayName":"Paul Killeen","avatar":"https://cdn.bsky.app/img/avatar/plain/did:plc:7j3bplw2whqhrlsnayfsuo53/bafkreiecstrlib4h6aqhpe7lgbafutmrsbpapfcbdlytxvaln4i2vxvzee","associated":{"chat":{"allowIncoming":"none"},"activitySubscription":{"allowSubscriptions":"followers"}},"labels":[],"createdAt":"2024-11-17T13:03:01.736Z","description":"Overall Winner of World Landscape Photographer 2022. Category Winner in LPOTY 2022. Based in Northern Ireland. 1-2-1 workshops available.\n\nLinks below.\n\nhttps://linktr.ee/paulkilleenphotography","indexedAt":"2026-02-11T09:51:56.071Z"}},{"uri":"at://did:plc:v7gngzva22rilmcp6fiypowf/app.bsky.graph.listitem/3ljydavbvtm27","subject":{"did":"did:plc:hm27bzd64j5qhddv525z3ciz","handle":"spyhoppertravels.bsky.social","displayName":"Spyhopper Travels","avatar":"https://cdn.bsky.app/img/avatar/plain/did:plc:hm27bzd64j5qhddv525z3ciz/bafkreicdqrxrnqgyww3s2eleeoytfdbi6kwqvvx34lqtiazo7wagqccgzu","associated":{"activitySubscription":{"allowSubscriptions":"followers"}},"labels":[],"createdAt":"2025-01-20T05:44:37.143Z","description":"-I am a zoologist, artist, & nature photographer based in the PNW. I love sharing my passion for the natural world. 📸🌎🫍🐺🐈‍⬛\n-they/them 🌈\n-All images/art posted are by me unless reposted.\n-Woke AF\n-🧩\n- http://spyhoppertravels.zenfolio.com","indexedAt":"2026-03-26T20:17:18.157Z"}},{"uri":"at://did:plc:v7gngzva22rilmcp6fiypowf/app.bsky.graph.listitem/3lik63melng2z","subject":{"did":"did:plc:uaw6yv65emr5vlqvncfjy65g","handle":"marygemignani.bsky.social","displayName":"Mary Gemignani","avatar":"https://cdn.bsky.app/img/avatar/plain/did:plc:uaw6yv65emr5vlqvncfjy65g/bafkreigo3oiqpm7hg6mklpmiflg4o4efl3mwirrhe2bynpkyghzjfxytf4","associated":{"chat":{"allowIncoming":"following"},"activitySubscription":{"allowSubscriptions":"followers"}},"labels":[{"src":"did:plc:uaw6yv65emr5vlqvncfjy65g","uri":"at://did:plc:uaw6yv65emr5vlqvncfjy65g/app.bsky.actor.profile/self","cid":"bafyreiaxkgb5phhdks2l3tzq6pzo2maypag3z6czeis7ypf3nzrr4dztxm","val":"!no-unauthenticated","cts":"1970-01-01T00:00:00.000Z"}],"createdAt":"2023-05-05T02:35:08.860Z","description":"Data Professional. Photographer. I try to put birds and all of nature in a good light. 📷\n\nAll posts are copyright my own unless noted otherwise.","indexedAt":"2025-11-06T22:55:34.452Z"}},{"uri":"at://did:plc:v7gngzva22rilmcp6fiypowf/app.bsky.graph.listitem/3lg4xuiauvl2i","subject":{"did":"did:plc:tnctrxi2kyn7hodov6qbkyf2","handle":"sophiecarr.bsky.social","displayName":"Sophie Carr","avatar":"https://cdn.bsky.app/img/avatar/plain/did:plc:tnctrxi2kyn7hodov6qbkyf2/bafkreidn3jgzlyxp375eonei6gy2mmsfju2wujie37ldllh3ourr4ov43q","associated":{"chat":{"allowIncoming":"all"},"activitySubscription":{"allowSubscriptions":"followers"}},"labels":[{"src":"did:plc:tnctrxi2kyn7hodov6qbkyf2","uri":"at://did:plc:tnctrxi2kyn7hodov6qbkyf2/app.bsky.actor.profile/self","cid":"bafyreic4bqwbpspgj5e65iv5kc3ilrf7o3qpat75kqztr6dmccouhuqxba","val":"!no-unauthenticated","cts":"1970-01-01T00:00:00.000Z"}],"createdAt":"2023-07-13T10:47:56.866Z","description":"Iceland-obsessed Landscape Photographer. UK-based.\n\nNewsletter sign-up: https://preview.mailerlite.io/preview/2233713/forms/183477923653617261?fresh=1%2Fpopup","indexedAt":"2026-09-01T17:30:10.798Z"}},{"uri":"at://did:plc:v7gngzva22rilmcp6fiypowf/app.bsky.graph.listitem/3lgl5ubsidz2i","subject":{"did":"did:plc:fagkxkv3ldiu6wt5do7yrgio","handle":"zeworld.bsky.social","displayName":"Efrén Yanes","avatar":"https://cdn.bsky.app/img/avatar/plain/did:plc:fagkxkv3ldiu6wt5do7yrgio/bafkreihqf6wtxljym3hstagkvkd7fr57o5ccgsk3gjloks5k52pfv2bage","associated":{"activitySubscription":{"allowSubscriptions":"followers"}},"labels":[],"createdAt":"2024-11-16T21:32:23.538Z","description":"Fotografía de paisaje y astrofotografía\n📍Tenerife","indexedAt":"2024-11-24T10:27:15.645Z"}},{"uri":"at://did:plc:v7gngzva22rilmcp6fiypowf/app.bsky.graph.listitem/3lik5izcald23","subject":{"did":"did:plc:tl4lz4mtg6a3pjpzm6g4qim4","handle":"albertdros.bsky.social","displayName":"Albert Dros","avatar":"https://cdn.bsky.app/img/avatar/plain/did:plc:tl4lz4mtg6a3pjpzm6g4qim4/bafkreifraufjsphzicgs5fq6ym64ibdnmigpjtql6p4uxswys5h63cad64","associated":{"chat":{"allowIncoming":"none"},"activitySubscription":{"allowSubscriptions":"followers"}},"labels":[],"createdAt":"2024-11-24T09:19:33.245Z","description":"Landscape Photographer from the Netherlands . See my work on www.albertdros.com","indexedAt":"2025-06-24T22:50:31.833Z"}},{"uri":"at://did:plc:v7gngzva22rilmcp6fiypowf/app.bsky.graph.listitem/3lik5izcalf23","subject":{"did":"did:plc:r3jjakfjtpgiutezhta2bxtr","handle":"nikontom.bsky.social","displayName":"Nikon Tom","avatar":"https://cdn.bsky.app/img/avatar/plain/did:plc:r3jjakfjtpgiutezhta2bxtr/bafkreifowprttva6lc5kwmi2ll3atdxf3a5l2duixuflmahcc532gbekb4","associated":{"chat":{"allowIncoming":"all"},"activitySubscription":{"allowSubscriptions":"followers"}},"labels":[],"createdAt":"2024-11-14T17:06:21.136Z","description":"📷 Nikon Photographer 📷 \"The Camera Cafe Show \"🎧Podcast Host🎧 Always trying to Inspire people to pick up a Camera 😊 https://thecameracafeshow.com/","indexedAt":"2024-11-21T12:07:13.770Z"}},{"uri":"at://did:plc:v7gngzva22rilmcp6fiypowf/app.bsky.graph.listitem/3lgl5ubsie22i","subject":{"did":"did:plc:wbj65xnocn6xy5iax65snujj","handle":"craigmcg.bsky.social","displayName":"Craig McGowan","avatar":"https://cdn.bsky.app/img/avatar/plain/did:plc:wbj65xnocn6xy5iax65snujj/bafkreicikcrz7ckxfd2365e4mscjms2j4ff35d7djqnbejybjsfwnaipyy","associated":{"activitySubscription":{"allowSubscriptions":"followers"}},"labels":[],"createdAt":"2023-11-21T00:50:43.665Z","description":"Photographer - landscapes in remote and cooler climates are my passion. But I also find time to shoot closer to home when I can. No AI, just lens, light and intent... \n\nhttps://CraigMcGowanphotography.com","indexedAt":"2025-05-24T05:15:47.840Z"}},{"uri":"at://did:plc:v7gngzva22rilmcp6fiypowf/app.bsky.graph.listitem/3liwjvzjnnf2b","subject":{"did":"did:plc:lqjjj3sbpkrj6crvqpxjjjki","handle":"timothykeeneyphotography.com","displayName":"Timothy Keeney","avatar":"https://cdn.bsky.app/img/avatar/plain/did:plc:lqjjj3sbpkrj6crvqpxjjjki/bafkreib2jxgok2yzvl4bflecaocxddmyeaoypi2ded4wb2xkrzzlyjl264","associated":{"chat":{"allowIncoming":"none"},"activitySubscription":{"allowSubscriptions":"followers"}},"labels":[],"createdAt":"2024-11-22T18:05:39.554Z","description":"Connecticut-Based Landscape & Nature Photographer\nWaterfalls, Iceland, American Southwest & More\nShop Fine Art Prints for Home & Office\ntimothykeeneyphotography.com","indexedAt":"2026-06-07T00:58:27.965Z"}},{"uri":"at://did:plc:v7gngzva22rilmcp6fiypowf/app.bsky.graph.listitem/3lbiyoyvf242c","subject":{"did":"did:plc:73d227ekp5oaulxctb3feysk","handle":"bebedi.bsky.social","displayName":"Edwin Godinho","avatar":"https://cdn.bsky.app/img/avatar/plain/did:plc:73d227ekp5oaulxctb3feysk/bafkreig6uthxumaw2uzmyzyjm4dvrbyx272kpw6r6d3ywxpt2vbwkbr35u","associated":{"activitySubscription":{"allowSubscriptions":"followers"}},"labels":[],"createdAt":"2024-11-15T22:44:21.341Z","description":"An Engineer & Tech Management pro ⚙️🛠️💻 by day. An award🏆winning amateur #wildlifephotographer 📸 in my spare time. Travel the 🌎 to photograph #wildlife & #nature 🪶 Call me Bebedi 👨🏻‍🔧🇵🇹  🏡 🇬🇧\nIG ▶️ www.instagram.com/edwin.godinho \nFB ▶️ www.facebook.com/egodinho","indexedAt":"2025-02-04T07:43:03.443Z"}}]},{"uri":"at://did:plc:na44tnnzcxmrmgxohiwueh4n/app.bsky.graph.starterpack/3lawnq5azwn2e","cid":"bafyreifnxbjtdk3tgrvahkb6xb2a2mapmmhqjjbjaabnflx6r23zc5gfwa","record":{"$type":"app.bsky.graph.starterpack","createdAt":"2024-11-14T20:01:57.330Z","description":"A list of Film and TV Magazines on BlueSky.","feeds":[],"list":"at://did:plc:na44tnnzcxmrmgxohiwueh4n/app.bsky.graph.list/3lawnq4rm272p","name":"Film & TV Magazines","updatedAt":"2024-11-16T14:39:24.010Z"},"creator":{"did":"did:plc:na44tnnzcxmrmgxohiwueh4n","handle":"filmcritics.org.uk","displayName":"Film Critics Association UK 🎥","avatar":"https://cdn.bsky.app/img/avatar/plain/did:plc:na44tnnzcxmrmgxohiwueh4n/bafkreifbqyl5hiyamhj3lvm54n4qggu3z5had4jturxytk7v4nky4twlem","associated":{"chat":{"allowIncoming":"none","allowGroupInvites":"none"},"activitySubscription":{"allowSubscriptions":"followers"}},"labels":[],"createdAt":"2024-10-17T21:04:24.009Z"},"joinedAllTimeCount":0,"joinedWeekCount":0,"labels":[],"indexedAt":"2024-11-16T14:39:24.202Z","feeds":[],"list":{"uri":"at://did:plc:na44tnnzcxmrmgxohiwueh4n/app.bsky.graph.list/3lawnq4rm272p","cid":"bafyreid4xuu3oi4rxcai7x75mrwsuxjzsoyil3pnmwtkbpkug34rk5rhvi","name":"Film Magazines","purpose":"app.bsky.graph.defs#referencelist","listItemCount":11,"indexedAt":"2024-11-14T20:01:57.155Z","labels":[]},"listItemsSample":[{"uri":"at://did:plc:na44tnnzcxmrmgxohiwueh4n/app.bsky.graph.listitem/3lawnq4z6fd2a","subject":{"did":"did:plc:azhp7skmdpph62i3zgy4jixy","handle":"variety.com","displayName":"Variety","avatar":"https://cdn.bsky.app/img/avatar/plain/did:plc:azhp7skmdpph62i3zgy4jixy/bafkreih5qqrmzerge6oae3v2zn7ql3alxy2fjacl5fi5qdb6664mebl3dm","associated":{"activitySubscription":{"allowSubscriptions":"followers"}},"labels":[],"createdAt":"2023-07-03T16:51:17.472Z","verification":{"verifications":[{"issuer":"did:plc:z72i7hdynmk6r22z27h6tvur","issuerDisplayName":"Bluesky","issuerHandle":"bsky.app","uri":"at://did:plc:z72i7hdynmk6r22z27h6tvur/app.bsky.graph.verification/3lndpsptyoy2m","isValid":true,"createdAt":"2025-04-21T10:46:07.477Z"}],"verifiedStatus":"valid","trustedVerifierStatus":"none"},"description":"The business of entertainment.","indexedAt":"2025-11-21T15:55:08.730Z"}},{"uri":"at://did:plc:na44tnnzcxmrmgxohiwueh4n/app.bsky.graph.listitem/3lawnq4z6fb2a","subject":{"did":"did:plc:pp5hr2e5evpr4lto52hnkt2t","handle":"empiremagazine.bsky.social","displayName":"Empire Magazine","avatar":"https://cdn.bsky.app/img/avatar/plain/did:plc:pp5hr2e5evpr4lto52hnkt2t/bafkreihfayrtmi73vu6666lw4s33meo2sdklc4w77x7vwcg3swarnpctwm","associated":{"chat":{"allowIncoming":"none","allowGroupInvites":"none"},"activitySubscription":{"allowSubscriptions":"followers"}},"labels":[],"createdAt":"2024-11-13T11:13:45.787Z","verification":{"verifications":[{"issuer":"did:plc:z72i7hdynmk6r22z27h6tvur","issuerDisplayName":"Bluesky","issuerHandle":"bsky.app","uri":"at://did:plc:z72i7hdynmk6r22z27h6tvur/app.bsky.graph.verification/3ma4vhetgbs2w","isValid":true,"createdAt":"2025-12-16T19:37:11.967Z"}],"verifiedStatus":"valid","trustedVerifierStatus":"none"},"description":"The latest film news, reviews and interviews from Empire, the world's most trusted movie destination.\n\nhttps://www.empireonline.com","indexedAt":"2026-10-15T14:57:54.564Z"}},{"uri":"at://did:plc:na44tnnzcxmrmgxohiwueh4n/app.bsky.graph.listitem/3lawnq4z6fg2a","subject":{"did":"did:plc:dccs3caiylrq3krh475homau","handle":"fangoria.bsky.social","displayName":"FANGORIA","avatar":"https://cdn.bsky.app/img/avatar/plain/did:plc:dccs3caiylrq3krh475homau/bafkreid5op7fai5diy2vjohrnytrwkx573svbzdkn5faau4kt3fcyfd2z4","associated":{"activitySubscription":{"allowSubscriptions":"followers"}},"labels":[],"createdAt":"2023-05-23T22:59:24.741Z","description":"First in Fright since 1979, FANGORIA is a collectible magazine, a podcast network, and an entertainment studio.\nFANGORIA.com","indexedAt":"2026-03-17T14:09:30.247Z"}},{"uri":"at://did:plc:na44tnnzcxmrmgxohiwueh4n/app.bsky.graph.listitem/3lawnq4z6fc2a","subject":{"did":"did:plc:apnkdg3j3ceoz2ypvmsexvhv","handle":"lwlies.com","displayName":"Little White Lies","avatar":"https://cdn.bsky.app/img/avatar/plain/did:plc:apnkdg3j3ceoz2ypvmsexvhv/bafkreiezxybznp2jhp3mh2rrard6szkohfbdjvp76gkcniyklm36syfii4","associated":{"chat":{"allowIncoming":"none","allowGroupInvites":"none"},"activitySubscription":{"allowSubscriptions":"followers"}},"labels":[],"createdAt":"2023-09-19T10:44:06.332Z","verification":{"verifications":[{"issuer":"did:plc:z72i7hdynmk6r22z27h6tvur","issuerDisplayName":"Bluesky","issuerHandle":"bsky.app","uri":"at://did:plc:z72i7hdynmk6r22z27h6tvur/app.bsky.graph.verification/3mv7kfu43hi26","isValid":true,"createdAt":"2026-09-11T02:46:33.205Z"}],"verifiedStatus":"valid","trustedVerifierStatus":"none"},"description":"Passionate about movies since 2005.\n\nhttps://linktr.ee/lwlies\n\nSupport our independent journalism: http://bit.ly/3tKr2O9","indexedAt":"2026-10-08T14:39:59.963Z"}},{"uri":"at://did:plc:na44tnnzcxmrmgxohiwueh4n/app.bsky.graph.listitem/3lawnq4z6ff2a","subject":{"did":"did:plc:nko47mu7w2rwf6f5q5jrseuq","handle":"moviejawn.com","displayName":"MovieJawn","avatar":"https://cdn.bsky.app/img/avatar/plain/did:plc:nko47mu7w2rwf6f5q5jrseuq/bafkreicjlebzofnaoqaz4tqxg4ieulowwfjtb4zbn736qekipqbm4u3axu","associated":{"chat":{"allowIncoming":"all"},"activitySubscription":{"allowSubscriptions":"followers"}},"labels":[],"createdAt":"2023-07-03T19:36:18.543Z","description":"Everyone’s favorite PRINT movie zine, movie review website and movie makers.\nhttps://linktr.ee/moviejawn","indexedAt":"2026-09-22T16:27:47.858Z"}},{"uri":"at://did:plc:na44tnnzcxmrmgxohiwueh4n/app.bsky.graph.listitem/3lawnq4z6fh2a","subject":{"did":"did:plc:nmharg4zqkkvakpni7yeqd6m","handle":"radiotimes.bsky.social","displayName":"Radio Times","avatar":"https://cdn.bsky.app/img/avatar/plain/did:plc:nmharg4zqkkvakpni7yeqd6m/bafkreihw7alzp5u36lvt3ntno4a4dafnwsr2czbn5i5qhhtafolu5enj7m","associated":{"activitySubscription":{"allowSubscriptions":"followers"}},"labels":[],"createdAt":"2024-02-12T12:46:45.521Z","description":"Radically improving the entertainment life of the nation. #WhatsWorthWatching","indexedAt":"2026-08-14T13:51:15.872Z"}},{"uri":"at://did:plc:na44tnnzcxmrmgxohiwueh4n/app.bsky.graph.listitem/3lazj5ihw7w2u","subject":{"did":"did:plc:ga3vfhuxwgxg2xp3k4kzbtj6","handle":"sfxmagazine.bsky.social","displayName":"SFX magazine","avatar":"https://cdn.bsky.app/img/avatar/plain/did:plc:ga3vfhuxwgxg2xp3k4kzbtj6/bafkreidqhcf3ybru25py2fuebwc2xycglqq6x7hhftiouvvmaaz2xgkrqm","associated":{"chat":{"allowIncoming":"following","allowGroupInvites":"none"},"activitySubscription":{"allowSubscriptions":"followers"}},"labels":[],"createdAt":"2023-09-19T14:08:19.823Z","description":"The world's number one sci-fi, fantasy & horror magazine. Welcoming fans, geeks and aficionados since 1995. Yep, we know it looks a bit like SEX.🙄","indexedAt":"2026-10-07T11:47:59.464Z"}},{"uri":"at://did:plc:na44tnnzcxmrmgxohiwueh4n/app.bsky.graph.listitem/3lawnq4z6fa2a","subject":{"did":"did:plc:na44tnnzcxmrmgxohiwueh4n","handle":"filmcritics.org.uk","displayName":"Film Critics Association UK 🎥","avatar":"https://cdn.bsky.app/img/avatar/plain/did:plc:na44tnnzcxmrmgxohiwueh4n/bafkreifbqyl5hiyamhj3lvm54n4qggu3z5had4jturxytk7v4nky4twlem","associated":{"chat":{"allowIncoming":"none","allowGroupInvites":"none"},"activitySubscription":{"allowSubscriptions":"followers"}},"labels":[],"createdAt":"2024-10-17T21:04:24.009Z","description":"The national association for Film Critics based in the UK.\n\nContact @dgfilmhounds.bsky.social","indexedAt":"2026-01-22T17:25:20.525Z"}},{"uri":"at://did:plc:na44tnnzcxmrmgxohiwueh4n/app.bsky.graph.listitem/3lawnq4z6fe2a","subject":{"did":"did:plc:h4sdeit3xhlrwsucfeiajcza","handle":"filmhounds.co.uk","displayName":"FILMHOUNDS Magazine","avatar":"https://cdn.bsky.app/img/avatar/plain/did:plc:h4sdeit3xhlrwsucfeiajcza/bafkreiadkozxdclkvgwafhhgsojyqu6dwr6xn44sr37umyfgbujwbg5fue","associated":{"chat":{"allowIncoming":"none","allowGroupInvites":"none"},"activitySubscription":{"allowSubscriptions":"followers"}},"labels":[],"createdAt":"2023-10-10T21:13:22.159Z","description":"Available in print from filmhounds.co.uk","indexedAt":"2026-10-14T20:34:55.758Z"}},{"uri":"at://did:plc:na44tnnzcxmrmgxohiwueh4n/app.bsky.graph.listitem/3mczpej4d742l","subject":{"did":"did:plc:3h2mxnm76cfip65bjvafi2bg","handle":"latepictureshow.bsky.social","displayName":"Late Nite Picture Show","avatar":"https://cdn.bsky.app/img/avatar/plain/did:plc:3h2mxnm76cfip65bjvafi2bg/bafkreih75mdnazbq7rl3g77dqhlutkvm42o4d6rsqmheipe4jnlg5eg62a","associated":{"chat":{"allowIncoming":"none"},"activitySubscription":{"allowSubscriptions":"followers"}},"labels":[],"createdAt":"2025-09-15T11:05:51.297Z","description":"A Magazine For Those Who Think Films\n\n- Current Issue - A Year in Review\n- Previous Issue- London Film Festival\n\nwww.LateNitePictureShow.com\n\n📍London","indexedAt":"2026-01-22T17:38:24.726Z"}}]},{"uri":"at://did:plc:m7h633sxyqlzwlhdubecv4uy/app.bsky.graph.starterpack/3lggcx32mgu23","cid":"bafyreigbqzmnwqk6larzrtakh4r2d7ojjw46a6yzfrgmzpr3af5c2hvbhu","record":{"$type":"app.bsky.graph.starterpack","createdAt":"2025-01-23T16:37:32.408Z","feeds":[],"list":"at://did:plc:m7h633sxyqlzwlhdubecv4uy/app.bsky.graph.list/3lggcx2qvwj2m","name":"Gaming : Studios, Publishers, Media & Leakers","updatedAt":"2025-02-26T06:24:41.024Z"},"creator":{"did":"did:plc:m7h633sxyqlzwlhdubecv4uy","handle":"x3nu.bsky.social","displayName":"","avatar":"https://cdn.bsky.app/img/avatar/plain/did:plc:m7h633sxyqlzwlhdubecv4uy/bafkreihvvkifokmdm5nhqckasbftkrg5rs3u4fgm6makjlk5rv5xvabznu","associated":{"activitySubscription":{"allowSubscriptions":"followers"}},"labels":[],"createdAt":"2024-11-13T17:28:30.684Z"},"joinedAllTimeCount":0,"joinedWeekCount":0,"labels":[],"indexedAt":"2025-02-26T06:24:41.554Z","feeds":[],"list":{"uri":"at://did:plc:m7h633sxyqlzwlhdubecv4uy/app.bsky.graph.list/3lggcx2qvwj2m","cid":"bafyreidpaxaukmaxnx5vb5q5kd6nllfyouxhw4jquy7jjk4msxrvfm5fgm","name":"Gaming & Leaks","purpose":"app.bsky.graph.defs#referencelist","listItemCount":72,"indexedAt":"2025-01-23T16:37:32.457Z","labels":[]},"listItemsSample":[{"uri":"at://did:plc:m7h633sxyqlzwlhdubecv4uy/app.bsky.graph.listitem/3lggeeffhao2a","subject":{"did":"did:plc:xwqgusybtrpm67tcwqdfmzvy","handle":"ign.com","displayName":"IGN","avatar":"https://cdn.bsky.app/img/avatar/plain/did:plc:xwqgusybtrpm67tcwqdfmzvy/bafkreifjtza52etlgjuek5iuqwi7j7izlxuanffvizj2ry5s5g4dbh47ju","associated":{"activitySubscription":{"allowSubscriptions":"followers"}},"labels":[],"createdAt":"2023-05-15T16:55:58.559Z","verification":{"verifications":[{"issuer":"did:plc:z72i7hdynmk6r22z27h6tvur","issuerDisplayName":"Bluesky","issuerHandle":"bsky.app","uri":"at://did:plc:z72i7hdynmk6r22z27h6tvur/app.bsky.graph.verification/3lndps5dw2k26","isValid":true,"createdAt":"2025-04-21T10:45:48.072Z"}],"verifiedStatus":"valid","trustedVerifierStatus":"valid"},"description":"We're all about movies, TV, and video games!\n\nign.com","indexedAt":"2026-09-16T20:21:09.553Z"}},{"uri":"at://did:plc:m7h633sxyqlzwlhdubecv4uy/app.bsky.graph.listitem/3lggeeffiam2a","subject":{"did":"did:plc:3nfshkzomgboapasu6amkhui","handle":"playstation.com","displayName":"PlayStation","avatar":"https://cdn.bsky.app/img/avatar/plain/did:plc:3nfshkzomgboapasu6amkhui/bafkreic6hd57x7uoxrzsjku76stwatskovjznvevuq4n4rdzmkd7se25n4","associated":{"chat":{"allowIncoming":"none","allowGroupInvites":"none"},"activitySubscription":{"allowSubscriptions":"followers"}},"labels":[],"createdAt":"2023-04-29T16:10:54.361Z","verification":{"verifications":[{"issuer":"did:plc:z72i7hdynmk6r22z27h6tvur","issuerDisplayName":"Bluesky","issuerHandle":"bsky.app","uri":"at://did:plc:z72i7hdynmk6r22z27h6tvur/app.bsky.graph.verification/3lndpseyz6i2m","isValid":true,"createdAt":"2025-04-21T10:45:56.113Z"}],"verifiedStatus":"valid","trustedVerifierStatus":"none"},"description":"Official PlayStation account. Updates on PS5, PlayStation VR2, PlayStation Plus, PS4 and more.","indexedAt":"2026-09-30T15:10:13.755Z"}},{"uri":"at://did:plc:m7h633sxyqlzwlhdubecv4uy/app.bsky.graph.listitem/3lggeeffiak2a","subject":{"did":"did:plc:epti2tfjvwhnvwliibxe5koh","handle":"nintendoamericax.bsky.social","displayName":"Nintendo of America [Twitter/X Mirror]","avatar":"https://cdn.bsky.app/img/avatar/plain/did:plc:epti2tfjvwhnvwliibxe5koh/bafkreiffcfq56fzcjtnlcras7tikttjw5uzgxou34eo6ycbbxu5z6be4ku","associated":{"chat":{"allowIncoming":"following"},"activitySubscription":{"allowSubscriptions":"followers"}},"labels":[],"createdAt":"2023-08-13T23:24:49.311Z","description":"Welcome to the unofficial Nintendo of America Bluesky account, where you'll find the latest Nintendo news! For ESRB ratings go to esrb.org.\n\n[Mirror of @NintendoAmerica on Twitter/X. Not automated—posts may be delayed or omitted. Not endorsed by Nintendo.]","indexedAt":"2026-10-14T14:41:13.057Z"}},{"uri":"at://did:plc:m7h633sxyqlzwlhdubecv4uy/app.bsky.graph.listitem/3lggeeffhal2a","subject":{"did":"did:plc:mdivtinxejsqcjqwseqykpyj","handle":"gamedeveloper.bsky.social","displayName":"Game Developer","avatar":"https://cdn.bsky.app/img/avatar/plain/did:plc:mdivtinxejsqcjqwseqykpyj/bafkreiaofo2ksjpt5fre6zzpzk4bxxculioqgq5vj66lsqia6q7m4pbmbi","associated":{"chat":{"allowIncoming":"none","allowGroupInvites":"none"},"activitySubscription":{"allowSubscriptions":"followers"}},"labels":[],"createdAt":"2023-07-05T20:58:27.513Z","verification":{"verifications":[{"issuer":"did:plc:z72i7hdynmk6r22z27h6tvur","issuerDisplayName":"Bluesky","issuerHandle":"bsky.app","uri":"at://did:plc:z72i7hdynmk6r22z27h6tvur/app.bsky.graph.verification/3mdnppgthtt26","isValid":true,"createdAt":"2026-01-30T16:25:10.434Z"}],"verifiedStatus":"valid","trustedVerifierStatus":"none"},"description":"Serving the video game industry through breaking news, insight on game industry trends, expert advice & resources, and more.\n\nPress requests: news@gamedeveloper.com\n\nTips: tips@gamedeveloper.com","indexedAt":"2026-01-27T10:03:05.332Z"}},{"uri":"at://did:plc:m7h633sxyqlzwlhdubecv4uy/app.bsky.graph.listitem/3lggeeffhas2a","subject":{"did":"did:plc:n46qe2qoi6qgo6fbknctquzi","handle":"giantbomb.com","displayName":"Giant Bomb","avatar":"https://cdn.bsky.app/img/avatar/plain/did:plc:n46qe2qoi6qgo6fbknctquzi/bafkreibwaootgevtzjfzgk3novjfutcnasimqk7poqtamk2idh3jw6rewq","associated":{"chat":{"allowIncoming":"none","allowGroupInvites":"none"},"activitySubscription":{"allowSubscriptions":"followers"}},"labels":[],"createdAt":"2023-07-05T15:16:55.763Z","verification":{"verifications":[{"issuer":"did:plc:z72i7hdynmk6r22z27h6tvur","issuerDisplayName":"Bluesky","issuerHandle":"bsky.app","uri":"at://did:plc:z72i7hdynmk6r22z27h6tvur/app.bsky.graph.verification/3mkapdyctep2y","isValid":true,"createdAt":"2026-04-24T14:10:30.940Z"}],"verifiedStatus":"valid","trustedVerifierStatus":"none"},"status":{"uri":"at://did:plc:n46qe2qoi6qgo6fbknctquzi/app.bsky.actor.status/self","cid":"bafyreifbherqywfshvtjnksptju6woxf3qhmwgjeyozqv3lvxsupapijb4","record":{"$type":"app.bsky.actor.status","createdAt":"2026-09-16T18:19:49.288Z","durationMinutes":120,"embed":{"$type":"app.bsky.embed.external","external":{"$type":"app.bsky.embed.external#external","description":"Grubberdillo (Cyberdillo) | Blight Club | Streaming cyberdillo for 613 viewers.","thumb":{"$type":"blob","ref":{"$link":"bafkreidgexme6b6z4zqrsa5akglmu7hrqklewavm6pqgpiacotr3rwte7y"},"mimeType":"image/jpeg","size":115049},"title":"GiantBomb - Live on Twitch","uri":"https://twitch.tv/giantbomb"}},"status":"app.bsky.actor.status#live"},"status":"app.bsky.actor.status#live","embed":{"external":{"uri":"https://twitch.tv/giantbomb","title":"GiantBomb - Live on Twitch","description":"Grubberdillo (Cyberdillo) | Blight Club | Streaming cyberdillo for 613 viewers.","thumb":"https://cdn.bsky.app/img/feed_thumbnail/plain/did:plc:n46qe2qoi6qgo6fbknctquzi/bafkreidgexme6b6z4zqrsa5akglmu7hrqklewavm6pqgpiacotr3rwte7y"},"$type":"app.bsky.embed.external#view"},"labels":[],"expiresAt":"2026-09-16T20:19:49.288Z","isActive":false},"description":"The Yacht Rock of Video Games\ngiantbomb.com/join\n\nFor business inquiries: giantbomb@rightclick.gg\nMedia: media@giantbomb.com","indexedAt":"2026-10-15T17:33:42.159Z"}},{"uri":"at://did:plc:m7h633sxyqlzwlhdubecv4uy/app.bsky.graph.listitem/3lggeeffhau2a","subject":{"did":"did:plc:m5wvwfth6pdkxyoee7ug3v2v","handle":"rockpapershotgun.bsky.social","displayName":"Rock Paper Shotgun","avatar":"https://cdn.bsky.app/img/avatar/plain/did:plc:m5wvwfth6pdkxyoee7ug3v2v/bafkreifebbh5hewqlye47jiidjj4aeprq5ckgo6orjqkwh74vuohrt2ctu","associated":{"chat":{"allowIncoming":"none","allowGroupInvites":"none"},"activitySubscription":{"allowSubscriptions":"followers"}},"labels":[],"createdAt":"2023-10-05T10:01:16.419Z","verification":{"verifications":[{"issuer":"did:plc:xwqgusybtrpm67tcwqdfmzvy","issuerDisplayName":"IGN","issuerHandle":"ign.com","uri":"at://did:plc:xwqgusybtrpm67tcwqdfmzvy/app.bsky.graph.verification/3lrgz7ioxdm2g","isValid":true,"createdAt":"2025-06-12T22:53:05.669Z"}],"verifiedStatus":"valid","trustedVerifierStatus":"none"},"description":"We write about PC games, and things to do with PC games, at http://rockpapershotgun.com.","indexedAt":"2024-02-07T11:39:50.624Z"}},{"uri":"at://did:plc:m7h633sxyqlzwlhdubecv4uy/app.bsky.graph.listitem/3lggeeffhat2a","subject":{"did":"did:plc:6kzbvhxuaw6637ojmt22g6fw","handle":"annapurna.com","displayName":"ANNAPURNA","avatar":"https://cdn.bsky.app/img/avatar/plain/did:plc:6kzbvhxuaw6637ojmt22g6fw/bafkreicf4l26hcu7a7r7cs6fnzpm4umgvxrnxlmudh7qpjncrarn3jeksa","associated":{"activitySubscription":{"allowSubscriptions":"followers"}},"labels":[],"createdAt":"2024-10-18T22:27:46.806Z","verification":{"verifications":[{"issuer":"did:plc:z72i7hdynmk6r22z27h6tvur","issuerDisplayName":"Bluesky","issuerHandle":"bsky.app","uri":"at://did:plc:z72i7hdynmk6r22z27h6tvur/app.bsky.graph.verification/3lndpsdyhyq2f","isValid":true,"createdAt":"2025-04-21T10:45:55.033Z"}],"verifiedStatus":"valid","trustedVerifierStatus":"none"},"description":"an independent studio focused on creating high-quality stories and experiences across Live Action and Animated Film and Television, Live Theater, and Interactive Gaming.","indexedAt":"2026-10-08T13:32:56.458Z"}},{"uri":"at://did:plc:m7h633sxyqlzwlhdubecv4uy/app.bsky.graph.listitem/3lggeeffham2a","subject":{"did":"did:plc:ikcgqus5qu2o3gcjosb4fklu","handle":"devolverdigital.com","displayName":"Devolver Digital","avatar":"https://cdn.bsky.app/img/avatar/plain/did:plc:ikcgqus5qu2o3gcjosb4fklu/bafkreih7n2g56elsyqugsjtqitmqkk2wcmd7efcqebrtkd7c3twue5lhcm","associated":{"chat":{"allowIncoming":"following"},"activitySubscription":{"allowSubscriptions":"followers"}},"labels":[],"createdAt":"2023-07-03T14:59:00.395Z","verification":{"verifications":[{"issuer":"did:plc:z72i7hdynmk6r22z27h6tvur","issuerDisplayName":"Bluesky","issuerHandle":"bsky.app","uri":"at://did:plc:z72i7hdynmk6r22z27h6tvur/app.bsky.graph.verification/3lvrcfucynr2c","isValid":true,"createdAt":"2025-08-06T22:45:06.579Z"}],"verifiedStatus":"valid","trustedVerifierStatus":"none"},"description":"A person you meet in the forest, who just puts their finger to your lips to shoosh you, and then gives you the budget for your game.","indexedAt":"2026-08-27T20:21:56.067Z"}},{"uri":"at://did:plc:m7h633sxyqlzwlhdubecv4uy/app.bsky.graph.listitem/3lggeeffhan2a","subject":{"did":"did:plc:5gmpuanuw3fsf2z3hc3zawow","handle":"arcsystemworks.com","displayName":"Arc System Works America","avatar":"https://cdn.bsky.app/img/avatar/plain/did:plc:5gmpuanuw3fsf2z3hc3zawow/bafkreigwl42nocomcu4j3fgdg5rtyicblo2buyirns5ydx5mvd6ensvema","associated":{"activitySubscription":{"allowSubscriptions":"followers"}},"labels":[],"createdAt":"2024-06-10T18:46:12.911Z","verification":{"verifications":[{"issuer":"did:plc:z72i7hdynmk6r22z27h6tvur","issuerDisplayName":"Bluesky","issuerHandle":"bsky.app","uri":"at://did:plc:z72i7hdynmk6r22z27h6tvur/app.bsky.graph.verification/3lwutuixvcj2l","isValid":true,"createdAt":"2025-08-21T02:00:42.418Z"}],"verifiedStatus":"valid","trustedVerifierStatus":"none"},"description":"Official BlueSky account for Arc System Works America, Inc., the publishers of the Guilty Gear & BlazBlue series.\nWebsite: arcsystemworks.com","indexedAt":"2026-09-28T23:08:39.561Z"}},{"uri":"at://did:plc:m7h633sxyqlzwlhdubecv4uy/app.bsky.graph.listitem/3lggeeffial2a","subject":{"did":"did:plc:qap6gpor3iaifteqmkmqvklu","handle":"nintendolife.com","displayName":"Nintendo Life","avatar":"https://cdn.bsky.app/img/avatar/plain/did:plc:qap6gpor3iaifteqmkmqvklu/bafkreiambaj5uraw3xnds2zqjjobp6c6eitl53z37sct2mzmh2nt35qfce","associated":{"chat":{"allowIncoming":"none","allowGroupInvites":"none"},"activitySubscription":{"allowSubscriptions":"followers"}},"labels":[],"createdAt":"2024-02-06T15:29:34.092Z","verification":{"verifications":[{"issuer":"did:plc:xwqgusybtrpm67tcwqdfmzvy","issuerDisplayName":"IGN","issuerHandle":"ign.com","uri":"at://did:plc:xwqgusybtrpm67tcwqdfmzvy/app.bsky.graph.verification/3lrgz7rp5gm23","isValid":true,"createdAt":"2025-06-12T22:53:15.133Z"}],"verifiedStatus":"valid","trustedVerifierStatus":"none"},"description":"The latest Nintendo Switch news, reviews and videos. We are the most visited #Nintendo site in the world. DMs open. #NintendoSwitch #Switch #3DS\nwww.nintendolife.com","indexedAt":"2025-02-14T10:19:48.044Z"}},{"uri":"at://did:plc:m7h633sxyqlzwlhdubecv4uy/app.bsky.graph.listitem/3lggeeffgbc2a","subject":{"did":"did:plc:4nci4ly3xu3nau7hzr373bte","handle":"remedygames.com","displayName":"Remedy Entertainment","avatar":"https://cdn.bsky.app/img/avatar/plain/did:plc:4nci4ly3xu3nau7hzr373bte/bafkreiaqqpdfnl7kq4oumuv6a5vi262h35j3cy54jbllpgotsuws4imb7i","associated":{"chat":{"allowIncoming":"following"},"activitySubscription":{"allowSubscriptions":"followers"}},"labels":[],"createdAt":"2024-10-21T09:43:31.708Z","verification":{"verifications":[{"issuer":"did:plc:z72i7hdynmk6r22z27h6tvur","issuerDisplayName":"Bluesky","issuerHandle":"bsky.app","uri":"at://did:plc:z72i7hdynmk6r22z27h6tvur/app.bsky.graph.verification/3lvn6mnhlgr2d","isValid":true,"createdAt":"2025-08-05T07:26:40.298Z"}],"verifiedStatus":"valid","trustedVerifierStatus":"none"},"description":"Creating extraordinary worlds and inviting you down the rabbit hole. Making games since 1995. #CONTROLResonant out now! \n\nNow working on Max Payne 1 & 2 Remake.","indexedAt":"2026-09-24T14:41:49.060Z"}}]}]}
            """;
        using JsonDocument document = JsonDocument.Parse(body);
        using TestServer server = CreateServer(body);
        using BlueskyAgent agent = CreateAgent(server);

        AtProtoHttpResult<RecommendationReadOnlyCollection<StarterPackView>> result = await agent.GetSuggestedStarterPacksWithRecommendation(
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Equal(document.RootElement.GetProperty("starterPacks").GetArrayLength(), result.Result.Count);
        Assert.NotEmpty(result.Result);
        Assert.Null(result.Result.RecommendationId);
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public async Task SuggestionsRouteToPdsOnlyWhenAuthenticated(bool legacy, bool authenticated)
    {
        Uri pds = new("https://pds.test.invalid");
        Uri appView = new("https://appview.test.invalid");
        Did did = new("did:plc:test");
        string accessJwt = JwtBuilder.CreateJwt(did, pds.ToString());
        using TestServer server = CreateServer(
            """{"starterPacks":[],"recIdStr":"synthetic-routing-recommendation"}""",
            request =>
            {
                Assert.Equal(authenticated ? pds.Host : appView.Host, request.Host.Host);
                Assert.Equal(authenticated ? $"Bearer {accessJwt}" : string.Empty, request.Headers.Authorization.ToString());
            });
        using BlueskyAgent agent = new(
            new TestHttpClientFactory(server),
            new BlueskyAgentOptions { PublicAppViewUri = appView })
        {
            Service = pds
        };
        if (authenticated)
        {
            agent.Credentials = new AccessCredentials(
                pds,
                AuthenticationType.UsernamePassword,
                accessJwt,
                "refreshToken");
        }

        Assert.Equal(authenticated, agent.IsAuthenticated);
        if (legacy)
        {
            AtProtoHttpResult<ICollection<StarterPackView>> result = await agent.GetSuggestedStarterPacks(
                cancellationToken: TestContext.Current.CancellationToken);
            Assert.True(result.Succeeded);
            Assert.Empty(result.Result);
        }
        else
        {
            AtProtoHttpResult<RecommendationReadOnlyCollection<StarterPackView>> result = await agent.GetSuggestedStarterPacksWithRecommendation(
                cancellationToken: TestContext.Current.CancellationToken);
            Assert.True(result.Succeeded);
            Assert.Empty(result.Result);
            Assert.Equal("synthetic-routing-recommendation", result.Result.RecommendationId);
        }
    }

    private static BlueskyAgent CreateAgent(TestServer server) =>
        new(new TestHttpClientFactory(server), new BlueskyAgentOptions { PublicAppViewUri = TestServerBuilder.DefaultUri })
        {
            Service = TestServerBuilder.DefaultUri
        };

    private static TestServer CreateServer(string body, Action<HttpRequest>? inspect = null, HttpStatusCode statusCode = HttpStatusCode.OK) =>
        TestServerBuilder.CreateServer(TestServerBuilder.DefaultUri, async context =>
        {
            Assert.Equal("/xrpc/app.bsky.unspecced.getSuggestedStarterPacks", context.Request.Path.ToString());
            inspect?.Invoke(context.Request);
            context.Response.StatusCode = (int)statusCode;
            context.Response.ContentType = "application/json";
            context.Response.Headers["x-synthetic-response"] = "preserved";
            await context.Response.WriteAsync(body, TestContext.Current.CancellationToken);
        });

    private static void AssertFailure<T>(AtProtoHttpResult<T> result)
    {
        Assert.False(result.Succeeded);
        Assert.Null(result.Result);
        Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode);
        Assert.Equal("InvalidRequest", result.AtErrorDetail?.Error);
        Assert.Equal("Synthetic failure.", result.AtErrorDetail?.Message);
        Assert.NotNull(result.HttpResponseHeaders);
        Assert.Equal("preserved", Assert.Single(result.HttpResponseHeaders.GetValues("x-synthetic-response")));
    }
}

#pragma warning restore BSKYUnspecced
