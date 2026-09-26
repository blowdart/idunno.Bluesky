// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Nodes;

using idunno.AtProto.Repo;
using idunno.AtProto.Repo.Models;

namespace idunno.AtProto.Test;

/// <summary>
/// Covers the argument validation, strong reference calculation and value equality of the repository result types.
/// </summary>
[ExcludeFromCodeCoverage]
public class RepositoryResultTests
{
    private static readonly AtUri s_firstUri = new("at://did:plc:abcdefghijklmnopqrstuvwx/blue.idunno.test/rkey1");
    private static readonly AtUri s_secondUri = new("at://did:plc:abcdefghijklmnopqrstuvwx/blue.idunno.test/rkey2");
    private static readonly Cid s_firstCid = "bafyreib2rxk3rh6kzwq6y7ug4eqhfhpqaqzqvuflstfpvgkzjt7b5yfkzy";
    private static readonly Cid s_secondCid = "bafyreicypmumcyemtsrblhm4r4cawkjax744amgpzmb2fcksfut4g7rvya";

    [Theory]
    [InlineData(true, false, "uri")]
    [InlineData(false, true, "cid")]
    public void CreateRecordResultValidatesItsArguments(bool nullUri, bool nullCid, string expectedParameterName)
    {
        AtUri uri = nullUri ? null! : s_firstUri;
        Cid cid = nullCid ? null! : s_firstCid;

        ArgumentNullException fromStringOverload =
            Assert.Throws<ArgumentNullException>(() => new CreateRecordResult(uri, cid, null, (string?)null));
        ArgumentNullException fromEnumOverload =
            Assert.Throws<ArgumentNullException>(() => new CreateRecordResult(uri, cid, null, (ValidationStatus?)null));

        Assert.Equal(expectedParameterName, fromStringOverload.ParamName, StringComparer.Ordinal);
        Assert.Equal(expectedParameterName, fromEnumOverload.ParamName, StringComparer.Ordinal);
    }

    [Fact]
    public void ApplyWritesUpdateResultThrowsOnANullResponse()
    {
        ArgumentNullException exception =
            Assert.Throws<ArgumentNullException>(() => new ApplyWritesUpdateResult(null!));

        Assert.Equal("applyWritesUpdateResponse", exception.ParamName, StringComparer.Ordinal);
    }

    [Fact]
    public void AtProtoRecordCopyConstructorThrowsOnANullRecord()
    {
        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(() => new AtProtoRecord(null!));

        Assert.Equal("record", exception.ParamName, StringComparer.Ordinal);
    }

    [Fact]
    public void ChangingTheUriOfARepositoryRecordChangesItsStrongReference()
    {
        AtProtoRepositoryRecord record = new(s_firstUri, s_firstCid, null);
        AtProtoRepositoryRecord changed = record with { Uri = s_secondUri };

        Assert.Equal(s_secondUri, changed.StrongReference.Uri);
        Assert.Equal(s_firstCid, changed.StrongReference.Cid);
    }

    [Fact]
    public void ChangingTheCidOfARepositoryRecordChangesItsStrongReference()
    {
        AtProtoRepositoryRecord record = new(s_firstUri, s_firstCid, null);
        AtProtoRepositoryRecord changed = record with { Cid = s_secondCid };

        Assert.Equal(s_firstUri, changed.StrongReference.Uri);
        Assert.Equal(s_secondCid, changed.StrongReference.Cid);
    }

    [Fact]
    public void ChangingTheUriOfAnApplyWritesUpdateResultChangesItsStrongReference()
    {
        ApplyWritesUpdateResult result = new(new ApplyWritesUpdateResponse(s_firstUri, s_firstCid));
        ApplyWritesUpdateResult changed = result with { Uri = s_secondUri };

        Assert.Equal(s_secondUri, changed.StrongReference.Uri);
        Assert.Equal(s_firstCid, changed.StrongReference.Cid);
    }

    [Fact]
    public void TwoRepositoryRecordsWithTheSameExtensionDataAreEqual()
    {
        AtProtoRepositoryRecord left = new(s_firstUri, s_firstCid, null) { ExtensionData = CreateExtensionData("key", "\"value\"") };
        AtProtoRepositoryRecord right = new(s_firstUri, s_firstCid, null) { ExtensionData = CreateExtensionData("key", "\"value\"") };

        Assert.Equal(left, right);
        Assert.Equal(left.GetHashCode(), right.GetHashCode());
    }

    [Fact]
    public void TwoRepositoryRecordsWithDifferentExtensionDataAreNotEqual()
    {
        AtProtoRepositoryRecord left = new(s_firstUri, s_firstCid, null) { ExtensionData = CreateExtensionData("key", "\"value\"") };
        AtProtoRepositoryRecord right = new(s_firstUri, s_firstCid, null) { ExtensionData = CreateExtensionData("key", "\"other\"") };

        Assert.NotEqual(left, right);
    }

    [Fact]
    public void TwoRepositoryRecordsWithDifferentUrisAreNotEqual()
    {
        AtProtoRepositoryRecord left = new(s_firstUri, s_firstCid, null);
        AtProtoRepositoryRecord right = new(s_secondUri, s_firstCid, null);

        Assert.NotEqual(left, right);
    }

    [Fact]
    public void TwoRepositoryRecordsWithDifferentValuesAreNotEqual()
    {
        AtProtoRepositoryRecord left = new(s_firstUri, s_firstCid, JsonNode.Parse("""{"key":"value"}""")!.AsObject());
        AtProtoRepositoryRecord right = new(s_firstUri, s_firstCid, JsonNode.Parse("""{"key":"other"}""")!.AsObject());

        Assert.NotEqual(left, right);
    }

    [Fact]
    public void TwoTypedRepositoryRecordsWithTheSameExtensionDataAreEqual()
    {
        AtProtoRepositoryRecord<AtProtoRecord> left =
            new(s_firstUri, s_firstCid, new AtProtoRecord()) { ExtensionData = CreateExtensionData("key", "\"value\"") };
        AtProtoRepositoryRecord<AtProtoRecord> right =
            new(s_firstUri, s_firstCid, new AtProtoRecord()) { ExtensionData = CreateExtensionData("key", "\"value\"") };

        Assert.Equal(left, right);
        Assert.Equal(left.GetHashCode(), right.GetHashCode());
    }

    [Fact]
    public void TwoTypedRepositoryRecordsWithDifferentExtensionDataAreNotEqual()
    {
        AtProtoRepositoryRecord<AtProtoRecord> left =
            new(s_firstUri, s_firstCid, new AtProtoRecord()) { ExtensionData = CreateExtensionData("key", "\"value\"") };
        AtProtoRepositoryRecord<AtProtoRecord> right =
            new(s_firstUri, s_firstCid, new AtProtoRecord()) { ExtensionData = CreateExtensionData("key", "\"other\"") };

        Assert.NotEqual(left, right);
    }

    private static Dictionary<string, JsonElement> CreateExtensionData(string key, string json) =>
        new() { { key, JsonDocument.Parse(json).RootElement } };
}
