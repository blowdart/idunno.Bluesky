// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using Duende.IdentityModel.OidcClient;

using idunno.AtProto.Authentication;

namespace idunno.AtProto.Test;

[ExcludeFromCodeCoverage]
public class OAuthLoginStateTests
{
    private static readonly Guid s_correlationId = Guid.NewGuid();

    private static OAuthLoginState CreateState(
        IDictionary<string, string>? extraProperties = null,
        string redirectUri = "https://client.test/callback") =>
        new(
            startUrl: "https://authority.test/authorize",
            state: "state",
            codeVerifier: "codeVerifier",
            redirectUri: redirectUri,
            error: "error",
            errorDescription: "errorDescription",
            expectedAuthority: "https://authority.test/",
            expectedService: "https://pds.test/",
            proofKey: "proofKey",
            correlationId: s_correlationId,
            extraProperties: extraProperties);

    [Fact]
    public void EqualStatesWithDistinctButEquivalentExtraPropertiesProduceTheSameHashCode()
    {
        OAuthLoginState first = CreateState(new Dictionary<string, string>(StringComparer.Ordinal) { ["a"] = "1", ["b"] = "2" });
        OAuthLoginState second = CreateState(new Dictionary<string, string>(StringComparer.Ordinal) { ["b"] = "2", ["a"] = "1" });

        Assert.Equal(first, second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
    }

    [Fact]
    public void EqualStatesWithNoExtraPropertiesProduceTheSameHashCode()
    {
        Assert.Equal(CreateState().GetHashCode(), CreateState().GetHashCode());
    }

    [Fact]
    public void StatesDifferingOnlyByExtraPropertiesAreNotEqual()
    {
        OAuthLoginState first = CreateState(new Dictionary<string, string>(StringComparer.Ordinal) { ["a"] = "1" });
        OAuthLoginState second = CreateState(new Dictionary<string, string>(StringComparer.Ordinal) { ["a"] = "2" });

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void EqualStatesCollapseToASingleEntryInAHashSet()
    {
        HashSet<OAuthLoginState> set =
        [
            CreateState(new Dictionary<string, string>(StringComparer.Ordinal) { ["a"] = "1" }),
            CreateState(new Dictionary<string, string>(StringComparer.Ordinal) { ["a"] = "1" }),
        ];

        Assert.Single(set);
    }

    [Fact]
    public void StatesDifferingOnlyByRedirectUriAreNotEqual()
    {
        OAuthLoginState first = CreateState(redirectUri: "https://client.test/callback");
        OAuthLoginState second = CreateState(redirectUri: "https://attacker.test/callback");

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void ANullStateConvertsToANullAuthorizeState()
    {
        OAuthLoginState? state = null;

        Assert.Null((AuthorizeState?)state);
    }
}
