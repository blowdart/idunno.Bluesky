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

    [Theory]
    [InlineData("https://PDS.test.internal:443/path?query=value", "resource-nonce")]
    [InlineData("https://user@pds.test.internal/path", "resource-nonce")]
    [InlineData("https://pds.test.internal:8443/path", null)]
    [InlineData("http://pds.test.internal/path", null)]
    [InlineData("https://authority.test/token", "authority-nonce")]
    [InlineData("https://unrelated.test/path", null)]
    public void ProofNoncesAreScopedBySchemeHostAndPortForAllCredentialKinds(string destination, string? expectedNonce)
    {
        DPoPAccessCredentials access = new(
            service: s_service,
            accessJwt: JwtBuilder.CreateJwt(new Did("did:plc:identifier")),
            refreshToken: "refresh",
            dPoPProofKey: Duende.IdentityModel.OidcClient.DPoP.JsonWebKeys.CreateECDsaJson(),
            dPoPNonce: "legacy-nonce")
        {
            DPoPNonces = new Dictionary<string, string>
            {
                ["https://pds.test.internal"] = "resource-nonce",
                ["https://authority.test"] = "authority-nonce"
            }
        };
        AtProtoCredential[] credentials = [access, new DPoPRefreshCredential(access), new DPoPRevokeCredentials(access)];
        foreach (AtProtoCredential credential in credentials)
        {
            using HttpRequestMessage request = new(HttpMethod.Post, destination);
            credential.SetAuthenticationHeaders(request);
            JsonWebToken proof = new(Assert.Single(request.Headers.GetValues("DPoP")));
            string? nonce = proof.TryGetClaim("nonce", out System.Security.Claims.Claim? claim) ? claim.Value : null;
            Assert.Equal(expectedNonce, nonce);
        }
    }

    [Fact]
    public void RestoredNonceMapsAreCopiedAndReturnedSnapshotsDoNotChange()
    {
        Dictionary<string, string> persisted = new() { ["https://pds.test.internal"] = "original" };
        DPoPAccessCredentials access = new(
            service: s_service,
            accessJwt: JwtBuilder.CreateJwt(new Did("did:plc:identifier")),
            refreshToken: "refresh",
            dPoPProofKey: JwtBuilder.CreateProofKey(),
            dPoPNonce: "legacy-nonce")
        {
            DPoPNonces = persisted
        };
        persisted["https://pds.test.internal"] = "mutated";
        IReadOnlyDictionary<string, string> snapshot = access.DPoPNonces;
        DPoPRefreshCredential refresh = new(access);
        access.DPoPNonce = "updated";

        Assert.Equal("original", snapshot["https://pds.test.internal"]);
        Assert.Equal("updated", refresh.DPoPNonces["https://pds.test.internal"]);
    }

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
