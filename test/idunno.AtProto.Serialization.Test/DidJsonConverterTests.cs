// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace idunno.AtProto.Serialization.Test
{
    [ExcludeFromCodeCoverage]
    public class DidJsonConverterTests
    {
        private readonly JsonSerializerOptions _jsonSerializerOptions = new(JsonSerializerDefaults.Web);

        [Theory]
        [InlineData("did:web:discover.bsky.app")]
        [InlineData("did:plc:z72i7hdynmk6r22z27h6tvur")]
        public void ValidDidSerializesCorrectly(string value)
        {
            string expected = $"{{\"did\":\"{value}\"}}";
            AtDidExample atDidExample = new(new Did(value));

            string actual = JsonSerializer.Serialize(atDidExample, options: _jsonSerializerOptions);

            Assert.Equal(expected, actual);
        }

        [Theory]
        [InlineData("did:web:discover.bsky.app")]
        [InlineData("did:plc:z72i7hdynmk6r22z27h6tvur")]
        public void ValidDidDeserializesCorrectly(string did)
        {
            Did expected = new(did);

            string json = $"{{\"did\":\"{did}\"}}";

            AtDidExample? actual = JsonSerializer.Deserialize<AtDidExample>(json, options: _jsonSerializerOptions);

            Assert.NotNull(actual);

            Assert.Equal(expected.Value, actual.Did.Value);
        }

        [Theory]
        [InlineData("{\"did\": 0}")]
        [InlineData("{\"did\":}")]
        [InlineData("{\"did\":\"\"}")]
        public void InvalidAtDidThrowsJsonExceptionWhenDeserializing(string json)
        {
            Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<AtDidExample>(json, options: _jsonSerializerOptions));
        }

        class AtDidExample
        {
            public AtDidExample(Did did) => Did = did;

            public Did Did { get; }
        }
    }
}
