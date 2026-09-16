// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text;

using idunno.AtProto.Authentication;

using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace idunno.AtProto.Integration.Test;

[ExcludeFromCodeCoverage]
public class DPoPCredentialTests
{
    private static readonly Uri s_service = new("https://pds.test.internal");

    [Fact]
    public void DPoPAccessCredentialsBindsProofToTheTokenInTheAuthorizationHeader()
    {
        DPoPAccessCredentials credentials = new(
            service: s_service,
            accessJwt: JwtBuilder.CreateJwt(new Did("did:plc:identifier")),
            refreshToken: "refresh",
            dPoPProofKey: JwtBuilder.CreateProofKey(),
            dPoPNonce: "nonce");

        using HttpRequestMessage request = new(HttpMethod.Post, s_service);
        credentials.SetAuthenticationHeaders(request);

        Assert.Equal(AccessTokenHash(credentials.AccessJwt), ProofAccessTokenHash(request));
    }

    [Fact]
    public void DPoPRefreshCredentialBindsProofToTheTokenInTheAuthorizationHeader()
    {
        DPoPRefreshCredential credentials = new(
            service: s_service,
            refreshToken: JwtBuilder.CreateJwt(new Did("did:plc:identifier")),
            dPoPProofKey: JwtBuilder.CreateProofKey(),
            dPoPNonce: "nonce");

        using HttpRequestMessage request = new(HttpMethod.Post, s_service);
        credentials.SetAuthenticationHeaders(request);

        Assert.Equal(AccessTokenHash(credentials.RefreshToken), ProofAccessTokenHash(request));
    }

    [Fact]
    public void DPoPAccessCredentialsProofRemainsBoundToOneTokenWhenAccessJwtChangesConcurrently()
    {
        string first = JwtBuilder.CreateJwt(new Did("did:plc:first"));
        string second = JwtBuilder.CreateJwt(new Did("did:plc:second"));

        DPoPAccessCredentials credentials = new(
            service: s_service,
            accessJwt: first,
            refreshToken: "refresh",
            dPoPProofKey: JwtBuilder.CreateProofKey(),
            dPoPNonce: "nonce");

        using ManualResetEventSlim stop = new(false);

        Thread mutator = new(() =>
        {
            bool useFirst = false;

            while (!stop.IsSet)
            {
                credentials.AccessJwt = useFirst ? first : second;
                useFirst = !useFirst;
            }
        })
        {
            IsBackground = true
        };

        mutator.Start();

        try
        {
            for (int iteration = 0; iteration < 200; iteration++)
            {
                using HttpRequestMessage request = new(HttpMethod.Post, s_service);
                credentials.SetAuthenticationHeaders(request);

                string presentedToken = request.Headers.Authorization!.Parameter!;

                Assert.Equal(AccessTokenHash(presentedToken), ProofAccessTokenHash(request));
            }
        }
        finally
        {
            stop.Set();
            mutator.Join();
        }
    }

    [Fact]
    public void DPoPRefreshCredentialProofRemainsBoundToOneTokenWhenRefreshTokenChangesConcurrently()
    {
        const string first = "refresh-token-one";
        const string second = "refresh-token-two";

        DPoPRefreshCredential credentials = new(
            service: s_service,
            refreshToken: first,
            dPoPProofKey: JwtBuilder.CreateProofKey(),
            dPoPNonce: "nonce");

        using ManualResetEventSlim stop = new(false);

        Thread mutator = new(() =>
        {
            bool useFirst = false;

            while (!stop.IsSet)
            {
                credentials.RefreshToken = useFirst ? first : second;
                useFirst = !useFirst;
            }
        })
        {
            IsBackground = true
        };

        mutator.Start();

        try
        {
            for (int iteration = 0; iteration < 200; iteration++)
            {
                using HttpRequestMessage request = new(HttpMethod.Post, s_service);
                credentials.SetAuthenticationHeaders(request);

                string presentedToken = request.Headers.Authorization!.Parameter!;

                Assert.Equal(AccessTokenHash(presentedToken), ProofAccessTokenHash(request));
            }
        }
        finally
        {
            stop.Set();
            mutator.Join();
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void DPoPRefreshCredentialConstructorStoresAnAbsentNonceAsAnEmptyString(string? nonce)
    {
        DPoPRefreshCredential credentials = new(
            service: s_service,
            refreshToken: "refresh",
            dPoPProofKey: JwtBuilder.CreateProofKey(),
            dPoPNonce: nonce!);

        Assert.Equal(string.Empty, credentials.DPoPNonce);
    }

    [Fact]
    public void DPoPRefreshCredentialNonceSetterStoresNullAsAnEmptyString()
    {
        DPoPRefreshCredential credentials = new(
            service: s_service,
            refreshToken: "refresh",
            dPoPProofKey: JwtBuilder.CreateProofKey(),
            dPoPNonce: "nonce")
        {
            DPoPNonce = null!
        };

        Assert.Equal(string.Empty, credentials.DPoPNonce);
    }

    [Fact]
    public void DPoPRevokeCredentialsThrowsArgumentNullExceptionWhenAccessCredentialsIsNull()
    {
        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(
            () => new DPoPRevokeCredentials(null!));

        Assert.Equal("accessCredentials", exception.ParamName);
    }

    private static string AccessTokenHash(string token)
    {
        return Base64UrlEncoder.Encode(SHA256.HashData(Encoding.ASCII.GetBytes(token)));
    }

    private static string? ProofAccessTokenHash(HttpRequestMessage request)
    {
        string proof = Assert.Single(request.Headers.GetValues("DPoP"));

        JsonWebToken proofToken = new(proof);

        return proofToken.TryGetClaim("ath", out System.Security.Claims.Claim? claim) ? claim.Value : null;
    }
}
