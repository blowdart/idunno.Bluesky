// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text.Json;
using System.Text.Json.Nodes;

using idunno.AtProto;
using idunno.AtProto.Labels;

namespace idunno.Bluesky.Labeler;

/// <summary>
/// Builds public labeler DID-document additions and verifies their publication.
/// </summary>
public static class LabelerIdentityDocument
{
    private const string LabelKeyFragment = "atproto_label";
    private const string LabelerServiceFragment = "atproto_labeler";
    private const string MultikeyType = "Multikey";
    private const string LabelerServiceType = "AtprotoLabeler";

    /// <summary>
    /// Creates a merge-ready JSON snippet containing the label signing key and labeler service.
    /// </summary>
    /// <param name="did">The labeler's DID.</param>
    /// <param name="signingKey">The local label signing key.</param>
    /// <param name="labelerEndpoint">The public HTTPS labeler endpoint.</param>
    /// <returns>A JSON object containing only the public verification method and service entries.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="did"/>, <paramref name="signingKey"/>, or <paramref name="labelerEndpoint"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="did"/> is not a <c>did:web</c> identifier, or <paramref name="labelerEndpoint"/> is not an absolute HTTPS URI.</exception>
    /// <remarks>
    /// <para>This returns additions for an operator to merge into the existing identity document for <paramref name="did" />. It does not produce a replacement document and contains no private key material.</para>
    /// </remarks>
    public static string CreateDidWebSnippet(Did did, LabelSigningKey signingKey, Uri labelerEndpoint)
    {
        ArgumentNullException.ThrowIfNull(did);
        ArgumentNullException.ThrowIfNull(signingKey);
        return CreateDidWebSnippet(did, signingKey.ExportPublicKeyMultibase(), labelerEndpoint);
    }

    /// <summary>
    /// Creates a merge-ready JSON snippet from a public label signing key and service endpoint.
    /// </summary>
    /// <param name="did">The labeler's DID.</param>
    /// <param name="publicKeyMultibase">The public label signing key in multibase form.</param>
    /// <param name="labelerEndpoint">The public HTTPS labeler endpoint.</param>
    /// <returns>A JSON object containing only the public verification method and service entries.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="did"/>, <paramref name="publicKeyMultibase"/>, or <paramref name="labelerEndpoint"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="did"/> is not a <c>did:web</c> identifier, the public key is not a base58btc multibase value, or <paramref name="labelerEndpoint"/> is not an absolute HTTPS URI.</exception>
    /// <remarks>
    /// <para>This overload allows an operator tool to prepare identity publication from a remote public-key response without access to private signing material.</para>
    /// <para>This returns additions for an operator to merge into the existing identity document for <paramref name="did" />. It does not produce a replacement document and contains no private key material.</para>
    /// </remarks>
    public static string CreateDidWebSnippet(Did did, string publicKeyMultibase, Uri labelerEndpoint)
    {
        ArgumentNullException.ThrowIfNull(did);
        ArgumentNullException.ThrowIfNull(publicKeyMultibase);
        ValidateDidWeb(did);
        ValidatePublicKey(publicKeyMultibase);
        ValidateEndpoint(labelerEndpoint);

        JsonObject snippet = CreateSnippet(did, publicKeyMultibase, labelerEndpoint);
        return snippet.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }

    /// <summary>
    /// Creates a merge-ready JSON snippet containing only the labeler service entry.
    /// </summary>
    /// <param name="did">The labeler's DID.</param>
    /// <param name="labelerEndpoint">The public HTTPS labeler endpoint.</param>
    /// <returns>A JSON object containing only the public service entry.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="did"/> or <paramref name="labelerEndpoint"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="did"/> is not a <c>did:web</c> identifier, or <paramref name="labelerEndpoint"/> is not an absolute HTTPS URI.</exception>
    /// <remarks>
    /// <para>Use this when changing an endpoint. Merge or replace only the entry with the <c>#atproto_labeler</c> identifier; retain every unrelated property and entry in the identity document for <paramref name="did" />.</para>
    /// </remarks>
    public static string CreateDidWebServiceSnippet(Did did, Uri labelerEndpoint)
    {
        ArgumentNullException.ThrowIfNull(did);
        ValidateDidWeb(did);
        ValidateEndpoint(labelerEndpoint);

        JsonObject snippet = new()
        {
            ["service"] = new JsonArray(CreateService(did, labelerEndpoint))
        };
        return snippet.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }

    /// <summary>
    /// Verifies that the resolved document for <paramref name="did" /> publishes the expected label signing key and labeler endpoint.
    /// </summary>
    /// <param name="didDocument">The resolved identity document.</param>
    /// <param name="did">The expected labeler identifier.</param>
    /// <param name="signingKey">The local label signing key whose public key must be published.</param>
    /// <param name="labelerEndpoint">The expected public HTTPS labeler endpoint.</param>
    /// <exception cref="ArgumentNullException"><paramref name="didDocument"/>, <paramref name="did"/>, <paramref name="signingKey"/>, or <paramref name="labelerEndpoint"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="labelerEndpoint"/> is not an absolute HTTPS URI.</exception>
    /// <exception cref="InvalidDataException">The document for <paramref name="did" /> does not publish exactly the expected public key and endpoint.</exception>
    public static void VerifyPublication(
        DidDocument didDocument,
        Did did,
        LabelSigningKey signingKey,
        Uri labelerEndpoint)
    {
        ArgumentNullException.ThrowIfNull(didDocument);
        ArgumentNullException.ThrowIfNull(did);
        ArgumentNullException.ThrowIfNull(signingKey);
        ValidateEndpoint(labelerEndpoint);

        if (didDocument.Id != did)
        {
            throw new InvalidDataException("The resolved DID document belongs to a different identifier.");
        }

        if ((didDocument.VerificationMethods ?? []).Any(method => method is null || string.IsNullOrWhiteSpace(method.Id)) ||
            (didDocument.Services ?? []).Any(service => service is null || string.IsNullOrWhiteSpace(service.Id)))
        {
            throw new InvalidDataException("The DID document contains a null entry or an empty entry identifier.");
        }

        string expectedKey = signingKey.ExportPublicKeyMultibase();
        VerificationMethod[] keyEntries =
        [
            .. (didDocument.VerificationMethods ?? [])
                .Where(method => HasFragment(method.Id, did, LabelKeyFragment))
        ];
        if (keyEntries.Length != 1 ||
            keyEntries[0].Type != MultikeyType ||
            keyEntries[0].Controller != did ||
            keyEntries[0].PublicKeyMultibase != expectedKey)
        {
            throw new InvalidDataException("The DID document does not publish the expected #atproto_label verification method.");
        }

        DidDocService[] serviceEntries =
        [
            .. (didDocument.Services ?? [])
                .Where(service => HasFragment(service.Id, did, LabelerServiceFragment))
        ];
        if (serviceEntries.Length != 1 ||
            serviceEntries[0].Type != LabelerServiceType ||
            serviceEntries[0].ServiceEndpoint is not { IsAbsoluteUri: true } publishedEndpoint ||
            !string.IsNullOrEmpty(publishedEndpoint.UserInfo) ||
            !string.IsNullOrEmpty(publishedEndpoint.Fragment) ||
            publishedEndpoint != labelerEndpoint)
        {
            throw new InvalidDataException("The DID document does not publish the expected #atproto_labeler service.");
        }
    }

    private static JsonObject CreateSnippet(Did did, string publicKeyMultibase, Uri labelerEndpoint) =>
        new()
        {
            ["verificationMethod"] = new JsonArray(
                new JsonObject
                {
                    ["id"] = $"{did}#{LabelKeyFragment}",
                    ["type"] = MultikeyType,
                    ["controller"] = did.ToString(),
                    ["publicKeyMultibase"] = publicKeyMultibase
                }),
            ["service"] = new JsonArray(CreateService(did, labelerEndpoint))
        };

    private static JsonObject CreateService(Did did, Uri labelerEndpoint) =>
        new()
        {
            ["id"] = $"{did}#{LabelerServiceFragment}",
            ["type"] = LabelerServiceType,
            ["serviceEndpoint"] = labelerEndpoint.AbsoluteUri
        };

    private static bool HasFragment(string identifier, Did did, string fragment) =>
        identifier.Equals($"#{fragment}", StringComparison.Ordinal) ||
        identifier.Equals($"{did}#{fragment}", StringComparison.Ordinal);

    private static void ValidateDidWeb(Did did)
    {
        if (!did.ToString().StartsWith("did:web:", StringComparison.Ordinal))
        {
            throw new ArgumentException("A did:web DID is required.", nameof(did));
        }
    }

    internal static void ValidateEndpoint(Uri labelerEndpoint)
    {
        ArgumentNullException.ThrowIfNull(labelerEndpoint);
        if (!labelerEndpoint.IsAbsoluteUri ||
            labelerEndpoint.Scheme != Uri.UriSchemeHttps ||
            !string.IsNullOrEmpty(labelerEndpoint.UserInfo) ||
            labelerEndpoint.AbsolutePath is not ("" or "/") ||
            !string.IsNullOrEmpty(labelerEndpoint.Query) ||
            !string.IsNullOrEmpty(labelerEndpoint.Fragment))
        {
            throw new ArgumentException(
                "The labeler endpoint must be an HTTPS origin without credentials, a path, a query, or a fragment.",
                nameof(labelerEndpoint));
        }
    }

    private static void ValidatePublicKey(string publicKeyMultibase)
    {
        LabelSignatureVerifier.ValidatePublicKey(publicKeyMultibase);
    }
}
