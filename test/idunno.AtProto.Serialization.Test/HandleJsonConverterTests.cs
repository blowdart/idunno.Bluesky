// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

using Xunit;

namespace idunno.AtProto.Serialization.Test
{
    [ExcludeFromCodeCoverage]
    public class HandleJsonConverterTests
    {
        private readonly JsonSerializerOptions _jsonSerializerOptions = new(JsonSerializerDefaults.Web);

        [Theory]
        [InlineData("jay.bsky.social")]
        [InlineData("name.t--t")] //  not a real TLD, but syntax ok
        [InlineData("xn--notarealidn.com")]
        [InlineData("example.t")] //  not a real TLD, but syntax ok
        [InlineData("xn--ls8h.test")]
        public void ValidHandleSerializesCorrectly(string value)
        {
            string expected = $"{{\"handle\":\"{value}\"}}";
            AtHandleExample atHandleExample = new(new Handle(value));

            string actual = JsonSerializer.Serialize(atHandleExample, options: _jsonSerializerOptions);
            Assert.Equal(expected, actual);
        }

        [Theory]
        [InlineData("jay.bsky.social")]
        [InlineData("name.t--t")] //  not a real TLD, but syntax ok
        [InlineData("xn--notarealidn.com")]
        [InlineData("example.t")] //  not a real TLD, but syntax ok
        [InlineData("xn--ls8h.test")]
        public void ValidHandleDeserializesCorrectly(string handle)
        {
            Handle expected = new(handle);

            string json = $"{{\"handle\":\"{handle}\"}}";

            AtHandleExample? actual = JsonSerializer.Deserialize<AtHandleExample>(json, options: _jsonSerializerOptions);

            Assert.NotNull(actual);

            Assert.Equal(expected, actual.Handle);
        }

        [Theory]
        [InlineData("{\"handle\": 0}")]
        [InlineData("{\"handle\": }")]
        [InlineData("{\"handle\":\"\"}")]
        [InlineData("{\"handle\":\"jo@hn.test\"}")]
        public void InvalidHandleThrowsJsonExceptionWhenDeserializing(string json)
        {
            Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<AtHandleExample>(json, options: _jsonSerializerOptions));
        }

        class AtHandleExample
        {
            public AtHandleExample(Handle handle) => Handle = handle;

            public Handle Handle { get; }
        }
    }
}
