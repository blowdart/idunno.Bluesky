// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Text.Json;

using Duende.IdentityModel.OidcClient.DPoP;

using idunno.AtProto;
using idunno.AtProto.Authentication;
using idunno.Bluesky.Embed.External;
using idunno.Bluesky.Unspecced;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Logging;

namespace idunno.Bluesky.Integration.Test;

[ExcludeFromCodeCoverage]
public class AtmosphereExploreTests
{
    [Fact(Explicit = true)]
    public void ReflectionIsDisabledForExplicitMetadataValidation()
    {
        Assert.False(JsonSerializer.IsReflectionEnabledByDefault);
    }

    private static readonly string[] s_expectedLanguages = ["en", "fr&x=y"];

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExploreUsesTheCorrectServiceAndReplaysCapturedPublicData(bool authenticated)
    {
        // Public unauthenticated capture: HTTP 200, 2026-10-15T20:10:40Z.
        // Source: https://public.api.bsky.app/xrpc/app.bsky.unspecced.getAtmosphereExploreTab
        // Observed: Articles, publications with themes, galleries with images, an announcement and an application. No livestreams.
        const string json = """
            {"announcementBanner":{"id":"bd4b135a8995","title":"Atmosphere Tab Launch Party 2026","description":"Boop","url":"https://bsky.app","image":"https://app-cms-production.bsky.workers.dev/images/image-0a7748426cbd27b63c04a608d930120d2beb9727-3000x2000-jpg/large","overlayColor":"#000","textColor":"#fff"},"articles":[{"featured":true,"view":{"associatedRefs":[{"uri":"at://did:plc:ifn645rwvsuolxg7o3w7ouo4/site.standard.document/3mwqgs7dp2s2a","cid":"bafyreihcxcofz3zyrmgismvprjradsr7srdmaqy2y6ze7nt5slasfr3bsm","$type":"com.atproto.repo.strongRef"},{"uri":"at://did:plc:ifn645rwvsuolxg7o3w7ouo4/site.standard.publication/3mvimt7nmrs2m","cid":"bafyreic6arm47nzk5symkeafndsqjbtwbbb3pmne6qpgycpfwaowcrunnq","$type":"com.atproto.repo.strongRef"}],"associatedProfiles":[{"did":"did:plc:ifn645rwvsuolxg7o3w7ouo4","handle":"aster.id","displayName":"Aster ID","avatar":"https://cdn.bsky.app/img/avatar/plain/did:plc:ifn645rwvsuolxg7o3w7ouo4/bafkreicid7hoyx5d2if24eryyhb3lg4ioy2mdrmiriga3k2cay6k5io4ta","associated":{"chat":{"allowIncoming":"all","allowGroupInvites":"all"},"activitySubscription":{"allowSubscriptions":"followers"}},"labels":[],"createdAt":"2026-08-31T07:23:03.599Z"}],"labels":[],"uri":"https://blog.aster.place/3mwqgs7dp2s2a","title":"Announcing Aster - a home for researchers on the open web ","description":"","createdAt":"2026-09-30T13:22:17.189Z","image":"https://cdn.bsky.app/img/feed_thumbnail/plain/did:plc:ifn645rwvsuolxg7o3w7ouo4/bafkreidvntdnpe3nzj7wkdxjuhutbf5g7lozznoe7gosqgw3xc2mx3i3om","publisher":{"associatedRefs":[{"uri":"at://did:plc:ifn645rwvsuolxg7o3w7ouo4/site.standard.publication/3mvimt7nmrs2m","cid":"bafyreic6arm47nzk5symkeafndsqjbtwbbb3pmne6qpgycpfwaowcrunnq","$type":"com.atproto.repo.strongRef"}],"associatedProfiles":[{"did":"did:plc:ifn645rwvsuolxg7o3w7ouo4","handle":"aster.id","displayName":"Aster ID","avatar":"https://cdn.bsky.app/img/avatar/plain/did:plc:ifn645rwvsuolxg7o3w7ouo4/bafkreicid7hoyx5d2if24eryyhb3lg4ioy2mdrmiriga3k2cay6k5io4ta","associated":{"chat":{"allowIncoming":"all","allowGroupInvites":"all"},"activitySubscription":{"allowSubscriptions":"followers"}},"labels":[],"createdAt":"2026-08-31T07:23:03.599Z"}],"labels":[],"uri":"https://blog.aster.place/","title":"Aster Blog","description":"","logo":"https://cdn.bsky.app/img/avatar/plain/did:plc:ifn645rwvsuolxg7o3w7ouo4/bafkreia2oxf6i34p25yfny2joydlaqpgkzn2x7pipstjayrx7oziw7ue4i","theme":{"background":"#fdfcfa","foreground":"#000000","accent":"#3b2a7e","accentForeground":"#ffffff","$type":"app.bsky.embed.external#viewArticlePublicationTheme"},"subscriptionCount":36,"subscribers":[{"did":"did:plc:6n2ngs7zpcpwxz3jaoxj56tu","handle":"stau.space","displayName":"󱤑󱥡","pronouns":"she/her/ella","avatar":"https://cdn.bsky.app/img/avatar/plain/did:plc:6n2ngs7zpcpwxz3jaoxj56tu/bafkreici4t74nqximpcuyucuvuq23lokaijlvza6hs5jssznghafjwsk7q","associated":{"chat":{"allowIncoming":"all","allowGroupInvites":"all"},"activitySubscription":{"allowSubscriptions":"followers"}},"labels":[],"createdAt":"2025-08-06T13:09:51.497Z"},{"did":"did:plc:tsjjh5uzepibeoujgscgfgo4","handle":"jaredlemler.com","displayName":"Jared Lemler","avatar":"https://cdn.bsky.app/img/avatar/plain/did:plc:tsjjh5uzepibeoujgscgfgo4/bafkreiabiax3onjwvmnk4bodhtkks5vi7y2om2ki5fapfxuv3s24nneosq","associated":{"activitySubscription":{"allowSubscriptions":"followers"},"germ":{"showButtonTo":"usersIFollow","messageMeUrl":"https://landing.ger.mx/newUser"}},"labels":[],"createdAt":"2025-05-27T13:13:00.740Z"},{"did":"did:plc:yf2e5shthfoyrakhmerh4kst","handle":"tomredford.eu","displayName":"Tom","pronouns":"he/him","avatar":"https://cdn.bsky.app/img/avatar/plain/did:plc:yf2e5shthfoyrakhmerh4kst/bafkreiafc2cawwvjwuctqo4vi2nmye33633cvbywrrd4gjtid3qyhoecxq","associated":{"chat":{"allowIncoming":"none","allowGroupInvites":"none"},"activitySubscription":{"allowSubscriptions":"followers"}},"labels":[{"src":"did:plc:yf2e5shthfoyrakhmerh4kst","uri":"at://did:plc:yf2e5shthfoyrakhmerh4kst/app.bsky.actor.profile/self","cid":"bafyreidgrgc7o5l2fo7sevggk36cuak7qt2ar4l4grokvxvqphquwj22aa","val":"!no-unauthenticated","cts":"1970-01-01T00:00:00.000Z"}],"createdAt":"2023-12-01T08:15:00.552Z"}],"$type":"app.bsky.embed.external#viewArticlePublication"},"likeCount":13,"likers":[{"did":"did:plc:6n2ngs7zpcpwxz3jaoxj56tu","handle":"stau.space","displayName":"󱤑󱥡","pronouns":"she/her/ella","avatar":"https://cdn.bsky.app/img/avatar/plain/did:plc:6n2ngs7zpcpwxz3jaoxj56tu/bafkreici4t74nqximpcuyucuvuq23lokaijlvza6hs5jssznghafjwsk7q","associated":{"chat":{"allowIncoming":"all","allowGroupInvites":"all"},"activitySubscription":{"allowSubscriptions":"followers"}},"labels":[],"createdAt":"2025-08-06T13:09:51.497Z"},{"did":"did:plc:pbjvqaziagcyv2vqodldn5op","handle":"felixs.dev","displayName":"Felix Schneider","avatar":"https://cdn.bsky.app/img/avatar/plain/did:plc:pbjvqaziagcyv2vqodldn5op/bafkreieedhblw2ljvpmoec7xkby7q5k5uw767rnor4sbjjwrdlsv7fuvei","associated":{"chat":{"allowIncoming":"all","allowGroupInvites":"all"},"activitySubscription":{"allowSubscriptions":"followers"}},"labels":[],"createdAt":"2024-12-30T07:33:46.442Z"},{"did":"did:plc:tsjjh5uzepibeoujgscgfgo4","handle":"jaredlemler.com","displayName":"Jared Lemler","avatar":"https://cdn.bsky.app/img/avatar/plain/did:plc:tsjjh5uzepibeoujgscgfgo4/bafkreiabiax3onjwvmnk4bodhtkks5vi7y2om2ki5fapfxuv3s24nneosq","associated":{"activitySubscription":{"allowSubscriptions":"followers"},"germ":{"showButtonTo":"usersIFollow","messageMeUrl":"https://landing.ger.mx/newUser"}},"labels":[],"createdAt":"2025-05-27T13:13:00.740Z"}],"$type":"app.bsky.embed.external#viewArticle"}},{"featured":false,"view":{"associatedRefs":[{"uri":"at://did:plc:hsh4dq2dskqs2qxaxfpkelm5/site.standard.document/3mvghkbe4z523","cid":"bafyreif7kncjt62pj3thhpjxp3cujyxj5pwq2mcwbsxectqe67hgxuzy44","$type":"com.atproto.repo.strongRef"},{"uri":"at://did:plc:hsh4dq2dskqs2qxaxfpkelm5/site.standard.publication/3mi2zwmczuj27","cid":"bafyreigmgwhr7acc7rn7pqd7pj7uqd63qycgn473paixztwhasqiwjlcgy","$type":"com.atproto.repo.strongRef"}],"associatedProfiles":[{"did":"did:plc:hsh4dq2dskqs2qxaxfpkelm5","handle":"3-3.fyi","displayName":"Three and Three","avatar":"https://cdn.bsky.app/img/avatar/plain/did:plc:hsh4dq2dskqs2qxaxfpkelm5/bafkreigglochcanbj42rt2mncqsntlfdlme2htvwbmrqsptflavze7ltla","associated":{"activitySubscription":{"allowSubscriptions":"followers"}},"labels":[],"createdAt":"2025-01-31T02:21:38.644Z"}],"labels":[],"uri":"https://three-and-three.com/a/3mvghkbe4z523-issue-018","title":"Issue 018","description":"The September Edition","createdAt":"2026-09-14T12:00:00+00:00","image":"https://cdn.bsky.app/img/feed_thumbnail/plain/did:plc:hsh4dq2dskqs2qxaxfpkelm5/bafkreibwzolzic5rgxptyfwwxqvixv6rdhfmu3mvn5bx6ltttrjb2apiyu","publisher":{"associatedRefs":[{"uri":"at://did:plc:hsh4dq2dskqs2qxaxfpkelm5/site.standard.publication/3mi2zwmczuj27","cid":"bafyreigmgwhr7acc7rn7pqd7pj7uqd63qycgn473paixztwhasqiwjlcgy","$type":"com.atproto.repo.strongRef"}],"associatedProfiles":[{"did":"did:plc:hsh4dq2dskqs2qxaxfpkelm5","handle":"3-3.fyi","displayName":"Three and Three","avatar":"https://cdn.bsky.app/img/avatar/plain/did:plc:hsh4dq2dskqs2qxaxfpkelm5/bafkreigglochcanbj42rt2mncqsntlfdlme2htvwbmrqsptflavze7ltla","associated":{"activitySubscription":{"allowSubscriptions":"followers"}},"labels":[],"createdAt":"2025-01-31T02:21:38.644Z"}],"labels":[],"uri":"https://three-and-three.com/","title":"Three & Three","description":"A well-balanced contradiction. A community for creative people who want to talk shop & also run around the mall with their friends. Every month, we host a virtual meetup & drop 6 links in your inbox: 3 to stay ready, 3 to stay curious.","logo":"https://cdn.bsky.app/img/avatar/plain/did:plc:hsh4dq2dskqs2qxaxfpkelm5/bafkreibpcizubltrdquttw54iio5tq7tshcgnba6zxqxexsaaq2bugz3oi","theme":{"background":"#ffffff","foreground":"#001501","accent":"#008e16","accentForeground":"#c6ffc8","$type":"app.bsky.embed.external#viewArticlePublicationTheme"},"subscriptionCount":9,"subscribers":[{"did":"did:plc:e5rjqwr35a2af5mrcrvcls6p","handle":"salihaydin.bsky.social","displayName":"Salih Aydın","avatar":"https://cdn.bsky.app/img/avatar/plain/did:plc:e5rjqwr35a2af5mrcrvcls6p/bafkreib7c2p534ydv2d7wv5xtvbj6iin54l67jhlfji7bgye3lajt6ifpe","associated":{"activitySubscription":{"allowSubscriptions":"followers"}},"labels":[],"createdAt":"2023-05-02T02:53:52.393Z"},{"did":"did:plc:glgaiwhklx63ovajguvpxm2q","handle":"jlopes.eu","displayName":"J Lopes","pronouns":"","avatar":"https://cdn.bsky.app/img/avatar/plain/did:plc:glgaiwhklx63ovajguvpxm2q/bafkreidyqccz2miccpkocnsz752vwdpqcf5pp27pbtwefjteuznhep7eg4","associated":{"chat":{"allowIncoming":"all","allowGroupInvites":"following"},"activitySubscription":{"allowSubscriptions":"followers"}},"labels":[],"createdAt":"2023-08-24T08:39:22.561Z"},{"did":"did:plc:iuxoim42p7vdkremfa3ozmku","handle":"zafarali.me","displayName":"","avatar":"https://cdn.bsky.app/img/avatar/plain/did:plc:iuxoim42p7vdkremfa3ozmku/bafkreicmbdl4t5tvhmov3p3d56jucuc4sfaovdag6un6va3ccimrkwbd4i","associated":{"chat":{"allowIncoming":"none","allowGroupInvites":"none"},"activitySubscription":{"allowSubscriptions":"followers"}},"labels":[],"createdAt":"2024-11-18T18:07:28.846Z"}],"$type":"app.bsky.embed.external#viewArticlePublication"},"readingTime":4,"likeCount":0,"likers":[],"$type":"app.bsky.embed.external#viewArticle"}}],"publications":[{"featured":false,"view":{"associatedRefs":[{"uri":"at://did:plc:v46ojbiop5ebs5h7gaomixcc/site.standard.publication/3m57zfsq3a2fo","cid":"bafyreietv2vf4ixpdwqfekhlhkf7pf65dz24wubraz5zh472uyijll3hue","$type":"com.atproto.repo.strongRef"}],"associatedProfiles":[{"did":"did:plc:v46ojbiop5ebs5h7gaomixcc","handle":"brookie.blog","displayName":"Brookie","avatar":"https://cdn.bsky.app/img/avatar/plain/did:plc:v46ojbiop5ebs5h7gaomixcc/bafkreiceidrk3m4ih5gk5joz4xk5iqijhsj74dv4lboo6cjzcej5gwrl6i","associated":{"chat":{"allowIncoming":"all","allowGroupInvites":"all"},"activitySubscription":{"allowSubscriptions":"followers"},"germ":{"showButtonTo":"usersIFollow","messageMeUrl":"https://landing.ger.mx/newUser"}},"labels":[],"createdAt":"2023-06-23T17:27:42.450Z","verification":{"verifications":[{"issuer":"did:plc:z72i7hdynmk6r22z27h6tvur","issuerDisplayName":"Bluesky","issuerHandle":"bsky.app","uri":"at://did:plc:z72i7hdynmk6r22z27h6tvur/app.bsky.graph.verification/3mgpx7yucrl2v","isValid":true,"createdAt":"2026-03-10T18:00:36.756Z"}],"verifiedStatus":"valid","trustedVerifierStatus":"none"}}],"labels":[],"uri":"https://brookie.blog/","title":"Brookie’s Blog","description":"hi i'm brookie and this is my blog :)","logo":"https://cdn.bsky.app/img/avatar/plain/did:plc:v46ojbiop5ebs5h7gaomixcc/bafkreif2zmdettmdbcyiborvikvzr645szqwt5b3dgo4xvsuem6oskqziy","theme":{"background":"#f5e6d3","foreground":"#292524","accent":"#b45309","accentForeground":"#f8f2ea","$type":"app.bsky.embed.external#viewArticlePublicationTheme"},"subscriptionCount":156,"subscribers":[{"did":"did:plc:3whdb534faiczugsz5fnohh6","handle":"rafms.eu","displayName":"rafa","avatar":"https://cdn.bsky.app/img/avatar/plain/did:plc:3whdb534faiczugsz5fnohh6/bafkreibdwrt4hwure6aegdsrku4i6zfdjeofxsznzze43qj73ydrbne4g4","associated":{"chat":{"allowIncoming":"none","allowGroupInvites":"none"},"activitySubscription":{"allowSubscriptions":"followers"},"germ":{"showButtonTo":"usersIFollow","messageMeUrl":"https://landing.ger.mx/newUser"}},"labels":[],"createdAt":"2023-11-18T08:49:00.619Z"},{"did":"did:plc:d6zmbofxdmyuc2r77t3jpvoq","handle":"cakeu.dev","displayName":"cakeu","avatar":"https://cdn.bsky.app/img/avatar/plain/did:plc:d6zmbofxdmyuc2r77t3jpvoq/bafkreifkrcjyel5k6xz663os4cwcojfo4v7t6z3ezpwenhoayhtwsdn5li","associated":{"activitySubscription":{"allowSubscriptions":"followers"}},"labels":[],"createdAt":"2024-12-03T17:42:52.744Z"},{"did":"did:plc:utrdz24mcbfcxor2bo2t6jsh","handle":"shiftykitsune.bsky.social","displayName":"Shifty!","avatar":"https://cdn.bsky.app/img/avatar/plain/did:plc:utrdz24mcbfcxor2bo2t6jsh/bafkreieqs2d42i5xzpn6q44fw63asnqv2lagvy2tzdzeid3uiaepxqsc6i","associated":{"activitySubscription":{"allowSubscriptions":"followers"}},"labels":[{"src":"did:plc:utrdz24mcbfcxor2bo2t6jsh","uri":"at://did:plc:utrdz24mcbfcxor2bo2t6jsh/app.bsky.actor.profile/self","cid":"bafyreifzgtw2naokg6nu5kllfwodjyuzm26hiril76bmkdtwle3gpjtvva","val":"!no-unauthenticated","cts":"2024-11-13T23:34:33.740Z"}],"createdAt":"2024-11-13T23:34:38.981Z"}],"$type":"app.bsky.embed.external#viewArticlePublication"}},{"featured":false,"view":{"associatedRefs":[{"uri":"at://did:plc:ewvi7nxzyoun6zhxrhs64oiz/site.standard.publication/3memeaezix223","cid":"bafyreifovmxsz426z7hcg2zwspz2evlzazkey4rbwiyrf5vn63gidt4i7e","$type":"com.atproto.repo.strongRef"}],"associatedProfiles":[{"did":"did:plc:ewvi7nxzyoun6zhxrhs64oiz","handle":"atproto.com","displayName":"AT Protocol Developers","avatar":"https://cdn.bsky.app/img/avatar/plain/did:plc:ewvi7nxzyoun6zhxrhs64oiz/bafkreiaihtwfr2d6mjfb6drzaze2gubqtst4ymiwxowqfxpsqxpnz2t33u","associated":{"chat":{"allowIncoming":"none"},"activitySubscription":{"allowSubscriptions":"followers"}},"labels":[],"createdAt":"2023-04-26T06:19:25.508Z","verification":{"verifications":[{"issuer":"did:plc:z72i7hdynmk6r22z27h6tvur","issuerDisplayName":"Bluesky","issuerHandle":"bsky.app","uri":"at://did:plc:z72i7hdynmk6r22z27h6tvur/app.bsky.graph.verification/3lndpv6ijlo2l","isValid":true,"createdAt":"2025-04-21T10:47:29.941Z"}],"verifiedStatus":"valid","trustedVerifierStatus":"none"}}],"labels":[],"uri":"https://atproto.com/blog","title":"AT Protocol","description":"Documentation, guides, and updates for the AT Protocol - the decentralized foundation for social networking.","theme":{"background":"#ffffff","foreground":"#18181b","accent":"#eab308","accentForeground":"#18181b","$type":"app.bsky.embed.external#viewArticlePublicationTheme"},"subscriptionCount":73,"subscribers":[{"did":"did:plc:cbbckcpqr6krasabam2slxza","handle":"buffthepixel.net","displayName":"Jonesy","pronouns":"he/him","avatar":"https://cdn.bsky.app/img/avatar/plain/did:plc:cbbckcpqr6krasabam2slxza/bafkreibanbtkgklnyemmtykx5h6pbp7egjfww6xt3qy7vnv742uxxgfpom","associated":{"activitySubscription":{"allowSubscriptions":"followers"}},"labels":[],"createdAt":"2026-04-21T07:16:17.940Z","status":{"uri":"at://did:plc:cbbckcpqr6krasabam2slxza/app.bsky.actor.status/self","cid":"bafyreifiyatl6am6adsltqh2prnnein7fqp4ql6ikrc5z2ewum7q4y7ifu","record":{"$type":"app.bsky.actor.status","createdAt":"2026-05-23T18:10:59Z","durationMinutes":10,"embed":{"$type":"app.bsky.embed.external","external":{"description":"@buffthepixel.myatproto.social is 🔴LIVE on stream.place","thumb":{"$type":"blob","ref":{"$link":"bafkreidtargn2u7uo42jmuxj5wnpyj5llzvk6njkf76s5gdrisahub7lve"},"mimeType":"image/jpeg","size":52818},"title":"How boring is Half Life 2?","uri":"https://stream.place/buffthepixel.myatproto.social"}},"status":"app.bsky.actor.status#live"},"status":"app.bsky.actor.status#live","embed":{"external":{"uri":"https://stream.place/buffthepixel.myatproto.social","title":"How boring is Half Life 2?","description":"@buffthepixel.myatproto.social is 🔴LIVE on stream.place","thumb":"https://cdn.bsky.app/img/feed_thumbnail/plain/did:plc:cbbckcpqr6krasabam2slxza/bafkreidtargn2u7uo42jmuxj5wnpyj5llzvk6njkf76s5gdrisahub7lve"},"$type":"app.bsky.embed.external#view"},"labels":[],"expiresAt":"2026-05-23T18:20:59.000Z","isActive":false}},{"did":"did:plc:tgfzv5irks5acnmk75j4elky","handle":"chadkoh.com","displayName":"Chad Kohalyk","avatar":"https://cdn.bsky.app/img/avatar/plain/did:plc:tgfzv5irks5acnmk75j4elky/bafkreiaq4e2zx3ja3ezpijlxreqhxitnieht4m5yp3fgybmv6ujlbozsii","associated":{"chat":{"allowIncoming":"none"},"activitySubscription":{"allowSubscriptions":"followers"},"germ":{"showButtonTo":"usersIFollow","messageMeUrl":"https://landing.ger.mx/newUser"}},"labels":[],"createdAt":"2023-01-18T17:40:49.591Z"},{"did":"did:plc:wiiz7qpptqyz5cj46s4po2v6","handle":"smlegitime.bsky.social","displayName":"Sybille Legitime","avatar":"https://cdn.bsky.app/img/avatar/plain/did:plc:wiiz7qpptqyz5cj46s4po2v6/bafkreia2grjkvuncrrb7rh5wggbqsp6yzkjssibutdz2rg4w233wwojcli","associated":{"activitySubscription":{"allowSubscriptions":"followers"}},"labels":[],"createdAt":"2024-09-11T22:19:04.708Z"}],"$type":"app.bsky.embed.external#viewArticlePublication"}}],"photos":[{"featured":false,"view":{"associatedRefs":[{"uri":"at://did:plc:7rkdalcyovirwkpzgzk74cje/social.grain.gallery/3mwvtii4k5ddt","cid":"bafyreiemxr6jgnye6x3gubcw6vhmflbfnp3ylex52dcitkyx53btk4wwsu","$type":"com.atproto.repo.strongRef"},{"uri":"at://did:plc:7rkdalcyovirwkpzgzk74cje/social.grain.gallery.item/3mwvtii4k5fdt","cid":"bafyreidntrs57bgw3pohq3kpzmbwiwo27oselznzwn3s23ikkyu3fd2fgu","$type":"com.atproto.repo.strongRef"},{"uri":"at://did:plc:7rkdalcyovirwkpzgzk74cje/social.grain.photo/3mwvtii4k5cdt","cid":"bafyreifhewrl2a2ukypzxs5hvau5kmxdopbfj474xxwcevhmeq6uzrzs4q","$type":"com.atproto.repo.strongRef"}],"associatedProfiles":[{"did":"did:plc:7rkdalcyovirwkpzgzk74cje","handle":"highway62.bsky.social","displayName":"Here, the ghost is me.","avatar":"https://cdn.bsky.app/img/avatar/plain/did:plc:7rkdalcyovirwkpzgzk74cje/bafkreifss64eqpqv5dz4masfpdi74omfhflkuxtqsvoozyi3am6uoiw5sq","associated":{"chat":{"allowIncoming":"following"},"activitySubscription":{"allowSubscriptions":"followers"}},"labels":[{"src":"did:plc:7rkdalcyovirwkpzgzk74cje","uri":"at://did:plc:7rkdalcyovirwkpzgzk74cje/app.bsky.actor.profile/self","cid":"bafyreiagrlkgpbzro7lt4nq7auw5zapxndhg3jiba5evthp5hn6m77mxsy","val":"!no-unauthenticated","cts":"1970-01-01T00:00:00.000Z"}],"createdAt":"2023-06-24T14:27:12.998Z"}],"labels":[],"uri":"at://did:plc:7rkdalcyovirwkpzgzk74cje/social.grain.gallery/3mwvtii4k5ddt","title":"Chandelier 2","description":"Taken along Ventura Boulevard, 2026","createdAt":"2026-10-02T16:52:47.420Z","items":[{"thumbnail":"https://cdn.bsky.app/img/feed_thumbnail/plain/did:plc:7rkdalcyovirwkpzgzk74cje/bafkreiazp2jgzn36qiirzvgnahy3rans2hvxgfnj7jkym7jn44jnu6gmtm","fullsize":"https://cdn.bsky.app/img/feed_fullsize/plain/did:plc:7rkdalcyovirwkpzgzk74cje/bafkreiazp2jgzn36qiirzvgnahy3rans2hvxgfnj7jkym7jn44jnu6gmtm","alt":"Lit chandeliers in a dark shop window, high contrast black and white. An unlit OPEN sign hangs in front of one of them","aspectRatio":{"width":1800,"height":1013},"$type":"app.bsky.embed.external#viewGalleryImage"}],"likeCount":2,"likers":[{"did":"did:plc:svd2pdxvyzycrcl7bk45nxjo","handle":"nerkho.ch","displayName":"Yannis","avatar":"https://cdn.bsky.app/img/avatar/plain/did:plc:svd2pdxvyzycrcl7bk45nxjo/bafkreibal27hfdrw3zt3fes7fm4lt4b3wbw7mac565bg6bcw4a7gfxczuu","associated":{"chat":{"allowIncoming":"following","allowGroupInvites":"following"},"activitySubscription":{"allowSubscriptions":"followers"}},"labels":[],"createdAt":"2024-09-17T21:52:27.911Z"},{"did":"did:plc:zz66uzoedqjukljsyfafzbro","handle":"davba.eurosky.social","displayName":"DavBa","avatar":"https://cdn.bsky.app/img/avatar/plain/did:plc:zz66uzoedqjukljsyfafzbro/bafkreifqwnj3mdzjx4we7rhee2e27nubwy6ipmfdx6treeohhapw7ho76q","associated":{"chat":{"allowIncoming":"none","allowGroupInvites":"none"},"activitySubscription":{"allowSubscriptions":"followers"}},"labels":[],"createdAt":"2023-10-03T11:14:20.311Z"}],"$type":"app.bsky.embed.external#viewGallery"}},{"featured":false,"view":{"associatedRefs":[{"uri":"at://did:plc:3jpt2mvvsumj2r7eqk4gzzjz/social.grain.gallery/3mx3sn672gljc","cid":"bafyreiaylfdpqufspz2w56e7mtj6zhyv5edczyavqxcbhz4nctapr4lmzi","$type":"com.atproto.repo.strongRef"},{"uri":"at://did:plc:3jpt2mvvsumj2r7eqk4gzzjz/social.grain.gallery.item/3mx3sn673ftjc","cid":"bafyreigvujtjgq3vrh3oqojdfg7ngacijpqctibvrh4dcarrwz5cffzu2u","$type":"com.atproto.repo.strongRef"},{"uri":"at://did:plc:3jpt2mvvsumj2r7eqk4gzzjz/social.grain.photo/3mx3sn672gkjc","cid":"bafyreies7kzybyc57sps3gn2b27jun427uhl56e2nnwtp5yu6xmcptvrxm","$type":"com.atproto.repo.strongRef"}],"associatedProfiles":[{"did":"did:plc:3jpt2mvvsumj2r7eqk4gzzjz","handle":"esb.lol","displayName":"Eric","avatar":"https://cdn.bsky.app/img/avatar/plain/did:plc:3jpt2mvvsumj2r7eqk4gzzjz/bafkreicmpkhaggfoagqj53jmzfmremae2qcw7oc3ugh5qvq6quncxp6wqq","associated":{"chat":{"allowIncoming":"following","allowGroupInvites":"following"},"activitySubscription":{"allowSubscriptions":"followers"}},"labels":[],"createdAt":"2023-05-02T03:39:27.863Z","verification":{"verifications":[{"issuer":"did:plc:z72i7hdynmk6r22z27h6tvur","issuerDisplayName":"Bluesky","issuerHandle":"bsky.app","uri":"at://did:plc:z72i7hdynmk6r22z27h6tvur/app.bsky.graph.verification/3ltmvuidftv2f","isValid":true,"createdAt":"2025-07-10T17:59:36.528Z"}],"verifiedStatus":"valid","trustedVerifierStatus":"none"}}],"labels":[],"uri":"at://did:plc:3jpt2mvvsumj2r7eqk4gzzjz/social.grain.gallery/3mx3sn672gljc","title":"Cookin' with Alf","description":"","createdAt":"2026-10-05T01:53:28.321Z","items":[{"thumbnail":"https://cdn.bsky.app/img/feed_thumbnail/plain/did:plc:3jpt2mvvsumj2r7eqk4gzzjz/bafkreifp2unqdtnmvvbuzvbkr7sissvsroltfcjeiqt4rqepbbrhr7tsva","fullsize":"https://cdn.bsky.app/img/feed_fullsize/plain/did:plc:3jpt2mvvsumj2r7eqk4gzzjz/bafkreifp2unqdtnmvvbuzvbkr7sissvsroltfcjeiqt4rqepbbrhr7tsva","alt":"Plush Alf puppet with apron, text on apron says \"Cookin' with Alf\"","aspectRatio":{"width":1500,"height":2000},"$type":"app.bsky.embed.external#viewGalleryImage"}],"likeCount":2,"likers":[{"did":"did:plc:bcgltzqazw5tb6k2g3ttenbj","handle":"chadtmiller.com","displayName":"Chad","avatar":"https://cdn.bsky.app/img/avatar/plain/did:plc:bcgltzqazw5tb6k2g3ttenbj/bafkreig5bcb7dl7aphw6eg4e7kjkvpdshfexdvk7jldg53ypre6ecdhesu","associated":{"chat":{"allowIncoming":"following","allowGroupInvites":"following"},"activitySubscription":{"allowSubscriptions":"followers"},"germ":{"showButtonTo":"usersIFollow","messageMeUrl":"https://landing.ger.mx/newUser"}},"labels":[],"createdAt":"2024-11-15T06:28:50.747Z"},{"did":"did:plc:qy7ubjcghnkisghhmzlc6f7h","handle":"oscarcalixto.com","displayName":"Oscar Calixto","avatar":"https://cdn.bsky.app/img/avatar/plain/did:plc:qy7ubjcghnkisghhmzlc6f7h/bafkreif6vopyxsonydyi536cdat4ijuuo6dw3shhqwgxwvrfediqcc35ki","associated":{"chat":{"allowIncoming":"following"},"activitySubscription":{"allowSubscriptions":"followers"}},"labels":[],"createdAt":"2024-08-31T17:04:27.103Z"}],"$type":"app.bsky.embed.external#viewGallery"}}],"livestreams":[],"apps":[{"id":"d7ea9a5058e7","title":"Leaflet","description":"Delightful publishing to connect with your community","logo":"https://app-cms-production.bsky.workers.dev/images/image-f3d78d0ed8e296e01d0fde7f6166ce6373c6c1b0-88x88-png/small","backgroundImage":"https://app-cms-production.bsky.workers.dev/images/image-abdae35a8f9dab80eff8051453c8e35de07b6ae6-418x418-png/large","overlayColor":"#57822b","textColor":"#FFF","url":"https://leaflet.pub","category":"writing"}]}
            """;
        Assert.NotNull(JsonSerializer.Deserialize<AtmosphereExploreTab>(json, BlueskyServer.BlueskyJsonSerializerOptions));
        using TestServer server = TestServerBuilder.CreateServer(TestServerBuilder.DefaultUri, async context =>
        {
            Assert.Equal("/xrpc/app.bsky.unspecced.getAtmosphereExploreTab", context.Request.Path);
            Assert.Equal(authenticated ? "pds.example.com" : TestServerBuilder.DefaultUri.Host, context.Request.Host.Host);
            Assert.Equal(authenticated, context.Request.Headers.ContainsKey("Authorization"));
            Assert.Equal(s_expectedLanguages, context.Request.Query["langs"].ToArray());
            Assert.Equal("US&x=y", context.Request.Query["countryCode"]);
            Assert.Equal("CA/West", context.Request.Query["regionCode"]);
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(json, TestContext.Current.CancellationToken);
        });
        using BlueskyAgent agent = new(new TestHttpClientFactory(server), new BlueskyAgentOptions
        {
            PublicAppViewUri = TestServerBuilder.DefaultUri
        })
        {
            Service = new Uri("https://pds.example.com")
        };
        if (authenticated)
        {
            Did did = "did:plc:hfgp6pj3akhqxntgqwramlbg";
            agent.Credentials = new AccessCredentials(
                agent.Service,
                AuthenticationType.UsernamePassword,
                JwtBuilder.CreateJwt(did, agent.Service.ToString()),
                "refreshToken");
        }

#pragma warning disable BSKYUnspecced
        AtProtoHttpResult<AtmosphereExploreTab> result = await agent.GetAtmosphereExploreTab(
            ["en", "fr&x=y"], "US&x=y", "CA/West",
            cancellationToken: TestContext.Current.CancellationToken);
#pragma warning restore BSKYUnspecced

        Assert.True(result.Succeeded);
        Assert.Equal(2, result.Result.Articles.Count);
        Assert.Equal(2, result.Result.Publications.Count);
        Assert.Equal(2, result.Result.Photos.Count);
        Assert.Empty(result.Result.Livestreams);
        Assert.Single(result.Result.Apps);
        Assert.NotNull(result.Result.Articles[0].View);
    }

    [Theory]
    [InlineData("articles")]
    [InlineData("publications")]
    [InlineData("photos")]
    [InlineData("livestreams")]
    [InlineData("apps")]
    public void ExploreRejectsNullCollectionElements(string property)
    {
        string json = """{"articles":[],"publications":[],"photos":[],"livestreams":[],"apps":[]}"""
            .Replace($"\"{property}\":[]", $"\"{property}\":[null]", StringComparison.Ordinal);

        Assert.Throws<JsonException>(() =>
            JsonSerializer.Deserialize<AtmosphereExploreTab>(json, BlueskyServer.BlueskyJsonSerializerOptions));
    }

    [Theory]
    [InlineData("articles")]
    [InlineData("publications")]
    [InlineData("photos")]
    [InlineData("livestreams")]
    [InlineData("apps")]
    public void ExploreRequiresPresentNonNullCollections(string property)
    {
        const string ValidJson = """{"articles":[],"publications":[],"photos":[],"livestreams":[],"apps":[]}""";
        string missing = ValidJson.Replace($"\"{property}\":[]", "\"ignored\":[]", StringComparison.Ordinal);
        string explicitNull = ValidJson.Replace($"\"{property}\":[]", $"\"{property}\":null", StringComparison.Ordinal);

        Assert.Throws<JsonException>(() =>
            JsonSerializer.Deserialize<AtmosphereExploreTab>(missing, BlueskyServer.BlueskyJsonSerializerOptions));
        Assert.Throws<JsonException>(() =>
            JsonSerializer.Deserialize<AtmosphereExploreTab>(explicitNull, BlueskyServer.BlueskyJsonSerializerOptions));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExploreDistinguishesAbsentAndEmptyQueryValues(bool empty)
    {
        using TestServer server = TestServerBuilder.CreateServer(TestServerBuilder.DefaultUri, async context =>
        {
            Assert.Equal(empty, context.Request.Query.ContainsKey("langs"));
            Assert.Equal(empty, context.Request.Query.ContainsKey("countryCode"));
            Assert.Equal(empty, context.Request.Query.ContainsKey("regionCode"));
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(
                """{"articles":[],"publications":[],"photos":[],"livestreams":[],"apps":[]}""",
                TestContext.Current.CancellationToken);
        });
        using HttpClient client = server.CreateClient();
#pragma warning disable BSKYUnspecced
        AtProtoHttpResult<AtmosphereExploreTab> result = await BlueskyServer.GetAtmosphereExploreTab(
            empty ? [""] : null, empty ? "" : null, empty ? "" : null,
            TestServerBuilder.DefaultUri, null, client,
            cancellationToken: TestContext.Current.CancellationToken);
#pragma warning restore BSKYUnspecced
        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task ExploreRejectsNullLanguageEntriesThroughBothApiLayers()
    {
        using HttpClient client = new();
        using BlueskyAgent agent = new();
#pragma warning disable BSKYUnspecced
        await Assert.ThrowsAsync<ArgumentNullException>(() => BlueskyServer.GetAtmosphereExploreTab(
            [null!], null, null, TestServerBuilder.DefaultUri, null, client,
            cancellationToken: TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ArgumentNullException>(() => agent.GetAtmosphereExploreTab(
            [null!], cancellationToken: TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ArgumentNullException>(() => BlueskyServer.GetAtmosphereExploreTab(
            null, null, null, null!, null, client,
            cancellationToken: TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ArgumentNullException>(() => BlueskyServer.GetAtmosphereExploreTab(
            null, null, null, TestServerBuilder.DefaultUri, null, null!,
            cancellationToken: TestContext.Current.CancellationToken));
#pragma warning restore BSKYUnspecced
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task ExploreRejectsNonpositiveResponseSize(int maximumResponseSize)
    {
        using HttpClient client = new();
#pragma warning disable BSKYUnspecced
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => BlueskyServer.GetAtmosphereExploreTab(
            null, null, null, TestServerBuilder.DefaultUri, null, client,
            maximumResponseSize: maximumResponseSize, cancellationToken: TestContext.Current.CancellationToken));
#pragma warning restore BSKYUnspecced
    }

    [Fact]
    public async Task ExploreForwardsCredentialUpdatesThroughTheAgentAndServer()
    {
        using TestServer server = TestServerBuilder.CreateServer(TestServerBuilder.DefaultUri, async context =>
        {
            context.Response.Headers.Append("DPoP-Nonce", "updatedNonce");
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(
                """{"articles":[],"publications":[],"photos":[],"livestreams":[],"apps":[]}""",
                TestContext.Current.CancellationToken);
        });
        using BlueskyAgent agent = new(new TestHttpClientFactory(server), new BlueskyAgentOptions
        {
            PublicAppViewUri = TestServerBuilder.DefaultUri
        })
        {
            Service = TestServerBuilder.DefaultUri,
            Credentials = (DPoPAccessCredentials)AtProtoCredential.Create(
                TestServerBuilder.DefaultUri,
                authenticationType: AuthenticationType.OAuth,
                accessJwt: JwtBuilder.CreateJwt(new Did("did:plc:identifier")),
                refreshToken: "refreshToken",
                dPoPProofKey: JsonWebKeys.CreateRsaJson(),
                dPoPNonce: "originalNonce")
        };
        string? persistedNonce = null;
        agent.CredentialsUpdatedAsync = (args, _) =>
        {
            persistedNonce = Assert.IsType<DPoPAccessCredentials>(args.AccessCredentials).DPoPNonce;
            return Task.CompletedTask;
        };

#pragma warning disable BSKYUnspecced
        AtProtoHttpResult<AtmosphereExploreTab> result = await agent.GetAtmosphereExploreTab(
            cancellationToken: TestContext.Current.CancellationToken);
#pragma warning restore BSKYUnspecced

        Assert.True(result.Succeeded);
        Assert.Equal("updatedNonce", persistedNonce);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExploreUsesTheConfiguredLoggerFactory(bool useAgent)
    {
        using ILoggerFactory loggerFactory = LoggerFactory.Create(_ => { });
        using BlueskyAgent agent = new(new BlueskyAgentOptions { LoggerFactory = loggerFactory });
        using HttpClient client = new();
        loggerFactory.Dispose();

#pragma warning disable BSKYUnspecced
        await Assert.ThrowsAsync<ObjectDisposedException>(() => useAgent
            ? agent.GetAtmosphereExploreTab(cancellationToken: TestContext.Current.CancellationToken)
            : BlueskyServer.GetAtmosphereExploreTab(
                null, null, null, TestServerBuilder.DefaultUri, null, client,
                loggerFactory: loggerFactory, cancellationToken: TestContext.Current.CancellationToken));
#pragma warning restore BSKYUnspecced
    }

    [Fact]
    public async Task ExploreUsesSourceGeneratedMetadataForLivestreamResponses()
    {
        Type[] types = [
            typeof(AtmosphereExploreTab), typeof(AtmosphereArticleItem), typeof(AtmospherePublicationItem),
            typeof(AtmosphereGalleryItem), typeof(AtmosphereLivestreamItem), typeof(AtmosphereAppCard),
            typeof(AtmosphereAnnouncementBanner), typeof(ArticleView), typeof(ArticlePublicationView),
            typeof(ArticlePublicationTheme), typeof(GalleryView), typeof(GalleryImageView), typeof(LivestreamView)];
        foreach (Type type in types)
        {
            Assert.NotNull(BlueskyServer.BlueskyJsonSerializerOptions.GetTypeInfo(type));
        }

        using TestServer server = TestServerBuilder.CreateServer(TestServerBuilder.DefaultUri, async context =>
        {
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(
                """
                {"articles":[],"publications":[],"photos":[],"apps":[],
                 "livestreams":[{"featured":false,"view":{"$type":"app.bsky.embed.external#viewLivestream",
                  "uri":"https://example.com/live","title":"","description":"","active":true}}]}
                """, TestContext.Current.CancellationToken);
        });
        using HttpClient client = server.CreateClient();

#pragma warning disable BSKYUnspecced
        AtProtoHttpResult<AtmosphereExploreTab> result = await BlueskyServer.GetAtmosphereExploreTab(
            null, null, null, TestServerBuilder.DefaultUri, null, client,
            cancellationToken: TestContext.Current.CancellationToken);
#pragma warning restore BSKYUnspecced

        Assert.True(result.Succeeded);
        Assert.True(Assert.Single(result.Result.Livestreams).View.Active);
    }

    [Theory]
    [InlineData("articles")]
    [InlineData("publications")]
    [InlineData("photos")]
    [InlineData("livestreams")]
    public async Task ExploreRejectsNullHydratedViews(string property)
    {
        string json = """{"articles":[],"publications":[],"photos":[],"livestreams":[],"apps":[]}"""
            .Replace($"\"{property}\":[]", $"\"{property}\":[{{\"featured\":false,\"view\":null}}]", StringComparison.Ordinal);
        using TestServer server = TestServerBuilder.CreateServer(TestServerBuilder.DefaultUri, async context =>
        {
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(json, TestContext.Current.CancellationToken);
        });
        using HttpClient client = server.CreateClient();

#pragma warning disable BSKYUnspecced
        AtProtoHttpResult<AtmosphereExploreTab> result = await BlueskyServer.GetAtmosphereExploreTab(
            null, null, null, TestServerBuilder.DefaultUri, null, client,
            cancellationToken: TestContext.Current.CancellationToken);
#pragma warning restore BSKYUnspecced
        Assert.False(result.Succeeded);
        Assert.Equal("InvalidResponse", result.AtErrorDetail?.Error);
    }

    [Fact]
    public async Task ExplorePreservesHttpErrors()
    {
        using TestServer server = TestServerBuilder.CreateServer(TestServerBuilder.DefaultUri, async context =>
        {
            context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync("""{"error":"InvalidRequest","message":"Invalid region"}""", TestContext.Current.CancellationToken);
        });
        using HttpClient client = server.CreateClient();

#pragma warning disable BSKYUnspecced
        AtProtoHttpResult<AtmosphereExploreTab> result = await BlueskyServer.GetAtmosphereExploreTab(
            null, null, null, TestServerBuilder.DefaultUri, null, client,
            cancellationToken: TestContext.Current.CancellationToken);
#pragma warning restore BSKYUnspecced

        Assert.False(result.Succeeded);
        Assert.Equal("InvalidRequest", result.AtErrorDetail?.Error);
    }
}
