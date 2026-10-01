// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;
using idunno.AtProto.Labels;

namespace idunno.Bluesky.Test;

public class SelfLabelReaderTests
{
    private static readonly Did s_actor = new("did:plc:actor");
    private static readonly Did s_other = new("did:plc:other");
    private const string Uri = "at://did:plc:actor/app.bsky.feed.post/3koaf5bu5kq27";
    private static readonly Cid s_cid = new("bafyreievgu2ty7qbiaaom5zhmkznsnajuzideek3lo7e65dwqlrvrxnmo4");
    private static readonly Cid s_otherCid = new("bafyreie5737gdxlw5i64vzichcalba3z2v5n6icifvx5xytvske7mr3hpm");

    private static Label CreateLabel(Did source, string uri, Cid? cid, string value) =>
        new(version: 1, source, uri, cid, value, isNegationLabel: false, DateTimeOffset.UtcNow, signature: null);

    [Fact]
    public void ReadReturnsAnEmptyListWhenThereAreNoLabels()
    {
        Assert.Empty(SelfLabelReader.Read([], s_actor, Uri));
    }

    [Fact]
    public void ReadReturnsDistinctMatchingValuesInOrder()
    {
        List<Label> labels =
        [
            CreateLabel(s_actor, Uri, s_cid, "porn"),
            CreateLabel(s_other, Uri, s_cid, "spam"),
            CreateLabel(s_actor, "at://did:plc:actor/app.bsky.feed.post/other", s_cid, "gore"),
            CreateLabel(s_actor, Uri, s_cid, "nudity"),
            CreateLabel(s_actor, Uri, s_cid, "porn"),
        ];

        Assert.Equal(["porn", "nudity"], SelfLabelReader.Read(labels, s_actor, Uri));
    }

    [Fact]
    public void ReadOnlyMatchesTheCidWhenAsked()
    {
        List<Label> labels =
        [
            CreateLabel(s_actor, Uri, s_cid, "porn"),
            CreateLabel(s_actor, Uri, s_otherCid, "nudity"),
            CreateLabel(s_actor, Uri, null, "sexual"),
        ];

        Assert.Equal(["porn", "nudity", "sexual"], SelfLabelReader.Read(labels, s_actor, Uri));
        Assert.Equal(["porn"], SelfLabelReader.Read(labels, s_actor, Uri, matchCid: true, s_cid));
    }

    [Fact]
    public void ReadReturnsAnEmptyListWhenNothingMatches()
    {
        List<Label> labels = [CreateLabel(s_other, Uri, s_cid, "spam")];

        Assert.Empty(SelfLabelReader.Read(labels, s_actor, Uri));
    }
}
