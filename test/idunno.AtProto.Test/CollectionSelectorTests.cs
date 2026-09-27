// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto.Jetstream;

namespace idunno.AtProto.Test;

[ExcludeFromCodeCoverage]
public class CollectionSelectorTests
{
    [Theory]
    [InlineData("app.bsky.feed.post")]
    [InlineData("com.example.thing")]
    public void AnExactCollectionIsNotAWildcardAndKeepsItsNsid(string value)
    {
        CollectionSelector selector = new(value);

        Assert.False(selector.IsWildcard);
        Assert.Equal(new Nsid(value), selector.Collection);
        Assert.Equal(value, selector.ToString());
    }

    [Theory]
    [InlineData("app.bsky.feed.*")]

    // The namespace is checked by appending a name, not by parsing it as an NSID, so a two part namespace is accepted
    // here although "app.bsky" is not a valid NSID. The server accepts it, so this must too.
    [InlineData("app.bsky.*")]
    [InlineData("com.example.a.b.c.*")]
    public void AWildcardIsRecognisedAndHasNoCollection(string value)
    {
        CollectionSelector selector = new(value);

        Assert.True(selector.IsWildcard);
        Assert.Null(selector.Collection);
        Assert.Equal(value, selector.ToString());
    }

    [Theory]

    // No "." before the "*", so it is not the wildcard form and is rejected as an NSID.
    [InlineData("app.bsky.fo*")]
    [InlineData("*")]
    [InlineData("*.bsky.feed.post")]
    [InlineData("app.*.feed.post")]

    // An empty namespace segment before the wildcard.
    [InlineData("app.bsky..*")]

    // A wildcard inside the namespace of another wildcard.
    [InlineData("app.bsky.feed.*.*")]
    [InlineData("not_an_nsid.*")]
    [InlineData("app.bsky.feed.post.")]
    public void AnInvalidSelectorIsRejected(string value)
    {
        Assert.Throws<NsidFormatException>(() => new CollectionSelector(value));
        Assert.False(CollectionSelector.TryParse(value, out CollectionSelector? result));
        Assert.Null(result);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void AnEmptySelectorIsRejected(string value)
    {
        Assert.Throws<ArgumentException>(() => new CollectionSelector(value));
    }

    [Fact]
    public void ANullSelectorIsRejected()
    {
        Assert.Throws<ArgumentNullException>(() => new CollectionSelector((string)null!));
    }

    [Fact]
    public void ANullNsidIsRejected()
    {
        Assert.Throws<ArgumentNullException>(() => new CollectionSelector((Nsid)null!));
    }

    [Theory]
    [InlineData("app.bsky.feed.post")]
    [InlineData("app.bsky.feed.*")]
    public void SelectorsWithTheSameValueAreEqual(string value)
    {
        CollectionSelector left = new(value);
        CollectionSelector right = new(value);

        Assert.Equal(left, right);
        Assert.True(left == right);
        Assert.False(left != right);
        Assert.Equal(left.GetHashCode(), right.GetHashCode());
    }

    [Fact]
    public void AWildcardDoesNotEqualTheNamespaceItMatches()
    {
        Assert.NotEqual(new CollectionSelector("app.bsky.feed.*"), new CollectionSelector("app.bsky.feed.post"));
    }

    [Fact]
    public void ASelectorIsNotEqualToNull()
    {
        CollectionSelector selector = new("app.bsky.feed.post");

        Assert.False(selector.Equals(null));
        Assert.False(selector == null);
        Assert.True(selector != null);
        Assert.False(selector.Equals((object)42));
    }

    [Fact]
    public void ASelectorCanBeCreatedFromAStringOrAnNsid()
    {
        CollectionSelector fromString = "app.bsky.feed.post";
        CollectionSelector fromNsid = new Nsid("app.bsky.feed.post");

        Assert.Equal(fromString, fromNsid);
        Assert.Equal(fromString, CollectionSelector.FromString("app.bsky.feed.post"));
        Assert.Equal(fromNsid, CollectionSelector.FromNsid(new Nsid("app.bsky.feed.post")));
    }

    [Fact]
    public void AValidSelectorIsParsed()
    {
        Assert.True(CollectionSelector.TryParse("app.bsky.feed.*", out CollectionSelector? result));
        Assert.NotNull(result);
        Assert.True(result.IsWildcard);
    }
}
