// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;

using idunno.AtProto.Labels;
using idunno.Bluesky;
using idunno.Bluesky.Actor;
using idunno.Bluesky.Feed;
using idunno.Bluesky.Feed.Model;

namespace idunno.AtProto.Benchmarks;

/// <summary>
/// Measures computing self labels for every post and author, which used to be LINQ queries.
/// </summary>
/// <remarks>
/// <para>The <c>Corpus</c> scenario uses the posts and authors from the captured timeline and author feed exactly as the
/// AppView returned them, where most have no labels. The <c>Labelled</c> scenario gives every author two self labels and a
/// labeler's label, and every post a self label and a labeler's label, to show the cost when there is work to do.</para>
/// <para>Profile self labels call the real <see cref="ProfileViewBasic.SelfLabels"/> property. <see cref="PostView.SelfLabels"/>
/// is cached after the first read, so post self labels make the same <c>SelfLabelReader</c> call the property makes.</para>
/// </remarks>
[MemoryDiagnoser]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class LinqBenchmarks
{
    private static readonly Did s_labeler = new("did:plc:ar7c4by46qjdydhdevvrndac");
    private static readonly DateTimeOffset s_labelled = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private ProfileViewBasic[] _authors = null!;
    private PostView[] _posts = null!;

    [Params("Corpus", "Labelled")]
    public string Scenario { get; set; } = "Corpus";

    [GlobalSetup]
    public void Setup()
    {
        JsonTypeInfo<GetTimelineResponse> timelineTypeInfo = (JsonTypeInfo<GetTimelineResponse>)BlueskyJsonSerializerOptions.Default.GetTypeInfo(typeof(GetTimelineResponse));
        JsonTypeInfo<GetAuthorFeedResponse> authorFeedTypeInfo = (JsonTypeInfo<GetAuthorFeedResponse>)BlueskyJsonSerializerOptions.Default.GetTypeInfo(typeof(GetAuthorFeedResponse));

        PostView[] posts =
        [
            .. JsonSerializer.Deserialize(CorpusFile.Read(CorpusFile.XrpcGetTimeline)[0], timelineTypeInfo)!.Feed.Select(entry => entry.Post),
            .. JsonSerializer.Deserialize(CorpusFile.Read(CorpusFile.XrpcGetAuthorFeed)[0], authorFeedTypeInfo)!.Feed.Select(entry => entry.Post)
        ];

        if (Scenario == "Labelled")
        {
            posts = [.. posts.Select(post => post with
            {
                Author = WithSelfLabels(post.Author),
                Labels =
                [
                    NewLabel(post.Author.Did, post.Uri.ToString(), post.Cid, SelfLabelValues.GraphicMedia),
                    NewLabel(s_labeler, post.Uri.ToString(), post.Cid, "spam")
                ]
            })];
        }

        _posts = posts;
        _authors = [.. posts.Select(post => post.Author)];
    }

    [Benchmark]
    [BenchmarkCategory("ProfileSelfLabels")]
    public int ProfileSelfLabels()
    {
        int count = 0;
        foreach (ProfileViewBasic author in _authors)
        {
            count += author.SelfLabels.Count;
        }

        return count;
    }

    [Benchmark]
    [BenchmarkCategory("PostSelfLabels")]
    public int PostSelfLabels()
    {
        int count = 0;
        foreach (PostView post in _posts)
        {
            count += post.Labels.Count == 0 ? 0 : SelfLabelReader.Read(post.Labels, post.Author.Did, post.Uri.ToString(), matchCid: true, post.Cid).Count;
        }

        return count;
    }

    private static ProfileViewBasic WithSelfLabels(ProfileViewBasic author)
    {
        string self = $"at://{author.Did}/app.bsky.actor.profile/self";
        return author with
        {
            Labels =
            [
                NewLabel(author.Did, self, null, "!no-unauthenticated"),
                NewLabel(author.Did, self, null, SelfLabelValues.GraphicMedia),
                NewLabel(s_labeler, $"at://{author.Did}", null, "spam")
            ]
        };
    }

    private static Label NewLabel(Did source, string uri, Cid? cid, string value) =>
        new(1, source, uri, cid, value, false, s_labelled, null);
}
