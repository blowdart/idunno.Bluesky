// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

using Xunit;

namespace idunno.AtProto.Serialization.Test
{
    [ExcludeFromCodeCoverage]
    public class NsidJsonConverterTests
    {
        private readonly JsonSerializerOptions _jsonSerializerOptions = new(JsonSerializerDefaults.Web);

        [Theory]
        [InlineData("a.b.c")]
        [InlineData("m.xn--masekowski-d0b.pl")]
        [InlineData("one.two.three")]
        [InlineData("one.two.three.four-and.FiVe")]
        public void ValidNsidSerializesCorrectly(string value)
        {
            string expected = $"{{\"nsid\":\"{value}\"}}";
            NsidExample nsidExample = new(new Nsid(value));

            string actual = JsonSerializer.Serialize(nsidExample, options: _jsonSerializerOptions);
            Assert.Equal(expected, actual);
        }

        [Theory]
        [InlineData("a.b.c")]
        [InlineData("m.xn--masekowski-d0b.pl")]
        [InlineData("one.two.three")]
        [InlineData("one.two.three.four-and.FiVe")]
        public void ValidNsidDeserializesCorrectly(string nsid)
        {
            Nsid expected = new(nsid);

            string json = $"{{\"nsid\":\"{nsid}\"}}";

            NsidExample? actual = JsonSerializer.Deserialize<NsidExample>(json, options: _jsonSerializerOptions);

            Assert.NotNull(actual);

            Assert.Equal(expected, actual.Nsid);
        }

        [Theory]
        [InlineData("{\"nsid\": 0}")]
        [InlineData("{\"nsid\": }")]
        [InlineData("{\"nsid\":\"\"}")]
        [InlineData("{\"nsid\":\"com.exa💩ple.thing\"}")]
        public void InvalidNsidThrowsJsonExceptionWhenDeserializing(string json)
        {
            Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<NsidExample>(json, options: _jsonSerializerOptions));
        }

        class NsidExample
        {
            public NsidExample(Nsid nsid) => Nsid = nsid;

            public Nsid Nsid { get; }
        }
    }
}
