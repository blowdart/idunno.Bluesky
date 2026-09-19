// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

using idunno.AtProto;
using idunno.Bluesky.Feed;
using idunno.Bluesky.Feed.Gates;

namespace idunno.Bluesky.Test;

[ExcludeFromCodeCoverage]
public class FeedRecordValidationTests
{
    private static readonly AtUri s_post = new("at://did:plc:hfgp6pj3akhqxntgqwramlbg/app.bsky.feed.post/3lcf6ry7xy22x");

    [Fact]
    public void ThreadGateRulesCannotBeAddedToAfterConstruction()
    {
        List<ThreadGateRule> rules = [new FollowingRule()];

        ThreadGate threadGate = new(s_post, rules);

        Assert.NotNull(threadGate.Rules);
        Assert.Throws<NotSupportedException>(() => threadGate.Rules.Add(new MentionRule()));
    }

    [Fact]
    public void ThreadGateRulesAreNotAffectedByLaterChangesToTheSourceCollection()
    {
        List<ThreadGateRule> rules = [new FollowingRule()];

        ThreadGate threadGate = new(s_post, rules);

        rules.Add(new MentionRule());

        Assert.NotNull(threadGate.Rules);
        Assert.Single(threadGate.Rules);
    }

    [Fact]
    public void ThreadGateHiddenRepliesCannotBeAddedToAfterConstruction()
    {
        List<AtUri> hiddenReplies = [s_post];

        ThreadGate threadGate = new(s_post, null, hiddenReplies);

        Assert.NotNull(threadGate.HiddenReplies);
        Assert.Throws<NotSupportedException>(() => threadGate.HiddenReplies.Add(s_post));
    }

    [Fact]
    public void PostGateRulesCannotBeAddedToAfterConstruction()
    {
        List<PostGateRule> rules = [new DisableEmbeddingRule()];

        PostGate postGate = new(s_post, rules);

        Assert.NotNull(postGate.Rules);
        Assert.Throws<NotSupportedException>(() => postGate.Rules.Add(new DisableEmbeddingRule()));
    }

    [Fact]
    public void PostGateDetachedEmbeddingUrisCannotBeAddedToAfterConstruction()
    {
        List<AtUri> detachedEmbeddingUris = [s_post];

        PostGate postGate = new(s_post, null, detachedEmbeddingUris);

        Assert.NotNull(postGate.DetachedEmbeddingUris);
        Assert.Throws<NotSupportedException>(() => postGate.DetachedEmbeddingUris.Add(s_post));
    }

    [Fact]
    public void PostGateDetachedEmbeddingUrisAreNotAffectedByLaterChangesToTheSourceCollection()
    {
        List<AtUri> detachedEmbeddingUris = [s_post];

        PostGate postGate = new(s_post, null, detachedEmbeddingUris);

        detachedEmbeddingUris.Add(s_post);

        Assert.NotNull(postGate.DetachedEmbeddingUris);
        Assert.Single(postGate.DetachedEmbeddingUris);
    }

    [Fact]
    public void ThreadViewPostDropsNullReplies()
    {
        const string json = """
            {
              "$type": "app.bsky.feed.defs#threadViewPost",
              "post": {
                "uri": "at://did:plc:hfgp6pj3akhqxntgqwramlbg/app.bsky.feed.post/3lcf6ry7xy22x",
                "cid": "bafyreihbfkwjqz3hfyvnlwvbxbfqvwzqkqcfcjhbgvqjqvqmgqgqjqgqgq",
                "author": { "did": "did:plc:hfgp6pj3akhqxntgqwramlbg", "handle": "test.invalid" },
                "record": { "$type": "app.bsky.feed.post", "text": "test", "createdAt": "2024-12-01T00:00:00Z" },
                "indexedAt": "2024-12-01T00:00:00Z"
              },
              "replies": [ null ]
            }
            """;

        ThreadViewPost? threadViewPost = JsonSerializer.Deserialize<ThreadViewPost>(json, BlueskyServer.BlueskyJsonSerializerOptions);

        Assert.NotNull(threadViewPost);
        Assert.NotNull(threadViewPost.Replies);
        Assert.Empty(threadViewPost.Replies);
    }

    [Theory]
    [InlineData(100, nameof(Maximum.PostsToList))]
    [InlineData(25, nameof(Maximum.PostsToGet))]
    [InlineData(1000, nameof(Maximum.PostThreadDepth))]
    [InlineData(1000, nameof(Maximum.PostThreadParentHeight))]
    public void FeedMaximumsMatchTheLexiconValues(int expected, string name)
    {
        int actual = name switch
        {
            nameof(Maximum.PostsToList) => Maximum.PostsToList,
            nameof(Maximum.PostsToGet) => Maximum.PostsToGet,
            nameof(Maximum.PostThreadDepth) => Maximum.PostThreadDepth,
            nameof(Maximum.PostThreadParentHeight) => Maximum.PostThreadParentHeight,
            _ => throw new ArgumentOutOfRangeException(nameof(name))
        };

        Assert.Equal(expected, actual);
    }
}
