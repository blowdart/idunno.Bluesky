// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace idunno.AtProto.Serialization.Test
{
    [ExcludeFromCodeCoverage]
    public class RecordKeyConverterTests
    {
        private readonly JsonSerializerOptions _jsonSerializerOptions = new(JsonSerializerDefaults.Web);

        [Theory]
        [InlineData("self")]
        [InlineData("example.com")]
        [InlineData("~1.2-3_")]
        [InlineData("dHJ1ZQ")]
        [InlineData("_")]
        [InlineData("literal:self")]
        [InlineData("pre:fix")]
        public void ValidRecordKeySerializesCorrectly(string value)
        {
            string expected = $"{{\"recordKey\":\"{value}\"}}";
            RecordKeyExample rKeyExample = new(new RecordKey(value));

            string actual = JsonSerializer.Serialize(rKeyExample, options: _jsonSerializerOptions);
            Assert.Equal(expected, actual);
        }

        [Theory]
        [InlineData("self")]
        [InlineData("example.com")]
        public void ValidRecordKeyDeserializesCorrectly(string recordKey)
        {
            RecordKey expected = new(recordKey);

            string json = $"{{\"recordKey\":\"{recordKey}\"}}";

            RecordKeyExample? actual = JsonSerializer.Deserialize<RecordKeyExample>(json, options: _jsonSerializerOptions);

            Assert.NotNull(actual);

            Assert.Equal(expected.Value, actual.RecordKey.Value);
        }

        [Theory]
        [InlineData("{\"recordKey\": 0}")]
        [InlineData("{\"recordKey\": }")]
        [InlineData("{\"recordKey\":\"\"}")]
        [InlineData("{\"recordKey\":\"alpha/beta\"}")]
        public void InvalidRecordThrowsJsonExceptionWhenDeserializing(string json)
        {
            Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<RecordKey>(json, options: _jsonSerializerOptions));
        }

        class RecordKeyExample
        {
            public RecordKeyExample(RecordKey recordKey) => RecordKey = recordKey;

            public RecordKey RecordKey { get; }
        }
    }
}
