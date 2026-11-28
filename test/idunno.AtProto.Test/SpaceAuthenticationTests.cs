// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

#pragma warning disable BSKYSpaces

using System.Security.Cryptography;
using System.Text;

using idunno.AtProto.Space;

using Microsoft.Extensions.Time.Testing;

namespace idunno.AtProto.Test;

public class SpaceAuthenticationTests
{
    private static readonly SpaceAtUri s_space = new("at:space/did:plc:owner/app.bsky.bookmark/test");

    [Theory]
    [InlineData("", "8cf837d5bf1994f0fac3ee1faa671d07e9a38b7f6153bdbb8a66b90159ef7d13")]
    [InlineData("00", "3c7708f8ae1f510f4439fa514deb1c7ece7a29085a2e8270a84b6ad6481cc0b4")]
    [InlineData("54657374436f6e74657874", "f53fb127f67dabf35b14fae14b53e6ce5c49e572f95eb4ef7a3b3cb9cd85f12b")]
    public void ExportMatchesRfc9180P256ExportOnlyVectors(string exporterContext, string expected)
    {
        using ECDiffieHellman sender = ECDiffieHellman.Create(new ECParameters
        {
            Curve = ECCurve.NamedCurves.nistP256,
            D = Convert.FromHexString("2f18b059576a0ec5a17121c0fe7ec8f00ea86f7b046fa3889ac8f21f89dbd484"),
            Q = DecodePoint("04d804370b7e24b94749eb1dc8df6d4d4a5d75f9effad01739ebcad5c54a40d57aaa8b4190fc124dbde2e4f1e1d1b012a3bc4038157dc29b55533a932306d8d38d")
        });
        using ECDiffieHellman recipient = ECDiffieHellman.Create(new ECParameters
        {
            Curve = ECCurve.NamedCurves.nistP256,
            Q = DecodePoint("046c6bb9e1976402c692fef72552f4aaeedd83a5e5079de3d7ae732da0f397b15921fb9c52c9866affc8e29c0271a35937023a9245982ec18bab1eb157cf16fc33")
        });
        byte[] secret = SpaceCrypto.Export(
            sender: sender,
            recipient: recipient,
            info: Convert.FromHexString("4f6465206f6e2061204772656369616e2055726e"),
            exporterContext: Convert.FromHexString(exporterContext),
            length: 32);

        Assert.Equal(Convert.FromHexString(expected), secret);
    }

    [Theory]
    [InlineData("sig=( \"authorization\"   \"atproto-space-audience\" );tag=\"atproto-space-credential\"", "sig=(\"authorization\" \"atproto-space-audience\");tag=\"atproto-space-credential\"")]
    [InlineData("rev=00042, data=:AAE=:, flag=?1", "rev=42, data=:AAE=:, flag")]
    [InlineData("other=(token;active=?1);value=-00.010;date=@00012", "other=(token;active);value=-0.01;date=@12")]
    [InlineData("other=%\"%e2%82%ac%22\";value=\"a\\\\b\"", "other=%\"%e2%82%ac%22\";value=\"a\\\\b\"")]
    public void StructuredFieldsNormalizeForSignatureVerification(string input, string expected)
    {
        Assert.Equal(expected, SpaceStructuredFields.Serialize(SpaceStructuredFields.Parse(input)));
    }

    [Theory]
    [InlineData("")]
    [InlineData("sig=:AA=:,")]
    [InlineData("sig=:AA=:, sig=:BB=:")]
    [InlineData("sig=(\"header\");tag=\"a\";tag=\"b\"")]
    [InlineData("sig=(\"header\";sf=\"bad\") garbage")]
    [InlineData("sig=(\"header\"")]
    [InlineData("sig=\"invalid\\q\"")]
    [InlineData("sig=:A A=:")]
    [InlineData("sig=?2")]
    [InlineData("sig=1.1234")]
    [InlineData("sig=1234567890123456")]
    [InlineData("sig=%\"%ff\"")]
    public void MalformedStructuredFieldsAreRejected(string input)
    {
        Assert.Throws<InvalidDataException>(() => SpaceStructuredFields.Parse(input));
    }

    [Theory]
    [InlineData("valid")]
    [InlineData("keyless")]
    [InlineData("wrong-key")]
    [InlineData("wrong-algorithm")]
    [InlineData("wrong-component")]
    [InlineData("extra-component")]
    [InlineData("missing-sf")]
    [InlineData("false-sf")]
    [InlineData("duplicate-tag")]
    [InlineData("token-tag")]
    [InlineData("token-component")]
    [InlineData("token-uri")]
    [InlineData("token-key")]
    [InlineData("altered-uri")]
    [InlineData("altered-cid")]
    [InlineData("duplicate-metadata")]
    [InlineData("signature-parameter")]
    public void MacVerificationEnforcesTheRecordProfileInsteadOfOnlyCheckingTheHash(string mutation)
    {
        using ECDiffieHellman key = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
        Did share = SpaceCrypto.ToDidKey(key.ExportParameters(false));
        SpaceAtUri uri = new(
            spaceDid: s_space.SpaceDid,
            spaceType: s_space.SpaceType,
            spaceKey: s_space.SpaceKey,
            authorDid: new Did("did:plc:writer"),
            collection: new Nsid("app.bsky.bookmark"),
            recordKey: new RecordKey("record"));
        Cid cid = Cid.FromDagCbor([0xa0]);
        byte[] secret = new byte[32];
        string metadata = $"uri=\"{uri}\", cid=\"{cid}\"";
        string parameters = $"(\"atproto-space-record\";sf);keyid=\"{share}\";tag=\"atproto-space-record\";alg=\"hmac-sha256\"";
        switch (mutation)
        {
            case "keyless": parameters = "(\"atproto-space-record\";sf);tag=\"atproto-space-record\""; break;
            case "wrong-key": parameters = parameters.Replace(share.Value, "did:key:wrong", StringComparison.Ordinal); break;
            case "wrong-algorithm": parameters = parameters.Replace("hmac-sha256", "hmac-sha512", StringComparison.Ordinal); break;
            case "wrong-component": parameters = parameters.Replace("(\"atproto-space-record\"", "(\"another\"", StringComparison.Ordinal); break;
            case "extra-component": parameters = parameters.Replace(";sf)", ";sf \"another\")", StringComparison.Ordinal); break;
            case "missing-sf": parameters = parameters.Replace(";sf", "", StringComparison.Ordinal); break;
            case "false-sf": parameters = parameters.Replace(";sf", ";sf=?0", StringComparison.Ordinal); break;
            case "token-tag": parameters = parameters.Replace("tag=\"atproto-space-record\"", "tag=atproto-space-record", StringComparison.Ordinal); break;
            case "token-component": parameters = parameters.Replace("(\"atproto-space-record\"", "(atproto-space-record", StringComparison.Ordinal); break;
            case "token-key": parameters = parameters.Replace($"keyid=\"{share}\"", $"keyid={share}", StringComparison.Ordinal); break;
            case "token-uri": metadata = $"uri={uri}, cid=\"{cid}\""; break;
            case "altered-uri": metadata = $"uri=\"{s_space}\", cid=\"{cid}\""; break;
            case "altered-cid": metadata = $"uri=\"{uri}\", cid=\"wrong\""; break;
            case "duplicate-metadata": metadata += $", cid=\"{cid}\""; break;
        }

        using HttpResponseMessage response = new();
        response.Headers.Add("Atproto-Space-Record", metadata);
        response.Headers.Add("Signature-Input", "changed=" + parameters +
            (mutation == "duplicate-tag" ? ", second=" + parameters : ""));
        byte[] signature = HMACSHA256.HashData(secret, Encoding.ASCII.GetBytes($"\"atproto-space-record\";sf: {metadata}\n\"@signature-params\": {parameters}"));
        response.Headers.Add("Signature", "changed=:" + Convert.ToBase64String(signature) + ":" +
            (mutation == "signature-parameter" ? ";unexpected" : ""));
        if (mutation is "valid" or "keyless")
        {
            SpaceResponseMac.Verify(
                headers: response.Headers,
                keyShare: share,
                secret: secret,
                uri: uri,
                cid: cid);
        }
        else
        {
            Assert.Throws<InvalidDataException>(() => SpaceResponseMac.Verify(
                headers: response.Headers,
                keyShare: share,
                secret: secret,
                uri: uri,
                cid: cid));
        }
    }

    [Fact]
    public void CredentialSignsTheAuthorizationAndExactAudienceAndExpiresItsKey()
    {
        FakeTimeProvider clock = CreateClock();
        ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        ECParameters publicKey = key.ExportParameters(false);
        using SpaceCredential credential = CreateCredential(key, clock);
        using HttpRequestMessage request = new(HttpMethod.Get, "https://example.com/xrpc/com.atproto.space.listRecords");
        Did audience = new("did:plc:writer");
        credential.SignRequest(request, s_space, audience);
        string authorization = Assert.Single(request.Headers.GetValues("Authorization"));
        string input = Assert.Single(request.Headers.GetValues("Signature-Input"));
        Assert.Equal("Atproto-Space-Credential test-token", authorization);
        Assert.Equal("did:plc:writer", Assert.Single(request.Headers.GetValues("Atproto-Space-Audience")));
        const string parameters = "(\"authorization\" \"atproto-space-audience\");tag=\"atproto-space-credential\"";
        Assert.Equal("sig=" + parameters, input);
        byte[] signature = Convert.FromBase64String(Assert.Single(request.Headers.GetValues("Signature"))[5..^1]);
        string signed = $"\"authorization\": {authorization}\n\"atproto-space-audience\": {audience}\n\"@signature-params\": {parameters}";
        using ECDsa verifier = ECDsa.Create(publicKey);
        Assert.True(verifier.VerifyData(
            data: Encoding.ASCII.GetBytes(signed),
            signature: signature,
            hashAlgorithm: HashAlgorithmName.SHA256,
            signatureFormat: DSASignatureFormat.IeeeP1363FixedFieldConcatenation));
        Assert.False(verifier.VerifyData(
            data: Encoding.ASCII.GetBytes(signed.Replace("did:plc:writer", "did:plc:other", StringComparison.Ordinal)),
            signature: signature,
            hashAlgorithm: HashAlgorithmName.SHA256,
            signatureFormat: DSASignatureFormat.IeeeP1363FixedFieldConcatenation));
        Assert.False(credential.IsExpired);

        clock.Advance(TimeSpan.FromMinutes(10));

        Assert.True(credential.IsExpired);
        Assert.Throws<ObjectDisposedException>(() => key.SignData("test"u8, HashAlgorithmName.SHA256));
        Assert.Throws<InvalidOperationException>(() => credential.EnsureUsable(s_space));
    }

    [Fact]
    public void RenewedCredentialIsIndependentOfThePreviousCredential()
    {
        FakeTimeProvider clock = CreateClock();
        using SpaceCredential first = CreateCredential(ECDsa.Create(ECCurve.NamedCurves.nistP256), clock);
        using SpaceCredential second = CreateCredential(ECDsa.Create(ECCurve.NamedCurves.nistP256), clock);
        Assert.NotEqual(first.KeyId, second.KeyId);
        first.Dispose();
        Assert.Throws<ObjectDisposedException>(() => first.EnsureUsable(s_space));
        second.EnsureUsable(s_space);
        Assert.Throws<ArgumentException>(() => second.EnsureUsable(new SpaceAtUri("at:space/did:plc:owner/app.bsky.bookmark/another")));
    }

    [Fact]
    public async Task DelegationIsConsumedByOnlyOneConcurrentAuthorizationAttempt()
    {
        FakeTimeProvider clock = CreateClock();
        SpaceDelegationToken delegation = new(
            token: "token",
            space: s_space,
            issuer: new Did("did:plc:user"),
            expiresAt: clock.GetUtcNow().AddMinutes(1),
            clock: clock);
        TaskCompletionSource start = new(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task<bool> Consume()
        {
            await start.Task;
            try
            {
                return delegation.Consume(s_space) == "token";
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }

        Task<bool>[] attempts = [Consume(), Consume()];
        start.SetResult();
        Assert.Single(await Task.WhenAll(attempts), succeeded => succeeded);
        Assert.True(delegation.IsUsed);
    }

    [Fact]
    public void WrongSpaceDoesNotConsumeDelegationAndExpirationPreventsUse()
    {
        FakeTimeProvider clock = CreateClock();
        SpaceDelegationToken token = new(
            token: "token",
            space: s_space,
            issuer: new Did("did:plc:user"),
            expiresAt: clock.GetUtcNow().AddMinutes(1),
            clock: clock);
        Assert.Throws<ArgumentException>(() => token.Consume(new SpaceAtUri("at:space/did:plc:owner/app.bsky.bookmark/other")));
        Assert.False(token.IsUsed);
        clock.Advance(TimeSpan.FromMinutes(1));
        Assert.True(token.IsExpired);
        Assert.Throws<InvalidOperationException>(() => token.Consume(s_space));
    }

    /// <summary>Decodes an RFC vector's uncompressed P-256 public point.</summary>
    private static ECPoint DecodePoint(string hex)
    {
        byte[] point = Convert.FromHexString(hex);
        return new ECPoint { X = point[1..33], Y = point[33..65] };
    }

    /// <summary>Creates a fixed clock shared by token generation, lifetime checks and expiry timers.</summary>
    private static FakeTimeProvider CreateClock() => new(new DateTimeOffset(
        year: 2026,
        month: 11,
        day: 27,
        hour: 12,
        minute: 0,
        second: 0,
        offset: TimeSpan.Zero));

    /// <summary>Transfers a test key into a ten-minute credential using the fixed test clock.</summary>
    private static SpaceCredential CreateCredential(ECDsa key, FakeTimeProvider clock) => new(
        space: s_space,
        keyId: SpaceCrypto.ToDidKey(key.ExportParameters(false)),
        token: "test-token",
        key: key,
        issuedAt: clock.GetUtcNow(),
        expiresAt: clock.GetUtcNow().AddMinutes(10),
        tokenId: "test-id",
        spaceRev: null,
        clock: clock);
}
