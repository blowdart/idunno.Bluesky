// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

using idunno.AtProto;
using idunno.AtProto.Labels;

using Microsoft.Extensions.Time.Testing;

namespace idunno.Bluesky.Labeler.Test;

/// <summary>Creates actual cryptographically verified reporter evidence using a provider-clock token lifetime.</summary>
internal static class ReportTestAuthentication
{
    /// <summary>Signs and verifies a method-scoped token instead of fabricating provider authentication evidence.</summary>
    internal static async Task<ModerationReportAuthenticationResult> CreateAsync(
        Did reporter,
        DateTimeOffset databaseTime,
        string tokenId,
        bool legacy = false,
        Did? labeler = null)
    {
        Did destination = labeler ?? StoreFixture.s_issuer;
        FakeTimeProvider clock = new(databaseTime);
        using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        byte[] privateKey = key.ExportPkcs8PrivateKey();
        string publicKey;
        try
        {
            using LabelSigningKey labelKey = LabelSigningKey.ImportPkcs8PrivateKey(privateKey);
            publicKey = labelKey.ExportPublicKeyMultibase();
        }
        finally
        {
            CryptographicOperations.ZeroMemory(privateKey);
        }

        JsonObject payload = new()
        {
            ["iss"] = reporter.Value,
            ["aud"] = legacy ? destination.Value : destination.Value + "#atproto_labeler",
            ["lxm"] = "com.atproto.moderation.createReport",
            ["iat"] = clock.GetUtcNow().ToUnixTimeSeconds(),
            ["exp"] = clock.GetUtcNow().AddMinutes(10).ToUnixTimeSeconds(),
            ["jti"] = tokenId
        };
        string unsigned = Encode(Encoding.UTF8.GetBytes("""{"typ":"JWT","alg":"ES256"}""")) + "." +
            Encode(Encoding.UTF8.GetBytes(payload.ToJsonString()));
        byte[] signature = key.SignData(
            Encoding.ASCII.GetBytes(unsigned), HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        string jwt = unsigned + "." + Encode(signature);
        JsonObject document = new()
        {
            ["id"] = reporter.Value,
            ["verificationMethod"] = new JsonArray(new JsonObject
            {
                ["id"] = reporter.Value + "#atproto",
                ["type"] = "Multikey",
                ["controller"] = reporter.Value,
                ["publicKeyMultibase"] = publicKey
            })
        };
        using HttpClient client = new(new DocumentHandler(document.ToJsonString()));
        return await ServiceAuthentication.VerifyModerationReportAsync(
            token: jwt, labeler: destination, httpClient: client, timeProvider: clock,
            cancellationToken: TestContext.Current.CancellationToken);
    }

    /// <summary>Encodes compact JWT segments without padding.</summary>
    private static string Encode(byte[] value) => Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    /// <summary>Serves only the fixture-owned reporter DID document without accessing the network.</summary>
    private sealed class DocumentHandler(string json) : HttpMessageHandler
    {
        /// <inheritdoc/>
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
    }
}
