// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

using Xunit;

namespace idunno.AtProto.Serialization.Test
{
    [ExcludeFromCodeCoverage]
    public class AtCidJsonConverterTests
    {
        private readonly JsonSerializerOptions _jsonSerializerOptions = new (JsonSerializerDefaults.Web);

        [Fact]
        public void ValidAtCidSerializesCorrectly()
        {
            const string cidAsString = "bafyreievgu2ty7qbiaaom5zhmkznsnajuzideek3lo7e65dwqlrvrxnmo4";
            const string expected = $"{{\"cid\":\"{cidAsString}\"}}";

            AtCidExample atCidExample = new(new AtCid(cidAsString));

            string actual = JsonSerializer.Serialize(atCidExample, options: _jsonSerializerOptions);

            Assert.Equal(expected, actual);
        }

        [Fact]
        public void ValidAtCidDeserializesCorrectly()
        {
            const string cidAsString = "bafyreievgu2ty7qbiaaom5zhmkznsnajuzideek3lo7e65dwqlrvrxnmo4";
            const string cidAsJson = $"{{\"cid\":\"{cidAsString}\"}}";

            AtCid expected = new (cidAsString);

            AtCidExample? actual = JsonSerializer.Deserialize<AtCidExample>(cidAsJson, options: _jsonSerializerOptions);

            Assert.NotNull(actual);
            Assert.Equal(expected.Value, actual!.Cid.Value);

        }

        [Fact]
        public void InvalidAtCidThrowsJsonExceptionWhenDeserializing()
        {
            string nonStringValueAsJson = "{\"cid\": 0}";
            string nullCidAsJson = "{\"cid\":}";
            string emptyCidAsJson = "{\"cid\":\"\"}";

            Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<AtCidExample>(nonStringValueAsJson, options: _jsonSerializerOptions));
            Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<AtCidExample>(nullCidAsJson, options: _jsonSerializerOptions));
            Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<AtCidExample>(emptyCidAsJson, options: _jsonSerializerOptions));
        }

        class AtCidExample
        {
            public AtCidExample(AtCid cid) => Cid = cid;

            public AtCid Cid { get; }
        }
    }
}
