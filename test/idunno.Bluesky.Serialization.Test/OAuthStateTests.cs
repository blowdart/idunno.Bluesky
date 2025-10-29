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
        public void OLoginAuthStateSerializesToJsonWithoutSourceGeneratedContextAndExtraProperties()
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
                correlationKey,
                new Dictionary<string, string>()
                {
                    { "key", "value" }
                });

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
                correlationKey,
                new Dictionary<string, string>()
                {
                    { "key", "value" }
                });

            string json = JsonSerializer.Serialize(expected, BlueskyServer.BlueskyJsonSerializerOptions);

            OAuthLoginState? actual = JsonSerializer.Deserialize<OAuthLoginState>(json, BlueskyServer.BlueskyJsonSerializerOptions);

            Assert.NotNull(actual);
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void OAuthLoginStateRoundTripsWithSourceGeneratedContextWithExtraProperties()
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
                correlationKey,
                new Dictionary<string, string>()
                {
                    { "key", "value" }
                });

            string json = JsonSerializer.Serialize(expected, BlueskyServer.BlueskyJsonSerializerOptions);

            OAuthLoginState? actual = JsonSerializer.Deserialize<OAuthLoginState>(json, BlueskyServer.BlueskyJsonSerializerOptions);

            Assert.NotNull(actual);
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void EqualityFailsWhenExtraPropertiesIsPresentOnLhsAndNullOnRhs()
        {
            Guid correlationKey = Guid.NewGuid();

            OAuthLoginState lhs = new(
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
                correlationKey,
                new Dictionary<string, string>()
                {
                    { "key", "value" }
                });

            OAuthLoginState rhs = new(
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

            Assert.NotEqual(lhs, rhs);
        }

        [Fact]
        public void EqualityFailsWhenExtraPropertiesIsPresentOnRhsAndNullOnLhs()
        {
            Guid correlationKey = Guid.NewGuid();

            OAuthLoginState lhs = new(
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

            OAuthLoginState rhs = new(
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
                correlationKey,
                new Dictionary<string, string>()
                {
                    { "key", "value" }
                });

            Assert.NotEqual(lhs, rhs);
        }

        [Fact]
        public void EqualityFailsWhenExtraPropertiesAreDifferent()
        {
            Guid correlationKey = Guid.NewGuid();

            OAuthLoginState lhs = new(
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
                correlationKey,
                new Dictionary<string, string>()
                {
                    { "key", "value" }
                });

            OAuthLoginState rhs = new(
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
                correlationKey,
                new Dictionary<string, string>()
                {
                    { "key", "differentValue" }
                });

            Assert.NotEqual(lhs, rhs);
        }
    }
}
