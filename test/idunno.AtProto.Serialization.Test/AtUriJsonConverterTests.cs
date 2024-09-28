// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

using Xunit;

namespace idunno.AtProto.Serialization.Test
{
    [ExcludeFromCodeCoverage]
    public class AtUriJsonConverterTests
    {
        private readonly JsonSerializerOptions _jsonSerializerOptions = new(JsonSerializerDefaults.Web);

        [Fact]
        public void ValidAtUriSerializesCorrectly()
        {
            const string atUriAsString = "at://did:plc:z72i7hdynmk6r22z27h6tvur/app.bsky.feed.generator/whats-hot";

            const string expected = $"{{\"uri\":\"{atUriAsString}\"}}";
            AtUriSample atCidExample = new(new AtUri(atUriAsString));

            string actual = JsonSerializer.Serialize(atCidExample, options: _jsonSerializerOptions);

            Assert.Equal(expected, actual);
        }

        [Fact]
        public void ValidAtUriDeserializesCorrectly()
        {
            const string atUriAsString = "at://did:plc:z72i7hdynmk6r22z27h6tvur/app.bsky.feed.generator/whats-hot";

            const string expected = $"{{\"uri\":\"{atUriAsString}\"}}";
            AtUriSample atUriExample = new(new AtUri(atUriAsString));

            string actual = JsonSerializer.Serialize(atUriExample, options: _jsonSerializerOptions);

            Assert.Equal(expected, actual);
        }

        [Theory]
        [InlineData("{\"uri\": 0}")]
        [InlineData("{\"uri\":}")]
        [InlineData("{\"uri\":\"\"}")]
        [InlineData("{\"uri\":\"bogus://did:plc:identifier/lexiconType/rkey#fragment\"}")]
        [InlineData("{\"uri\":\"at://did:plc:identifier/lexiconType/INVALID RKEY\"}")]
        public void InvalidAtUriOrRkeyThrowsJsonExceptionWhenDeserializing(string value)
        {
            Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<AtUriSample>(value, options: _jsonSerializerOptions));
        }

        class AtUriSample
        {
            public AtUriSample(AtUri uri) => Uri = uri;

            public AtUri Uri { get; }
        }

    }
}
