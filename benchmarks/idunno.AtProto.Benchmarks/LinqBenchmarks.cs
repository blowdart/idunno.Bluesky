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
/// Measures the LINQ queries which compute self labels for every post and author, against loop based replacements which
/// return the same results.
/// </summary>
/// <remarks>
/// <para>The <c>Corpus</c> scenario uses the posts and authors from the captured timeline and author feed exactly as the
/// AppView returned them, where most have no labels. The <c>Labelled</c> scenario gives every author two self labels and a
/// labeler's label, and every post a self label and a labeler's label, to show the cost when there is work to do.</para>
/// <para>Profile self labels call the real <see cref="ProfileViewBasic.SelfLabels"/> property. Post self labels are cached
/// after the first call, so the baseline is a copy of the query the property runs.</para>
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
        JsonTypeInfo<GetTimelineResponse> timelineTypeInfo = (JsonTypeInfo<GetTimelineResponse>)BlueskyJsonSerializerOptions.Options.GetTypeInfo(typeof(GetTimelineResponse));
        JsonTypeInfo<GetAuthorFeedResponse> authorFeedTypeInfo = (JsonTypeInfo<GetAuthorFeedResponse>)BlueskyJsonSerializerOptions.Options.GetTypeInfo(typeof(GetAuthorFeedResponse));

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

        Verify();
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("ProfileSelfLabels")]
    public int ProfileSelfLabelsCurrent()
    {
        int count = 0;
        foreach (ProfileViewBasic author in _authors)
        {
            count += author.SelfLabels.Count;
        }

        return count;
    }

    [Benchmark]
    [BenchmarkCategory("ProfileSelfLabels")]
    public int ProfileSelfLabelsLoop()
    {
        int count = 0;
        foreach (ProfileViewBasic author in _authors)
        {
            count += ProfileSelfLabels(author).Count;
        }

        return count;
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("PostSelfLabels")]
    public int PostSelfLabelsCurrent()
    {
        int count = 0;
        foreach (PostView post in _posts)
        {
            count += CurrentPostSelfLabels(post).Count;
        }

        return count;
    }

    [Benchmark]
    [BenchmarkCategory("PostSelfLabels")]
    public int PostSelfLabelsLoop()
    {
        int count = 0;
        foreach (PostView post in _posts)
        {
            count += PostSelfLabels(post).Count;
        }

        return count;
    }

    // A copy of the query PostView.SelfLabels runs the first time it is read.
    private static IReadOnlyList<string> CurrentPostSelfLabels(PostView post) =>
        post.Labels
            .Where(label => post.Author.Did == label.Source &&
                            post.Uri.ToString() == label.Uri &&
                            post.Cid == label.Cid)
            .Select(label => label.Value)
            .Distinct().ToList().AsReadOnly();

    // Skips the work when there are no labels, and builds the self URI once rather than once per label.
    private static IReadOnlyList<string> ProfileSelfLabels(ProfileViewBasic profile)
    {
        if (profile.Labels.Count == 0)
        {
            return [];
        }

        string? selfUri = null;
        List<string>? values = null;

        foreach (Label label in profile.Labels)
        {
            if (label.Source != profile.Did)
            {
                continue;
            }

            selfUri ??= $"at://{profile.Did}/app.bsky.actor.profile/self";
            if (label.Uri == selfUri && !(values?.Contains(label.Value) ?? false))
            {
                (values ??= []).Add(label.Value);
            }
        }

        return values is null ? [] : values;
    }

    // Skips the work when there are no labels, and turns the post URI into a string once rather than once per label.
    private static IReadOnlyList<string> PostSelfLabels(PostView post)
    {
        if (post.Labels.Count == 0)
        {
            return [];
        }

        string? uri = null;
        List<string>? values = null;

        foreach (Label label in post.Labels)
        {
            if (label.Source != post.Author.Did || label.Cid != post.Cid)
            {
                continue;
            }

            uri ??= post.Uri.ToString();
            if (label.Uri == uri && !(values?.Contains(label.Value) ?? false))
            {
                (values ??= []).Add(label.Value);
            }
        }

        return values is null ? [] : values.AsReadOnly();
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

    private void Verify()
    {
        foreach (ProfileViewBasic author in _authors)
        {
            if (!author.SelfLabels.SequenceEqual(ProfileSelfLabels(author)))
            {
                throw new InvalidOperationException($"Profile self labels differ for {author.Did}.");
            }
        }

        foreach (PostView post in _posts)
        {
            if (!post.SelfLabels.SequenceEqual(PostSelfLabels(post)) || !CurrentPostSelfLabels(post).SequenceEqual(PostSelfLabels(post)))
            {
                throw new InvalidOperationException($"Post self labels differ for {post.Uri}.");
            }
        }
    }
}
