// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;

using idunno.AtProto.Labels;

namespace idunno.AtProto.Serialization.Test;

public class SelfLabelsTests
{
    private readonly JsonSerializerOptions _jsonSerializerOptions = new(JsonSerializerDefaults.Web);

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t")]
    public void RequiredLabelValuesAcceptEmptyAndWhitespaceStrings(string value)
    {
        SelfLabel selfLabel = new(value);
        Label label = new(null, new Did("did:plc:ewvi7nxzyoun6zhxrhs64oiz"), "at://did:plc:ewvi7nxzyoun6zhxrhs64oiz/app.bsky.feed.post/test", null, value, false, DateTimeOffset.UtcNow, null);

        Assert.Equal(value, JsonSerializer.Deserialize<SelfLabel>(
            JsonSerializer.Serialize(selfLabel, AtProtoServer.AtProtoJsonSerializerOptions), AtProtoServer.AtProtoJsonSerializerOptions)!.Value);
        Assert.Equal(value, JsonSerializer.Deserialize<Label>(
            JsonSerializer.Serialize(label, AtProtoServer.AtProtoJsonSerializerOptions), AtProtoServer.AtProtoJsonSerializerOptions)!.Value);
    }

    [Fact]
    public void LabelValuesStillRejectNullAndEnforceTheMaximumByteLength()
    {
        Did did = new("did:plc:ewvi7nxzyoun6zhxrhs64oiz");
        Assert.Throws<ArgumentNullException>(() => new SelfLabel(null!));
        Assert.Throws<ArgumentNullException>(() => new Label(null, did, "at://did:plc:ewvi7nxzyoun6zhxrhs64oiz/app.bsky.feed.post/test", null, null!, false, DateTimeOffset.UtcNow, null));
        Assert.Equal(128, new SelfLabel(new string('a', 128)).Value.Length);
        Assert.Throws<ArgumentOutOfRangeException>(() => new SelfLabel(new string('a', 129)));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Label(null, did, "at://did:plc:ewvi7nxzyoun6zhxrhs64oiz/app.bsky.feed.post/test", null, new string('a', 129), false, DateTimeOffset.UtcNow, null));
    }

    [Fact]
    public void SelfLabelsSerializesProperlyWithSourceGeneratedJsonContext()
    {
        List<SelfLabel> labels = [new("selfLabel")];

        SelfLabels selfLabels = new(labels);

        selfLabels.AddLabel("selfLabel2");

        string selfLabelsAsJson = JsonSerializer.Serialize(selfLabels, AtProtoServer.AtProtoJsonSerializerOptions);

        Assert.Equal("{\"$type\":\"com.atproto.label.defs#selfLabels\",\"values\":[{\"val\":\"selfLabel\"},{\"val\":\"selfLabel2\"}]}", selfLabelsAsJson);
    }

    [Fact]
    public void SelfLabelsSerializesProperlyWithNoSourceGeneration()
    {
        List<SelfLabel> labels = [new("selfLabel")];

        SelfLabels selfLabels = new(labels);

        selfLabels.AddLabel("selfLabel2");

        string selfLabelsAsJson = JsonSerializer.Serialize(selfLabels, _jsonSerializerOptions);

        Assert.Equal("{\"$type\":\"com.atproto.label.defs#selfLabels\",\"values\":[{\"val\":\"selfLabel\"},{\"val\":\"selfLabel2\"}]}", selfLabelsAsJson);
    }

    [Fact]
    public void SelfLabelsDeserializesProperlyWithSourceGeneratedJsonContext()
    {
        string selfLabelsAsJson = """
        {
            "$type":"com.atproto.label.defs#selfLabels",
            "values": [
                {
                    "val":"selfLabel"
                },
                {
                    "val":"selfLabel2"
                }
            ]
        }
        """;

        SelfLabels? selfLabels = JsonSerializer.Deserialize<SelfLabels>(selfLabelsAsJson, AtProtoServer.AtProtoJsonSerializerOptions);

        Assert.NotNull(selfLabels);
        Assert.NotNull(selfLabels.Values);
        Assert.Equal(2, selfLabels.Values.Count);

        Assert.True(selfLabels.Contains("selfLabel"));
        Assert.True(selfLabels.Contains("selfLabel2"));

        selfLabels = JsonSerializer.Deserialize<SelfLabels>(selfLabelsAsJson, _jsonSerializerOptions);

        Assert.NotNull(selfLabels);
        Assert.NotNull(selfLabels.Values);
        Assert.Equal(2, selfLabels.Values.Count);

        Assert.True(selfLabels.Contains("selfLabel"));
        Assert.True(selfLabels.Contains("selfLabel2"));
    }


    [Fact]
    public void SelfLabelsDeserializesProperlyWithNoSourceGeneration()
    {
        string selfLabelsAsJson = """
        {
            "$type":"com.atproto.label.defs#selfLabels",
            "values": [
                {
                    "val":"selfLabel"
                },
                {
                    "val":"selfLabel2"
                }
            ]
        }
        """;

        SelfLabels? selfLabels = JsonSerializer.Deserialize<SelfLabels>(selfLabelsAsJson, _jsonSerializerOptions);

        Assert.NotNull(selfLabels);
        Assert.NotNull(selfLabels.Values);
        Assert.Equal(2, selfLabels.Values.Count);

        Assert.True(selfLabels.Contains("selfLabel"));
        Assert.True(selfLabels.Contains("selfLabel2"));
    }
}