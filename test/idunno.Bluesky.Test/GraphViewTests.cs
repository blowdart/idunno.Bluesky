// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;
using idunno.AtProto.Labels;
using idunno.Bluesky.Actor;
using idunno.Bluesky.Graph;

namespace idunno.Bluesky.Test;

/// <summary>
/// Covers the construction semantics of the graph view types.
/// </summary>
/// <remarks>
/// <para>A view is a projection of whatever the service returned, so it validates only what the type system requires
/// and derives anything it can rather than snapshotting it.</para>
/// </remarks>
[ExcludeFromCodeCoverage]
public class GraphViewTests
{
    private static readonly AtUri s_uri = new("at://did:plc:test/app.bsky.graph.starterpack/1");

    private static readonly AtUri s_otherUri = new("at://did:plc:test/app.bsky.graph.starterpack/2");

    private static readonly Cid s_cid = new("bafyreib2rxk3rh6kzwq6y7ug4eqhfhpqaqzqvuflstfpvgkzjt7b5yfkzy");

    private static readonly Cid s_otherCid = new("bafyreic6ymjhqzqvtnqgcx7fcnmhdmxlhfqpvbkhkpkzy7yqvq7zqvq7za");

    private static readonly AtUri s_listUri = new("at://did:plc:test/app.bsky.graph.list/1");

    private static StarterPack CreateRecord() => new("name", "description", s_listUri, feeds: null, DateTimeOffset.UtcNow, updatedAt: null);

    private static ProfileViewBasic CreateCreator() => new(
        new Did("did:plc:test"),
        new Handle("test.invalid"),
        displayName: null,
        pronouns: null,
        website: null,
        avatar: null,
        associated: null,
        viewer: null,
        labels: null,
        createdAt: null,
        verification: null,
        status: null);

    private static StarterPackViewBasic CreateView(IReadOnlyCollection<Label>? labels = null) => new(
        s_uri,
        s_cid,
        CreateRecord(),
        CreateCreator(),
        listItemCount: 1,
        joinedWeekCount: 2,
        joinedAllTimeCount: 3,
        labels,
        DateTimeOffset.UtcNow);

    [Fact]
    public void StarterPackViewBasicStrongReferenceReflectsTheCurrentUriAndCid()
    {
        StarterPackViewBasic view = CreateView();

        Assert.Equal(s_uri, view.StrongReference.Uri);
        Assert.Equal(s_cid, view.StrongReference.Cid);
    }

    /// <summary>
    /// The strong reference used to be built once in the constructor and stored, so copying a view with a different
    /// uri or cid left the copy pointing at the record the original came from.
    /// </summary>
    [Fact]
    public void StarterPackViewBasicStrongReferenceFollowsACopyWhichChangesTheUriAndCid()
    {
        StarterPackViewBasic copy = CreateView() with { Uri = s_otherUri, Cid = s_otherCid };

        Assert.Equal(s_otherUri, copy.StrongReference.Uri);
        Assert.Equal(s_otherCid, copy.StrongReference.Cid);
    }

    [Fact]
    public void StarterPackViewBasicThrowsOnNullArguments()
    {
        StarterPack record = CreateRecord();
        ProfileViewBasic creator = CreateCreator();
        DateTimeOffset indexedAt = DateTimeOffset.UtcNow;

        Assert.Equal("uri", Assert.Throws<ArgumentNullException>(
            () => new StarterPackViewBasic(null!, s_cid, record, creator, 0, 0, 0, null, indexedAt)).ParamName);
        Assert.Equal("cid", Assert.Throws<ArgumentNullException>(
            () => new StarterPackViewBasic(s_uri, null!, record, creator, 0, 0, 0, null, indexedAt)).ParamName);
        Assert.Equal("record", Assert.Throws<ArgumentNullException>(
            () => new StarterPackViewBasic(s_uri, s_cid, null!, creator, 0, 0, 0, null, indexedAt)).ParamName);
        Assert.Equal("creator", Assert.Throws<ArgumentNullException>(
            () => new StarterPackViewBasic(s_uri, s_cid, record, null!, 0, 0, 0, null, indexedAt)).ParamName);
    }

    /// <summary>
    /// The labels collection is copied rather than aliased, so a caller cannot mutate a view after constructing it.
    /// </summary>
    [Fact]
    public void StarterPackViewBasicCopiesTheLabelsItIsGiven()
    {
        List<Label> labels = [];

        StarterPackViewBasic view = CreateView(labels);

        labels.Add(new Label(
            version: null,
            source: new Did("did:plc:labeler"),
            uri: s_uri.ToString(),
            cid: null,
            value: "spam",
            isNegationLabel: false,
            creationTimestamp: DateTimeOffset.UtcNow,
            signature: []));

        Assert.Empty(view.Labels);
    }

    [Fact]
    public void StarterPackViewBasicDefaultsLabelsToAnEmptyCollection()
    {
        Assert.Empty(CreateView().Labels);
    }

    [Fact]
    public void ListViewBasicAcceptsANameTheLexiconWouldReject()
    {
        // Three family emoji. Three graphemes, but 75 UTF-8 bytes, which is over the 64 byte lexicon limit.
        string name = string.Concat(Enumerable.Repeat("\U0001F468\u200D\U0001F469\u200D\U0001F467\u200D\U0001F466", 3));

        ListViewBasic view = new(s_listUri, s_cid, name, ListPurpose.CurateList, null, null, null, null, null);

        Assert.Equal(name, view.Name);
    }

    [Fact]
    public void ListViewBasicThrowsOnNullArguments()
    {
        Assert.Equal("uri", Assert.Throws<ArgumentNullException>(
            () => new ListViewBasic(null!, s_cid, "name", ListPurpose.CurateList, null, null, null, null, null)).ParamName);
        Assert.Equal("cid", Assert.Throws<ArgumentNullException>(
            () => new ListViewBasic(s_listUri, null!, "name", ListPurpose.CurateList, null, null, null, null, null)).ParamName);
        Assert.Equal("name", Assert.Throws<ArgumentNullException>(
            () => new ListViewBasic(s_listUri, s_cid, null!, ListPurpose.CurateList, null, null, null, null, null)).ParamName);
    }
}
