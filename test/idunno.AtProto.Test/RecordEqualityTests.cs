// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

using idunno.AtProto.Labels;
using idunno.AtProto.Repo;

namespace idunno.AtProto.Test;

/// <summary>
/// Covers the value equality of <see cref="AtProtoRecord"/> and <see cref="SelfLabels"/>, both of which expose
/// collections which would otherwise be compared by reference.
/// </summary>
[ExcludeFromCodeCoverage]
public class RecordEqualityTests
{
    [Fact]
    public void TwoRecordsWithDefaultExtensionDataAreEqual()
    {
        AtProtoRecord left = new();
        AtProtoRecord right = new();

        Assert.Equal(left, right);
        Assert.Equal(left.GetHashCode(), right.GetHashCode());
    }

    [Fact]
    public void ARecordWithNullExtensionDataEqualsARecordWithEmptyExtensionData()
    {
        AtProtoRecord left = new() { ExtensionData = null };
        AtProtoRecord right = new();

        Assert.Equal(left, right);
        Assert.Equal(left.GetHashCode(), right.GetHashCode());
    }

    [Fact]
    public void TwoRecordsWithTheSameExtensionDataAreEqual()
    {
        AtProtoRecord left = new() { ExtensionData = CreateExtensionData("key", "\"value\"") };
        AtProtoRecord right = new() { ExtensionData = CreateExtensionData("key", "\"value\"") };

        Assert.Equal(left, right);
        Assert.Equal(left.GetHashCode(), right.GetHashCode());
    }

    [Theory]
    [InlineData("key", "\"other\"")]
    [InlineData("other", "\"value\"")]
    public void TwoRecordsWithDifferentExtensionDataAreNotEqual(string key, string json)
    {
        AtProtoRecord left = new() { ExtensionData = CreateExtensionData("key", "\"value\"") };
        AtProtoRecord right = new() { ExtensionData = CreateExtensionData(key, json) };

        Assert.NotEqual(left, right);
    }

    [Fact]
    public void TwoRecordsWithDifferentExtensionDataCountsAreNotEqual()
    {
        Dictionary<string, JsonElement> extensionData = CreateExtensionData("key", "\"value\"");
        extensionData.Add("second", JsonDocument.Parse("1").RootElement);

        AtProtoRecord left = new() { ExtensionData = CreateExtensionData("key", "\"value\"") };
        AtProtoRecord right = new() { ExtensionData = extensionData };

        Assert.NotEqual(left, right);
    }

    [Fact]
    public void TwoSelfLabelsWithTheSameValuesAreEqual()
    {
        SelfLabels left = new([new SelfLabel("porn")]);
        SelfLabels right = new([new SelfLabel("porn")]);

        Assert.Equal(left, right);
        Assert.Equal(left.GetHashCode(), right.GetHashCode());
    }

    [Fact]
    public void TwoSelfLabelsWithDifferentValuesAreNotEqual()
    {
        SelfLabels left = new([new SelfLabel("porn")]);
        SelfLabels right = new([new SelfLabel("nudity")]);

        Assert.NotEqual(left, right);
    }

    [Fact]
    public void TwoEmptySelfLabelsAreEqual()
    {
        SelfLabels left = new();
        SelfLabels right = new();

        Assert.Equal(left, right);
        Assert.Equal(left.GetHashCode(), right.GetHashCode());
    }

    [Fact]
    public void SelfLabelsDoesNotEqualNullOrAnotherType()
    {
        SelfLabels selfLabels = new([new SelfLabel("porn")]);

        Assert.False(selfLabels.Equals(null));
        Assert.False(selfLabels.Equals("porn"));
    }

    [Fact]
    public void MutatingSelfLabelsChangesEquality()
    {
        SelfLabels left = new([new SelfLabel("porn")]);
        SelfLabels right = new([new SelfLabel("porn")]);

        Assert.Equal(left, right);

        left.AddLabel("nudity");

        Assert.NotEqual(left, right);
    }

    private static Dictionary<string, JsonElement> CreateExtensionData(string key, string json) =>
        new() { { key, JsonDocument.Parse(json).RootElement } };
}
