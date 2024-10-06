// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Xunit;

namespace idunno.AtProto.Serialization.Test
{
    [ExcludeFromCodeCoverage]
    public class AtIdentifierJsonConverterTests
    {
        private readonly JsonSerializerOptions _jsonSerializerOptions = new(JsonSerializerDefaults.Web);

        [Theory]
        // Valid handles
        [InlineData("XX.LCS.MIT.EDU", typeof(Handle))]
        [InlineData("john.test", typeof(Handle))]
        [InlineData("jan.test", typeof(Handle))]
        [InlineData("a234567890123456789.test", typeof(Handle))]
        [InlineData("john2.test", typeof(Handle))]
        [InlineData("john-john.test", typeof(Handle))]

        // Valid DIDs
        [InlineData("did:method:val", typeof(Did))]
        [InlineData("did:method:VAL", typeof(Did))]
        [InlineData("did:method:val123", typeof(Did))]
        [InlineData("did:method:123", typeof(Did))]
        [InlineData("did:method:val-two", typeof(Did))]
        public void ValidAtIdentifiersShouldDeserializeAsCorrectType(string identifier, Type type)
        {
            string json = $"{{\"atIdentifier\":\"{identifier}\"}}";

            AtIdentifierExample? actual = JsonSerializer.Deserialize<AtIdentifierExample>(json, options: _jsonSerializerOptions);

            Assert.NotNull(actual);
            Assert.Equal(type, actual.AtIdentifier.GetType());

            if (type == typeof(Did))
            {
                Did expectedDid = new(identifier);
                Did? actualDid = actual.AtIdentifier as Did;

                Assert.NotNull(actualDid);
                Assert.Equal(expectedDid, actualDid);
            }
            else
            {
                Handle expectedHandle = new(identifier);
                Handle? actualHandle = actual.AtIdentifier as Handle;

                Assert.NotNull(actualHandle);
                Assert.Equal(expectedHandle, actualHandle);
            }
        }

        class AtIdentifierExample
        {
            public AtIdentifierExample(AtIdentifier atIdentifier) => AtIdentifier = atIdentifier;

            public AtIdentifier AtIdentifier { get; }
        }
    }
}
