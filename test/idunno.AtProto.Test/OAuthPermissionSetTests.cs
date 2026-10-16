// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto.Authentication;

namespace idunno.AtProto.Test;

[ExcludeFromCodeCoverage]
public class OAuthPermissionSetTests
{
    [Theory]
    [InlineData(null, "include:com.example.authBasic")]
    [InlineData("*", "include:com.example.authBasic?aud=%2A")]
    [InlineData("did:web:api.example.com#appview", "include:com.example.authBasic?aud=did%3Aweb%3Aapi.example.com%23appview")]
    [InlineData("did:plc:example#service", "include:com.example.authBasic?aud=did%3Aplc%3Aexample%23service")]
    public void PermissionSetsProduceIncludeScopes(string? audience, string expected)
    {
        OAuthPermissionSet permissionSet = new("com.example.authBasic", audience);

        Assert.Equal("com.example.authBasic", permissionSet.Nsid.ToString());
        Assert.Equal(audience, permissionSet.Audience);
        Assert.Equal(expected, permissionSet.ToString());
        string scope = permissionSet;
        Assert.Equal(expected, scope);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("https://api.example.com#appview")]
    [InlineData("did:web:api.example.com")]
    [InlineData("did:web:api.example.com#")]
    [InlineData("did:web:api.example.com#app view")]
    [InlineData("did:web:api.example.com#appview#other")]
    [InlineData("did:web:api.example.com#appview\u0001")]
    public void InvalidAudiencesAreRejected(string audience)
    {
        Assert.Throws<ArgumentException>(() => new OAuthPermissionSet("com.example.authBasic", audience));
    }

    [Fact]
    public void NullNsidsAndNullConversionsAreRejected()
    {
        Assert.Throws<ArgumentNullException>(() => new OAuthPermissionSet(null!));
        Assert.Throws<ArgumentNullException>(() => (string)(OAuthPermissionSet)null!);
    }
}
