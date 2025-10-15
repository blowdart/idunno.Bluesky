// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;
using Duende.IdentityModel.OidcClient;
using idunno.AtProto.Authentication;

namespace idunno.Bluesky.Serialization.Test
{
    [ExcludeFromCodeCoverage]
    public class OAuthStateTests
    {
        private readonly JsonSerializerOptions _jsonSerializerOptions = new(JsonSerializerDefaults.Web);

        [Fact]
        public void OLoginAuthStateSerializesToJsonWithSourceGeneratedContext()
        {
            Guid correlationKey = Guid.NewGuid();

            OAuthLoginState oAuthLoginState = new (
                new AuthorizeState()
                {
                    CodeVerifier = "codeVerifier",
                    Error = "Error",
                    ErrorDescription = "ErrorDescription",
                    RedirectUri = "RedirectUri",
                    StartUrl = "StartUrl",
                    State = "state"
                },
                "expectedAuthority",
                "expectedService",
                "proofKey",
                correlationKey);

            JsonSerializer.Serialize(oAuthLoginState, BlueskyServer.BlueskyJsonSerializerOptions);
        }

        [Fact]
        public void OLoginAuthStateSerializesToJsonWithoutSourceGeneratedContext()
        {
            Guid correlationKey = Guid.NewGuid();

            OAuthLoginState oAuthLoginState = new(
                new AuthorizeState()
                {
                    CodeVerifier = "codeVerifier",
                    Error = "Error",
                    ErrorDescription = "ErrorDescription",
                    RedirectUri = "RedirectUri",
                    StartUrl = "StartUrl",
                    State = "state"
                },
                "expectedAuthority",
                "expectedService",
                "proofKey",
                correlationKey);

            JsonSerializer.Serialize(oAuthLoginState, _jsonSerializerOptions);
        }

        [Fact]
        public void OAuthLoginStateRoundTripsWithSourceGeneratedContext()
        {
            Guid correlationKey = Guid.NewGuid();

            OAuthLoginState expected = new(
                new AuthorizeState()
                {
                    CodeVerifier = "codeVerifier",
                    Error = "Error",
                    ErrorDescription = "ErrorDescription",
                    RedirectUri = "RedirectUri",
                    StartUrl = "StartUrl",
                    State = "state"
                },
                "expectedAuthority",
                "expectedService",
                "proofKey",
                correlationKey);

            string json = JsonSerializer.Serialize(expected, BlueskyServer.BlueskyJsonSerializerOptions);

            OAuthLoginState? actual = JsonSerializer.Deserialize<OAuthLoginState>(json, BlueskyServer.BlueskyJsonSerializerOptions);

            Assert.NotNull(actual);
            Assert.Equal(expected, actual);
        }
    }
}
