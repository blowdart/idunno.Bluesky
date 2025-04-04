// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;

using idunno.AtProto.Labels;

namespace idunno.AtProto.Serialization.Test
{
    public class SelfLabelsTests
    {
        private readonly JsonSerializerOptions _jsonSerializerOptions = new(JsonSerializerDefaults.Web);

        [Fact]
        public void SelfLabelsSerializesProperlyWithSourceGeneratedJsonContext()
        {
            List<SelfLabel> labels = [new ("selfLabel")];

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
}
