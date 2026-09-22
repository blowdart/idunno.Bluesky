// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;
using idunno.Bluesky.Graph;

namespace idunno.Bluesky.Test;

/// <summary>
/// Covers the lexicon length limits on the graph records.
/// </summary>
/// <remarks>
/// <para>AT Protocol lexicons express their <c>maxLength</c> constraints in UTF-8 bytes, not in UTF-16 code units,
/// so validating against <see cref="string.Length"/> accepts values the service rejects. Where a lexicon declares
/// both <c>maxGraphemes</c> and <c>maxLength</c> both limits apply.</para>
/// </remarks>
[ExcludeFromCodeCoverage]
public class GraphRecordValidationTests
{
    // A four code point family emoji. One grapheme, two UTF-16 code units per code point, and 25 UTF-8 bytes.
    private const string FamilyEmoji = "\U0001F468\u200D\U0001F469\u200D\U0001F467\u200D\U0001F466";

    private static readonly AtUri s_list = new("at://did:plc:test/app.bsky.graph.list/1");

    [Fact]
    public void ListNameLimitIsMeasuredInUtf8BytesRatherThanUtf16CodeUnits()
    {
        // Three family emoji are 33 UTF-16 code units but 75 UTF-8 bytes, so this is under the old limit and over the real one.
        string name = string.Concat(Enumerable.Repeat(FamilyEmoji, 3));

        Assert.True(name.Length <= Maximum.ListNameLengthInBytes);
        Assert.True(name.GetUtf8Length() > Maximum.ListNameLengthInBytes);

        Assert.Throws<ArgumentOutOfRangeException>(() => new List(name, ListPurpose.CurateList, description: null));
    }

    [Fact]
    public void ListNameAcceptsAValueAtTheByteLimit()
    {
        string name = new('a', Maximum.ListNameLengthInBytes);

        List list = new(name, ListPurpose.CurateList, description: null);

        Assert.Equal(name, list.Name);
    }

    [Fact]
    public void ListNameSetterIsMeasuredInUtf8Bytes()
    {
        List list = new("name", ListPurpose.CurateList, description: null);

        Assert.Throws<ArgumentOutOfRangeException>(() => list.Name = string.Concat(Enumerable.Repeat(FamilyEmoji, 3)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ListDescriptionEnforcesTheByteLimitAsWellAsTheGraphemeLimit(bool useSetter)
    {
        // 200 family emoji are 200 graphemes, which is inside the grapheme limit, but 5000 UTF-8 bytes, which is not.
        string description = string.Concat(Enumerable.Repeat(FamilyEmoji, 200));

        Assert.True(description.GetGraphemeLength() <= Maximum.ListDescriptionLengthInGraphemes);
        Assert.True(description.GetUtf8Length() > Maximum.ListDescriptionLengthInBytes);

        if (useSetter)
        {
            List list = new("name", ListPurpose.CurateList, description: null);
            Assert.Throws<ArgumentOutOfRangeException>(() => list.Description = description);
        }
        else
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new List("name", ListPurpose.CurateList, description));
        }
    }

    [Fact]
    public void ListDescriptionStillEnforcesTheGraphemeLimit()
    {
        string description = new('a', Maximum.ListDescriptionLengthInGraphemes + 1);

        Assert.True(description.GetUtf8Length() <= Maximum.ListDescriptionLengthInBytes);

        Assert.Throws<ArgumentOutOfRangeException>(() => new List("name", ListPurpose.CurateList, description));
    }

    [Fact]
    public void StarterPackRejectsAnEmptyName()
    {
        Assert.Throws<ArgumentException>(
            () => new StarterPack(string.Empty, description: null, s_list, feeds: null, DateTimeOffset.UtcNow, updatedAt: null));
    }

    [Fact]
    public void StarterPackRejectsANullList()
    {
        Assert.Throws<ArgumentNullException>(
            () => new StarterPack("name", description: null, null!, feeds: null, DateTimeOffset.UtcNow, updatedAt: null));
    }

    [Fact]
    public void StarterPackNameEnforcesTheGraphemeLimit()
    {
        string name = new('a', Maximum.StarterPackNameLengthInGraphemes + 1);

        Assert.True(name.GetUtf8Length() <= Maximum.StarterPackNameLengthInBytes);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => new StarterPack(name, description: null, s_list, feeds: null, DateTimeOffset.UtcNow, updatedAt: null));
    }

    [Fact]
    public void StarterPackNameEnforcesTheByteLimit()
    {
        // 50 family emoji are 50 graphemes, which is inside the grapheme limit, but 1250 UTF-8 bytes, which is not.
        string name = string.Concat(Enumerable.Repeat(FamilyEmoji, Maximum.StarterPackNameLengthInGraphemes));

        Assert.True(name.GetGraphemeLength() <= Maximum.StarterPackNameLengthInGraphemes);
        Assert.True(name.GetUtf8Length() > Maximum.StarterPackNameLengthInBytes);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => new StarterPack(name, description: null, s_list, feeds: null, DateTimeOffset.UtcNow, updatedAt: null));
    }

    [Fact]
    public void StarterPackDescriptionEnforcesTheByteLimit()
    {
        string description = string.Concat(Enumerable.Repeat(FamilyEmoji, 200));

        Assert.True(description.GetGraphemeLength() <= Maximum.StarterPackDescriptionLengthInGraphemes);
        Assert.True(description.GetUtf8Length() > Maximum.StarterPackDescriptionLengthInBytes);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => new StarterPack("name", description, s_list, feeds: null, DateTimeOffset.UtcNow, updatedAt: null));
    }

    [Fact]
    public void StarterPackDescriptionEnforcesTheGraphemeLimit()
    {
        string description = new('a', Maximum.StarterPackDescriptionLengthInGraphemes + 1);

        Assert.True(description.GetUtf8Length() <= Maximum.StarterPackDescriptionLengthInBytes);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => new StarterPack("name", description, s_list, feeds: null, DateTimeOffset.UtcNow, updatedAt: null));
    }

    [Fact]
    public void StarterPackNameSetterIsValidated()
    {
        StarterPack starterPack = new("name", description: null, s_list, feeds: null, DateTimeOffset.UtcNow, updatedAt: null);

        Assert.Throws<ArgumentException>(() => starterPack.Name = string.Empty);
        Assert.Throws<ArgumentOutOfRangeException>(
            () => starterPack.Name = new string('a', Maximum.StarterPackNameLengthInGraphemes + 1));
    }

    [Fact]
    public void StarterPackListSetterRejectsNull()
    {
        StarterPack starterPack = new("name", description: null, s_list, feeds: null, DateTimeOffset.UtcNow, updatedAt: null);

        Assert.Throws<ArgumentNullException>(() => starterPack.List = null!);
    }

    [Fact]
    public void StarterPackAcceptsAValidRecord()
    {
        StarterPack starterPack = new(
            "name",
            "description",
            s_list,
            feeds: null,
            DateTimeOffset.UtcNow,
            updatedAt: null);

        Assert.Equal("name", starterPack.Name);
        Assert.Equal("description", starterPack.Description);
        Assert.Equal(s_list, starterPack.List);
    }

    /// <summary>
    /// <see cref="ListPurpose.Unknown"/> only exists to carry a purpose this library does not recognize, so a list
    /// must not be created with it. Without this guard it is the default value of the enum and would fail later,
    /// when the record is serialized.
    /// </summary>
    [Fact]
    public void AListCannotBeCreatedWithAnUnknownPurpose()
    {
        ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => new List("name", ListPurpose.Unknown, description: null));

        Assert.Equal("purpose", exception.ParamName);
    }

    [Fact]
    public void AListCannotBeCreatedWithTheDefaultPurpose()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new List("name", default, description: null));
    }
}