// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

using idunno.AtProto;
using idunno.AtProto.Authentication;
using idunno.AtProto.Benchmarks;
using idunno.AtProto.Labels;
using idunno.AtProto.Test;
using idunno.Bluesky.Actor;
using idunno.Bluesky.Feed;
using idunno.Bluesky.Feed.Model;

namespace idunno.Bluesky.Test;

/// <summary>
/// Replays the captured AppView responses through the XRPC client and the per post and per author computations built on
/// them, and fails if the bytes allocated per call or per item grow past a budget.
/// </summary>
/// <remarks>
/// <para>Allocations are deterministic for a given runtime, code and corpus, so they can gate a pull request. Every path
/// completes synchronously against the in memory replay, so the allocations of the current thread are exactly those of the
/// path being measured.</para>
/// <para>Budgets are roughly ten percent above the measured baseline. When a change reduces allocations, lower the budget
/// to lock in the gain. When the corpus is refreshed with benchmarks\capture.ps1, use the budgets it suggests.</para>
/// </remarks>
public class AllocationBudgetTests
{
    private static readonly Uri s_service = new("https://api.bsky.app");
    private static readonly Did s_actor = new("did:plc:z72i7hdynmk6r22z27h6tvur");
    private static readonly Did s_labeler = new("did:plc:ar7c4by46qjdydhdevvrndac");
    private static readonly DateTimeOffset s_labelled = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    // BlueskyJsonSerializerOptions.Options builds new options on every call, so resolve the metadata once.
    private static readonly JsonTypeInfo<GetTimelineResponse> s_timelineTypeInfo =
        (JsonTypeInfo<GetTimelineResponse>)BlueskyJsonSerializerOptions.Options.GetTypeInfo(typeof(GetTimelineResponse));

    private static readonly JsonTypeInfo<GetAuthorFeedResponse> s_authorFeedTypeInfo =
        (JsonTypeInfo<GetAuthorFeedResponse>)BlueskyJsonSerializerOptions.Options.GetTypeInfo(typeof(GetAuthorFeedResponse));

    // Bytes allocated per call, for a captured page of 50 feed entries. Baselines measured on .NET 9 and 10, then .NET 8,
    // are in the comments.
    public static TheoryData<string, long> XrpcBudgets => new()
    {
        { "Xrpc.GetTimeline", 2_260_000 },             // 2,018,624 / 2,048,304
        { "Xrpc.GetAuthorFeed", 3_540_000 },           // 3,167,576 / 3,212,310
        { "Xrpc.TimelineDecode", 2_230_000 },          // 2,009,672 / 2,039,176
    };

    [Theory]
    [MemberData(nameof(XrpcBudgets))]
    public async Task AllocationsPerCallAreWithinBudget(string path, long budget)
    {
        byte[] timeline = CorpusFile.Read(CorpusFile.XrpcGetTimeline)[0];
        byte[] authorFeed = CorpusFile.Read(CorpusFile.XrpcGetAuthorFeed)[0];

        // The replay handler ignores authorization, so an unsigned token which never expires is enough to authenticate the request.
        AccessCredentials credentials = new(s_service, AuthenticationType.UsernamePassword, UnsignedJwt("did:plc:aaaaaaaaaaaaaaaaaaaaaaaa"), "refresh");

        using HttpClient timelineClient = new(new ReplayHandler(timeline));
        using HttpClient authorFeedClient = new(new ReplayHandler(authorFeed));

        Func<int, Task> action = path switch
        {
            "Xrpc.GetTimeline" => async _ =>
                Assert.True((await BlueskyServer.GetTimeline(null, 50, null, s_service, credentials, timelineClient, cancellationToken: TestContext.Current.CancellationToken)).Succeeded),
            "Xrpc.GetAuthorFeed" => async _ =>
                Assert.True((await BlueskyServer.GetAuthorFeed(s_actor, 50, null, null, null, s_service, null, authorFeedClient, cancellationToken: TestContext.Current.CancellationToken)).Succeeded),
            "Xrpc.TimelineDecode" => _ =>
            {
                Assert.NotNull(JsonSerializer.Deserialize(timeline, s_timelineTypeInfo));
                return Task.CompletedTask;
            },
            _ => throw new ArgumentOutOfRangeException(nameof(path), path, "Unknown path.")
        };

        (long perCall, bool completedSynchronously) = await AllocationMeasurement.PerUnitAsync(4, action);

        AllocationMeasurement.Report(path, perCall);

        Assert.True(completedSynchronously, $"{path} did not complete synchronously, so its allocations cannot be attributed to it.");
        Assert.True(perCall <= budget, $"{path} allocated {perCall:N0} bytes per call, over its budget of {budget:N0}.");
    }

    // Bytes allocated per post or author. The Corpus scenario uses the captured timeline and author feed as the AppView
    // returned them, where most have no labels. The Labelled scenario gives every author two self labels and a labeler's
    // label, and every post a self label and a labeler's label.
    public static TheoryData<string, long> SelfLabelBudgets => new()
    {
        { "PostView.SelfLabels.Corpus", 16 },                //     0
        { "PostView.SelfLabels.Labelled", 160 },             //   144
        { "ProfileViewBasic.SelfLabels.Corpus", 24 },        //    18
        { "ProfileViewBasic.SelfLabels.Labelled", 340 },     //   304
    };

    [Theory]
    [MemberData(nameof(SelfLabelBudgets))]
    public void AllocationsPerItemAreWithinBudget(string path, long budget)
    {
        PostView[] posts =
        [
            .. JsonSerializer.Deserialize(CorpusFile.Read(CorpusFile.XrpcGetTimeline)[0], s_timelineTypeInfo)!.Feed.Select(entry => entry.Post),
            .. JsonSerializer.Deserialize(CorpusFile.Read(CorpusFile.XrpcGetAuthorFeed)[0], s_authorFeedTypeInfo)!.Feed.Select(entry => entry.Post)
        ];

        if (path.EndsWith(".Labelled", StringComparison.Ordinal))
        {
            posts = [.. posts.Select(WithLabels)];
        }

        long perItem;
        if (path.StartsWith("PostView.", StringComparison.Ordinal))
        {
            perItem = MeasureFreshCopies(posts);
        }
        else
        {
            ProfileViewBasic[] authors = [.. posts.Select(post => post.Author)];
            perItem = AllocationMeasurement.PerUnit(authors.Length, () =>
            {
                foreach (ProfileViewBasic author in authors)
                {
                    _ = author.SelfLabels;
                }
            });
        }

        AllocationMeasurement.Report(path, perItem);

        Assert.True(perItem <= budget, $"{path} allocated {perItem:N0} bytes per item, over its budget of {budget:N0}.");
    }

    // Post self labels are cached after the first call, so each run measures copies made before counting starts. The source
    // posts have never computed their self labels, so the copies have nothing cached.
    private static long MeasureFreshCopies(PostView[] posts)
    {
        long total = 0;

        // Warm up, then measure.
        for (int run = 0; run < 2; run++)
        {
            PostView[] copies = [.. posts.Select(post => post with { })];

            long start = GC.GetAllocatedBytesForCurrentThread();
            foreach (PostView post in copies)
            {
                _ = post.SelfLabels;
            }

            total = GC.GetAllocatedBytesForCurrentThread() - start;
        }

        return total / posts.Length;
    }

    private static PostView WithLabels(PostView post)
    {
        string self = $"at://{post.Author.Did}/app.bsky.actor.profile/self";

        return post with
        {
            Author = post.Author with
            {
                Labels =
                [
                    NewLabel(post.Author.Did, self, null, "!no-unauthenticated"),
                    NewLabel(post.Author.Did, self, null, SelfLabelValues.GraphicMedia),
                    NewLabel(s_labeler, $"at://{post.Author.Did}", null, "spam")
                ]
            },
            Labels =
            [
                NewLabel(post.Author.Did, post.Uri.ToString(), post.Cid, SelfLabelValues.GraphicMedia),
                NewLabel(s_labeler, post.Uri.ToString(), post.Cid, "spam")
            ]
        };
    }

    private static Label NewLabel(Did source, string uri, Cid? cid, string value) =>
        new(1, source, uri, cid, value, false, s_labelled, null);

    private static string UnsignedJwt(string subject)
    {
        static string Encode(string json) => Convert.ToBase64String(Encoding.UTF8.GetBytes(json)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

        return $"{Encode("{\"alg\":\"none\",\"typ\":\"JWT\"}")}.{Encode($"{{\"sub\":\"{subject}\",\"exp\":4102444800}}")}.";
    }

    private sealed class ReplayHandler(byte[] body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            ByteArrayContent content = new(body);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/json") { CharSet = "utf-8" };
            content.Headers.ContentLength = body.Length;

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content, RequestMessage = request });
        }
    }
}
