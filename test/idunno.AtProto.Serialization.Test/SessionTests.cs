// Copyright(c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Text.Json;
using idunno.AtProto.Authentication.Models;

namespace idunno.AtProto.Serialization.Test
{
    [ExcludeFromCodeCoverage]
    public class SessionTests
    {
        private readonly JsonSerializerOptions _jsonSerializerOptions;

        public SessionTests()
        {
            _jsonSerializerOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
            _jsonSerializerOptions.TypeInfoResolverChain.Insert(0, SourceGenerationContext.Default);
        }

        [Fact]
        public void CreateSessionRequestIdentifierPasswordRequestSerializesProperly()
        {
            const string identifier = "identifier";
            const string password = "password";

            CreateSessionRequest request = new()
            {
                Identifier = identifier,
                Password = password
            };

            string requestAsJson = JsonSerializer.Serialize(request, _jsonSerializerOptions);

            Assert.NotNull(requestAsJson);
            Assert.Equal("{\"identifier\":\"identifier\",\"password\":\"password\"}", requestAsJson);
        }

        [Fact]
        public void CreateSessionRequestIdentifierPasswordAuthFactorRequestSerializesProperly()
        {
            const string identifier = "identifier";
            const string password = "password";
            const string authFactorToken = "authFactorToken";

            CreateSessionRequest request = new()
            {
                Identifier = identifier,
                Password = password,
                AuthFactorToken = authFactorToken
            };

            string requestAsJson = JsonSerializer.Serialize(request, _jsonSerializerOptions);

            Assert.NotNull(requestAsJson);
            Assert.Equal("{\"identifier\":\"identifier\",\"password\":\"password\",\"authFactorToken\":\"authFactorToken\"}", requestAsJson);
        }

        [Fact]
        public void CreateSessionRequestRoundTrips()
        {
            const string identifier = "identifier";
            const string password = "password";
            const string authFactorToken = "authFactorToken";

            CreateSessionRequest request = new()
            {
                Identifier = identifier,
                Password = password,
                AuthFactorToken = authFactorToken
            };

            string requestAsJson = JsonSerializer.Serialize(request, _jsonSerializerOptions);

            CreateSessionRequest? deserializedRequest = JsonSerializer.Deserialize<CreateSessionRequest>(requestAsJson, _jsonSerializerOptions);

            Assert.NotNull(deserializedRequest);

            Assert.Equal(identifier, deserializedRequest.Identifier);
            Assert.Equal(password, deserializedRequest.Password);
            Assert.Equal(authFactorToken, deserializedRequest.AuthFactorToken);
        }
    }
}
