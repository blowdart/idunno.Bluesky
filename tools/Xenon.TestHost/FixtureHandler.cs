// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Net;
using System.Text;
using System.Text.Json.Nodes;

using idunno.AtProto;
using idunno.Bluesky;

using Microsoft.AspNetCore.WebUtilities;

namespace Xenon.TestHost;

/// <summary>Resolves only reserved fictional identities locally; real identities use the hardened network client.</summary>
internal sealed class FixtureHandler(string publicKey, HttpClient network, TimeProvider? clock = null) : HttpMessageHandler
{
    private static readonly string[] s_teamNames = ["administrator", "moderator", "viewer", "suspended", "removed", "local"];
    internal static readonly Did s_issuer = new("did:web:labeler.xenon.invalid");
    internal static readonly Did s_reporter = new("did:web:reporter.xenon.invalid");
    internal const int SubjectCount = 40;
    internal const string Cid = "bafyreihdwdcefgh4dqkjv67uzcmw7ojee6xedzdetojuzjevtenxquvyku";
    internal const string LegacyDeclaration = """
        {"$type":"app.bsky.labeler.service","createdAt":"2026-01-01T00:00:00Z",
         "policies":{"labelValues":["spam","scam"],"labelValueDefinitions":[]},"subjectTypes":["account","record"]}
        """;
    internal static readonly string s_declaration = LegacyDeclaration.TrimEnd()[..^1] + """
        ,"reasonTypes":["tools.ozone.report.defs#reasonMisleadingSpam","tools.ozone.report.defs#reasonOther",
        "tools.ozone.report.defs#reasonAppeal","com.atproto.moderation.defs#reasonSpam",
        "com.atproto.moderation.defs#reasonOther","com.atproto.moderation.defs#reasonAppeal"]}
        """;

    /// <summary>Creates one of the bounded fixture account identities.</summary>
    internal static Did Subject(int index) => new($"did:web:subject-{index:D2}.xenon.invalid");

    /// <summary>Creates a fictional team identity separate from moderation subjects.</summary>
    internal static Did TeamMember(string name) => new($"did:web:team-{name}.xenon.invalid");

    /// <summary>Exercises verified, invalid, unknown and absent verification state for both profile and post previews.</summary>
    private static string VerificationJson(string handle) => handle switch
    {
        "subject-01.xenon.invalid" or "subject-02.xenon.invalid" =>
            """{"verifications":[],"verifiedStatus":"valid","trustedVerifierStatus":"none"}""",
        "subject-03.xenon.invalid" or "subject-04.xenon.invalid" =>
            """{"verifications":[],"verifiedStatus":"invalid","trustedVerifierStatus":"none"}""",
        "subject-05.xenon.invalid" or "subject-06.xenon.invalid" =>
            """{"verifications":[],"verifiedStatus":"future-status","trustedVerifierStatus":"none"}""",
        _ => "null"
    };

    /// <inheritdoc/>
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Uri uri = request.RequestUri!;
        if (request.Method != HttpMethod.Get || uri.Scheme != Uri.UriSchemeHttps)
        {
            throw new HttpRequestException("The test fixture client permits only HTTPS reads; it never publishes to a PDS or PLC.");
        }

        string host = uri.Host;
        if (host == "mod.bsky.app" && uri.AbsolutePath == "/xrpc/com.atproto.label.queryLabels")
        {
            if (request.Headers.Authorization is not null || request.Headers.Contains("DPoP") || request.Headers.Contains("Cookie"))
            {
                throw new HttpRequestException("Fictional moderation previews must not carry moderator credentials.");
            }
            return ModerationLabels(uri);
        }
        if (host == "public.api.bsky.app")
        {
            if (uri.AbsolutePath == "/xrpc/app.bsky.labeler.getServices")
            {
                return Json(HttpStatusCode.OK, $$$"""
                    {"views":[{"$type":"app.bsky.labeler.defs#labelerViewDetailed",
                     "uri":"at://{{{WellKnownDistributedIdentifiers.BlueskyModerationLabeler}}}/app.bsky.labeler.service/self",
                     "cid":"{{{Cid}}}","creator":{"did":"{{{WellKnownDistributedIdentifiers.BlueskyModerationLabeler}}}","handle":"moderation.bsky.app"},
                     "indexedAt":"2026-01-01T00:00:00Z","policies":{"labelValues":["spam","impersonation","fixture-media","fixture-adult"],
                     "labelValueDefinitions":[
                     {"identifier":"spam","severity":"inform","blurs":"content","adultOnly":false},
                     {"identifier":"impersonation","severity":"inform","blurs":"none","adultOnly":false},
                     {"identifier":"fixture-media","severity":"alert","blurs":"media","adultOnly":false},
                     {"identifier":"fixture-adult","severity":"alert","blurs":"content","adultOnly":true}]}}]}
                    """);
            }
            Dictionary<string, Microsoft.Extensions.Primitives.StringValues> query = QueryHelpers.ParseQuery(uri.Query);
            query.TryGetValue("actor", out Microsoft.Extensions.Primitives.StringValues actor);
            Did? profile = Enumerable.Range(1, SubjectCount).Select(Subject)
                .FirstOrDefault(candidate => candidate.Value == actor.ToString());
            if (uri.AbsolutePath == "/xrpc/app.bsky.actor.getProfile" && profile is not null)
            {
                if (profile == Subject(8) || profile == Subject(9))
                {
                    return Json(HttpStatusCode.Forbidden, """{"error":"BlockedActor"}""");
                }
                string handle = profile.Value["did:web:".Length..];
                string verification = VerificationJson(handle);
                return Json(HttpStatusCode.OK, $$"""
                    {"did":"{{profile.Value}}","handle":"{{handle}}","displayName":"Fictional Xenon profile",
                     "description":"A fictional profile for local moderation testing. <script>not executable</script>",
                     "avatar":"https://cdn.bsky.app/img/avatar/plain/{{profile.Value}}/example.png",
                     "followersCount":20,"followsCount":5,"postsCount":13,"verification":{{verification}}}
                    """);
            }

        }

        if (host == "cdn.bsky.app" && uri.AbsolutePath.Contains(".xenon.invalid/example.png", StringComparison.Ordinal))
        {
            HttpResponseMessage image = new(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jRZkAAAAASUVORK5CYII="))
            };
            image.Content.Headers.ContentType = new("image/png");
            return image;
        }

        if (host == "pds.xenon.invalid")
        {
            if (request.Headers.Authorization is not null || request.Headers.Contains("DPoP") || request.Headers.Contains("Cookie"))
            {
                throw new HttpRequestException("Fictional repository previews must not carry moderator credentials.");
            }
            if (uri.AbsolutePath == "/xrpc/com.atproto.sync.getBlob")
            {
                Dictionary<string, Microsoft.Extensions.Primitives.StringValues> blobQuery = QueryHelpers.ParseQuery(uri.Query);
                bool video = blobQuery["cid"] == VideoCid;
                HttpResponseMessage blob = new(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent(video
                        ? [0, 0, 0, 20, 102, 116, 121, 112, 109, 112, 52, 50, 0, 0, 0, 0, 109, 112, 52, 50]
                        : Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jRZkAAAAASUVORK5CYII="))
                };
                blob.Content.Headers.ContentType = new(video ? "video/mp4" : "image/png");
                return blob;
            }
            if (uri.AbsolutePath != "/xrpc/com.atproto.repo.getRecord")
            {
                return Json(HttpStatusCode.NotFound, """{"error":"RecordNotFound"}""");
            }

            Dictionary<string, Microsoft.Extensions.Primitives.StringValues> query = QueryHelpers.ParseQuery(uri.Query);
            string repo = query["repo"].ToString();
            string collection = query["collection"].ToString();
            string rkey = query["rkey"].ToString();
            bool declaration = repo == s_issuer.Value && collection == "app.bsky.labeler.service" && rkey == "self";
            bool post = Enumerable.Range(1, SubjectCount).Any(index => repo == Subject(index).Value) &&
                collection == "app.bsky.feed.post" && rkey == "example";
            bool profile = Enumerable.Range(1, SubjectCount).Any(index => repo == Subject(index).Value) &&
                collection == "app.bsky.actor.profile" && rkey == "self";
            bool namedRecord = Enumerable.Range(1, SubjectCount).Any(index => repo == Subject(index).Value) &&
                collection is "app.bsky.graph.list" or "app.bsky.graph.starterpack" && rkey == "example";
            if (!declaration && !post && !profile && !namedRecord || query.TryGetValue("cid", out Microsoft.Extensions.Primitives.StringValues cid) && cid != Cid)
            {
                return Json(HttpStatusCode.NotFound, """{"error":"RecordNotFound"}""");
            }

            string record = s_declaration;
            if (profile)
            {
                record = $$$"""
                    {"$type":"app.bsky.actor.profile","displayName":"Fictional Xenon profile",
                     "labels":{{{SelfLabels(repo)}}},
                     "description":"A fictional profile for local moderation testing. <script>not executable</script>",
                     "avatar":{"$type":"blob","ref":{"$link":"{{{Cid}}}"},"mimeType":"image/png","size":68},
                     "banner":{"$type":"blob","ref":{"$link":"{{{Cid}}}"},"mimeType":"image/png","size":68}}
                    """;
            }
            else if (namedRecord)
            {
                record = $$"""
                    {"$type":"{{collection}}","name":"Fictional Xenon {{(collection == "app.bsky.graph.list" ? "list" : "starter pack")}}",
                     "description":"A fictional collection for local moderation testing. <script>not executable</script>"}
                    """;
            }
            else if (post)
            {
                string embed = repo == Subject(12).Value
                    ? $$$"""{"$type":"app.bsky.embed.video","alt":"Fictional video","aspectRatio":{"width":9,"height":16},"video":{"$type":"blob","ref":{"$link":"{{{VideoCid}}}"},"mimeType":"video/mp4","size":20}}"""
                    : $$$"""{"$type":"app.bsky.embed.images","images":[{"alt":"Fictional preview image","image":{"$type":"blob","ref":{"$link":"{{{Cid}}}"},"mimeType":"image/png","size":68}}]}""";
                if (repo == Subject(14).Value)
                {
                    embed = $$$"""
                        {"$type":"app.bsky.embed.gallery","items":[
                         {"$type":"app.bsky.embed.gallery#image","alt":"Fictional gallery image one","image":{"$type":"blob","ref":{"$link":"{{{Cid}}}"},"mimeType":"image/png","size":68}},
                         {"$type":"app.bsky.embed.gallery#image","alt":"Fictional gallery image two","image":{"$type":"blob","ref":{"$link":"{{{Cid}}}"},"mimeType":"image/png","size":68}}]}
                        """;
                }
                record = $$"""
                    {"$type":"app.bsky.feed.post","text":"Fictional Xenon test record. <script>not executable</script>",
                     "labels":{{SelfLabels(repo)}},
                     "createdAt":"2026-01-01T00:00:00Z","embed":{{embed}}}
                    """;
            }
            return Json(HttpStatusCode.OK, $$"""
                {"uri":"at://{{repo}}/{{collection}}/{{rkey}}","cid":"{{Cid}}","value":{{record}}}
                """);
        }

        Did? did = new[] { s_issuer, s_reporter }.Concat(Enumerable.Range(1, SubjectCount).Select(Subject))
            .Concat(s_teamNames.Select(TeamMember))
            .FirstOrDefault(candidate => candidate.Value == "did:web:" + host);
        if (did is not null && uri.AbsolutePath == "/.well-known/atproto-did")
        {
            return new(HttpStatusCode.OK) { Content = new StringContent(did.Value, Encoding.UTF8, "text/plain") };
        }

        if (did is not null && uri.AbsolutePath == "/.well-known/did.json")
        {
            return Json(HttpStatusCode.OK, $$"""
                {"id":"{{did.Value}}","alsoKnownAs":["at://{{host}}"],"verificationMethod":[
                 {"id":"{{did.Value}}#atproto","type":"Multikey","controller":"{{did.Value}}","publicKeyMultibase":"{{publicKey}}"}],
                 "service":[{"id":"#atproto_pds","type":"AtprotoPersonalDataServer","serviceEndpoint":"https://pds.xenon.invalid"}]}
                """);
        }

        if (host.EndsWith(".xenon.invalid", StringComparison.OrdinalIgnoreCase))
        {
            throw new HttpRequestException("The requested fictional identity or record is not in the test fixtures.");
        }

        using HttpRequestMessage lookup = new(HttpMethod.Get, uri);
        return await network.SendAsync(lookup, cancellationToken);
    }

    /// <summary>Creates harmless fictional self-labels for testing default image protection.</summary>
    private static string SelfLabels(string repo)
    {
        string? value = repo switch
        {
            "did:web:subject-21.xenon.invalid" => "porn",
            "did:web:subject-22.xenon.invalid" => "sexual",
            "did:web:subject-23.xenon.invalid" => "nudity",
            "did:web:subject-24.xenon.invalid" => "graphic-media",
            "did:web:subject-36.xenon.invalid" => "porn",
            _ => null
        };
        return value is null ? "null" : $$"""{"$type":"com.atproto.label.defs#selfLabels","values":[{"val":"{{value}}"}]}""";
    }

    /// <summary>Returns current, unrelated, superseded, expired and failed fictional label reads without contacting a moderation service.</summary>
    private HttpResponseMessage ModerationLabels(Uri uri)
    {
        Dictionary<string, Microsoft.Extensions.Primitives.StringValues> query = QueryHelpers.ParseQuery(uri.Query);
        string did = query["uriPatterns"].FirstOrDefault() ?? "";
        string record = query["uriPatterns"].LastOrDefault() ?? "";
        if (did == Subject(8).Value || did == Subject(9).Value || did == Subject(35).Value || did == Subject(36).Value)
        {
            return Json(HttpStatusCode.ServiceUnavailable, """{"error":"FixtureUnavailable"}""");
        }
        JsonArray labels = [];
        string? value = did switch
        {
            "did:web:subject-17.xenon.invalid" => "porn",
            "did:web:subject-18.xenon.invalid" => "nudity",
            "did:web:subject-19.xenon.invalid" => "graphic-media",
            "did:web:subject-20.xenon.invalid" => "porn",
            "did:web:subject-25.xenon.invalid" => "porn",
            "did:web:subject-26.xenon.invalid" => "sexual",
            "did:web:subject-27.xenon.invalid" => "graphic-media",
            "did:web:subject-28.xenon.invalid" => "nudity",
            "did:web:subject-29.xenon.invalid" => "porn",
            "did:web:subject-30.xenon.invalid" => "spam",
            "did:web:subject-31.xenon.invalid" => "!warn",
            "did:web:subject-32.xenon.invalid" => "fixture-media",
            "did:web:subject-33.xenon.invalid" => "fixture-adult",
            "did:web:subject-34.xenon.invalid" => "graphic-media",
            "did:web:subject-37.xenon.invalid" => "porn",
            "did:web:subject-38.xenon.invalid" => "sexual",
            "did:web:subject-39.xenon.invalid" => "porn",
            "did:web:subject-40.xenon.invalid" => "impersonation",
            _ => null
        };
        if (value is not null)
        {
            JsonObject label = new()
            {
                ["src"] = WellKnownDistributedIdentifiers.BlueskyModerationLabeler.Value,
                ["uri"] = record,
                ["val"] = value,
                ["cts"] = "2026-01-01T00:00:00Z"
            };
            if (did == Subject(26).Value)
            {
                label["src"] = s_issuer.Value;
            }
            if (did == Subject(27).Value)
            {
                label["cid"] = VideoCid;
            }
            if (did == Subject(29).Value)
            {
                label["exp"] = (clock ?? TimeProvider.System).GetUtcNow().AddDays(-1).ToString("O");
            }
            if (did == Subject(20).Value)
            {
                label["exp"] = (clock ?? TimeProvider.System).GetUtcNow().ToString("O");
            }
            if (did == Subject(37).Value)
            {
                label["uri"] = $"at://{did}/app.bsky.feed.post/unrelated";
            }
            if (did == Subject(38).Value)
            {
                label["uri"] = did;
            }
            if (did == Subject(39).Value)
            {
                label["cid"] = Cid;
            }
            labels.Add(label);
            if (did == Subject(28).Value)
            {
                JsonObject negation = (JsonObject)label.DeepClone();
                negation["cts"] = "2026-01-01T00:00:01Z";
                negation["neg"] = true;
                labels.Insert(0, negation);
            }
            if (did == Subject(34).Value && !query.ContainsKey("cursor"))
            {
                labels.Clear();
                return Json(HttpStatusCode.OK, """{"labels":[],"cursor":"fixture-page-2"}""");
            }
            if (did == Subject(17).Value)
            {
                return Json(HttpStatusCode.OK, """{"labels":[{"src":""");
            }
            if (did == Subject(18).Value && query.ContainsKey("cursor"))
            {
                throw new HttpRequestException("Fictional later-page failure.");
            }
            if (did == Subject(18).Value || did == Subject(19).Value)
            {
                return Json(HttpStatusCode.OK, new JsonObject { ["labels"] = labels, ["cursor"] = "fixture-repeat" }.ToJsonString());
            }
        }
        return Json(HttpStatusCode.OK, new JsonObject { ["labels"] = labels }.ToJsonString());
    }

    /// <summary>Returns a complete fixture response without network access.</summary>
    private static HttpResponseMessage Json(HttpStatusCode status, string json) =>
        new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    internal const string VideoCid = "bafkreig2n7moy25qixvtqznarvcqmvreikww6q5epjqhxr56rbce4vxnqu";
}
