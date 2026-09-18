// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Security.Claims;
using System.Text;
using System.Text.Json;

using idunno.AtProto.Authentication;

namespace idunno.AtProto.Test;

[ExcludeFromCodeCoverage]
public class CredentialTests
{
    private const string FirstDid = "did:plc:abcdefghijklmnopqrstuvwx";
    private const string SecondDid = "did:plc:zyxwvutsrqponmlkjihgfedc";

    private static readonly Uri s_service = new("https://service.test/");

    private static string Base64UrlEncode(string value) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(value)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static string CreateJwt(
        string? subject = FirstDid,
        string? audience = "did:web:service.test",
        DateTimeOffset? expires = null)
    {
        Dictionary<string, object> payload = [];

        if (subject is not null)
        {
            payload["sub"] = subject;
        }

        if (audience is not null)
        {
            payload["aud"] = audience;
        }

        payload["exp"] = (expires ?? DateTimeOffset.UtcNow.AddMinutes(30)).ToUnixTimeSeconds();

        return $"{Base64UrlEncode("{\"alg\":\"none\"}")}.{Base64UrlEncode(JsonSerializer.Serialize(payload))}.";
    }

    [Fact]
    public void SettingTheAccessJwtOnAnAccessTokenCredentialUpdatesTheDidAndExpiryToMatchTheNewToken()
    {
        DateTimeOffset secondExpiry = DateTimeOffset.UtcNow.AddHours(4);

        AccessTokenCredential credential = new(s_service, CreateJwt());

        Assert.Equal(new Did(FirstDid), credential.Did);

        credential.AccessJwt = CreateJwt(subject: SecondDid, expires: secondExpiry);

        Assert.Equal(new Did(SecondDid), credential.Did);
        Assert.Equal(secondExpiry.ToUnixTimeSeconds(), credential.ExpiresOn.ToUnixTimeSeconds());
    }

    [Fact]
    public void SettingTheAccessJwtOnAnAccessCredentialUpdatesTheDidAndExpiryToMatchTheNewToken()
    {
        DateTimeOffset secondExpiry = DateTimeOffset.UtcNow.AddHours(4);

        AccessCredentials credential = new(s_service, AuthenticationType.UsernamePassword, CreateJwt(), "refresh");

        Assert.Equal(new Did(FirstDid), credential.Did);

        credential.AccessJwt = CreateJwt(subject: SecondDid, expires: secondExpiry);

        Assert.Equal(new Did(SecondDid), credential.Did);
        Assert.Equal(secondExpiry.ToUnixTimeSeconds(), credential.ExpiresOn.ToUnixTimeSeconds());
    }

    [Fact]
    public void SettingTheAccessJwtOnAServiceCredentialUpdatesTheDidToMatchTheNewToken()
    {
        // Service JWTs carry no subject, so a service credential takes its identity from the audience.
        ServiceCredential credential = new(s_service, CreateJwt(audience: FirstDid));

        Assert.Equal(new Did(FirstDid), credential.Did);

        credential.AccessJwt = CreateJwt(audience: SecondDid);

        Assert.Equal(new Did(SecondDid), credential.Did);
    }

    [Fact]
    public void ARejectedAccessJwtLeavesAnAccessCredentialHoldingItsPreviousTokenDidAndExpiry()
    {
        string originalJwt = CreateJwt();

        AccessCredentials credential = new(s_service, AuthenticationType.UsernamePassword, originalJwt, "refresh");

        DateTimeOffset originalExpiry = credential.ExpiresOn;

        Assert.ThrowsAny<ArgumentException>(() => credential.AccessJwt = CreateJwt(subject: "not-a-did"));

        Assert.Equal(originalJwt, credential.AccessJwt);
        Assert.Equal(new Did(FirstDid), credential.Did);
        Assert.Equal(originalExpiry, credential.ExpiresOn);
    }

    [Fact]
    public void ARejectedAccessJwtLeavesAnAccessTokenCredentialHoldingItsPreviousTokenDidAndExpiry()
    {
        string originalJwt = CreateJwt();

        AccessTokenCredential credential = new(s_service, originalJwt);

        DateTimeOffset originalExpiry = credential.ExpiresOn;

        Assert.ThrowsAny<ArgumentException>(() => credential.AccessJwt = CreateJwt(subject: "not-a-did"));

        Assert.Equal(originalJwt, credential.AccessJwt);
        Assert.Equal(new Did(FirstDid), credential.Did);
        Assert.Equal(originalExpiry, credential.ExpiresOn);
    }

    [Fact]
    public void AServiceTokenCarryingNoAudienceIsRejectedWithAnArgumentExceptionRatherThanAnInvalidOperationException()
    {
        ArgumentException exception = Assert.Throws<ArgumentException>(
            () => new ServiceCredential(s_service, CreateJwt(audience: null)));

        Assert.Equal("jwt", exception.ParamName);
        Assert.StartsWith("Service token carries no audience.", exception.Message, StringComparison.Ordinal);
    }

    private static ClaimsIdentity CreateClaimsIdentity(
        string didClaim = FirstDid,
        string? accessTokenSubject = FirstDid,
        string issuer = "https://service.test/")
    {
        List<Claim> claims =
        [
            new Claim(AtProtoClaims.Did, didClaim, ClaimValueTypes.String, issuer),
            new Claim(AtProtoClaims.AccessToken, CreateJwt(subject: accessTokenSubject), ClaimValueTypes.String, issuer),
            new Claim(AtProtoClaims.RefreshToken, "refresh", ClaimValueTypes.String, issuer),
            new Claim(AtProtoClaims.DPoPProof, "proof", ClaimValueTypes.String, issuer),
            new Claim(AtProtoClaims.DPoPNonce, "nonce", ClaimValueTypes.String, issuer),
        ];

        return new ClaimsIdentity(claims, "Test");
    }

    [Fact]
    public void AClaimsIdentityWhoseDidClaimAgreesWithTheAccessTokenSubjectCreatesCredentials()
    {
        Assert.True(AtProtoCredential.TryCreate(CreateClaimsIdentity(), out DPoPAccessCredentials? credentials));

        Assert.NotNull(credentials);
        Assert.Equal(new Did(FirstDid), credentials.Did);
        Assert.Equal(s_service, credentials.Service);
    }

    [Fact]
    public void AClaimsIdentityWhoseDidClaimDisagreesWithTheAccessTokenSubjectIsRejected()
    {
        Assert.False(
            AtProtoCredential.TryCreate(
                CreateClaimsIdentity(didClaim: FirstDid, accessTokenSubject: SecondDid),
                out DPoPAccessCredentials? credentials));

        Assert.Null(credentials);
    }

    [Theory]
    [InlineData("ftp://service.test/")]
    [InlineData("file:///service.test/")]
    [InlineData("javascript:alert(1)")]
    [InlineData("LOCAL AUTHORITY")]
    public void AClaimsIdentityWhoseDidClaimIssuerIsNotAnAbsoluteHttpUriIsRejected(string issuer)
    {
        Assert.False(
            AtProtoCredential.TryCreate(CreateClaimsIdentity(issuer: issuer), out DPoPAccessCredentials? credentials));

        Assert.Null(credentials);
    }

    [Theory]
    [InlineData("https://service.test/")]
    [InlineData("http://service.test/")]
    public void AClaimsIdentityWhoseDidClaimIssuerIsAnAbsoluteHttpUriIsAccepted(string issuer)
    {
        Assert.True(
            AtProtoCredential.TryCreate(CreateClaimsIdentity(issuer: issuer), out DPoPAccessCredentials? credentials));

        Assert.NotNull(credentials);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("notAJwt")]
    [InlineData("one.two")]
    [InlineData("one.two.three.four.five.six")]
    public void ConstructingAnAccessTokenCredentialFromAValueWhichIsNotAJwtThrowsArgumentException(string accessJwt)
    {
        Assert.Throws<ArgumentException>(() => new AccessTokenCredential(s_service, accessJwt));
    }

    [Theory]
    [InlineData("notADid")]
    [InlineData("did:")]
    [InlineData("https://service.test/")]
    public void ConstructingAnAccessTokenCredentialFromAJwtWhoseSubjectIsNotADidThrowsArgumentException(string subject)
    {
        Assert.Throws<ArgumentException>(() => new AccessTokenCredential(s_service, CreateJwt(subject: subject)));
    }

    [Fact]
    public void SettingTheAccessJwtOnAnAccessTokenCredentialToAValueWhichIsNotAJwtThrowsArgumentException()
    {
        AccessTokenCredential credential = new(s_service, CreateJwt());

        Assert.Throws<ArgumentException>(() => credential.AccessJwt = "notAJwt");
    }

    [Theory]
    [InlineData("notAJwt")]
    [InlineData("one.two")]
    [InlineData("one.two.three.four.five.six")]
    public void ConstructingDPoPAccessCredentialsFromAValueWhichIsNotAJwtThrowsArgumentException(string accessJwt)
    {
        Assert.Throws<ArgumentException>(
            () => new DPoPAccessCredentials(s_service, accessJwt, "refreshToken", "proofKey", "nonce"));
    }
}
