// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;
using idunno.Bluesky.RichText;

namespace idunno.Bluesky.Test;

[ExcludeFromCodeCoverage]
public class RichTextObjectModelTests
{
    [Theory]
    [InlineData(-1, 10)]
    [InlineData(0, -1)]
    [InlineData(-10, -5)]
    [InlineData(50, 10)]
    public void ByteSliceThrowsWhenTheRangeIsNotValid(long byteStart, long byteEnd)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ByteSlice(byteStart, byteEnd));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(0, 10)]
    [InlineData(10, 10)]
    public void ByteSliceAcceptsAValidRange(long byteStart, long byteEnd)
    {
        ByteSlice byteSlice = new(byteStart, byteEnd);

        Assert.Equal(byteStart, byteSlice.ByteStart);
        Assert.Equal(byteEnd, byteSlice.ByteEnd);
    }

    [Fact]
    public void FacetFeaturesAreCopiedFromTheCollectionTheCallerSupplied()
    {
        List<FacetFeature> features = [new TagFacetFeature("tag")];

        Facet facet = new(new ByteSlice(0, 4), features);

        features.Clear();

        Assert.Single(facet.Features);
    }

    [Fact]
    public void FacetFeaturesCannotBeChangedThroughTheReturnedCollection()
    {
        Facet facet = new(new ByteSlice(0, 4), [new TagFacetFeature("tag")]);

        Assert.True(facet.Features.IsReadOnly);
        Assert.Throws<NotSupportedException>(() => facet.Features.Add(new TagFacetFeature("another")));
    }

    [Fact]
    public void HashTagAcceptsATagOfTheMaximumAllowedLength()
    {
        string tag = new('a', Maximum.TagLengthInGraphemes);

        HashTag hashTag = new(tag);

        Assert.Equal(tag, hashTag.Tag);
        Assert.Equal($"#{tag}", hashTag.Text);
    }

    [Fact]
    public void HashTagDoesNotValidateItsDisplayTextAgainstTheTagLimits()
    {
        string text = new('a', Maximum.TagLengthInGraphemes + 10);

        HashTag hashTag = new("tag", text);

        Assert.Equal("tag", hashTag.Tag);
        Assert.Equal(text, hashTag.Text);
    }

    [Fact]
    public void ToStringOnAFacetFeatureWithNoTextReturnsAnEmptyString()
    {
        TextlessFacetFeature facetFeature = new();

        Assert.Null(facetFeature.Text);
        Assert.Equal(string.Empty, facetFeature.ToString());
    }

    [Fact]
    public void LinkFacetFeatureExposesTheUriItWasConstructedWith()
    {
        LinkFacetFeature linkFacetFeature = new("https://example.org/page");

        Assert.Equal("https://example.org/page", linkFacetFeature.Uri);
    }

    // A derived record would otherwise have its own ToString() synthesized, which would hide the
    // implementation under test, so this type forwards to the base implementation explicitly.
    private sealed record TextlessFacetFeature : PostBuilderFacetFeature
    {
        public override string ToString() => base.ToString();
    }
}
