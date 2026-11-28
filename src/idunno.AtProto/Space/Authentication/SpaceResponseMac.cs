// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;

#pragma warning disable IDE0130 // Namespace does not match folder structure.
namespace idunno.AtProto.Space;
#pragma warning restore IDE0130 // Namespace does not match folder structure.

/// <summary>Verifies the RFC 9421 record-MAC profile without assuming an intermediary preserved signature labels.</summary>
[Experimental("BSKYSpaces", UrlFormat = "https://bluesky.idunno.dev/docs/spaces.html")]
internal static class SpaceResponseMac
{
    /// <summary>Requests authentication of a record with a freshly encapsulated P-256 key.</summary>
    internal static string Request(Did keyShare) =>
        $"mac=(\"atproto-space-record\";sf);keyid=\"{keyShare}\";tag=\"atproto-space-record\"";

    /// <summary>Verifies the response MAC and binds its authenticated URI and CID to the requested record.</summary>
    internal static void Verify(HttpResponseHeaders headers, Did keyShare, byte[] secret, SpaceAtUri uri, Cid cid)
    {
        Dictionary<string, SpaceStructuredFields.Field> inputs = SpaceStructuredFields.Parse(Header(headers, "Signature-Input"));
        Dictionary<string, SpaceStructuredFields.Field> signatures = SpaceStructuredFields.Parse(Header(headers, "Signature"));
        KeyValuePair<string, SpaceStructuredFields.Field>[] tagged = [.. inputs.Where(pair =>
            pair.Value.Items is not null &&
            pair.Value.Parameters.TryGetValue("tag", out SpaceStructuredFields.Bare? tag) &&
            tag.IsString && tag.Text == "atproto-space-record")];
        if (tagged.Length != 1)
        {
            throw new InvalidDataException("The response must have exactly one record-MAC signature.");
        }

        (string label, SpaceStructuredFields.Field input) = tagged[0];
        List<SpaceStructuredFields.Field> components = input.Items!;
        if (components.Count != 1 || components[0].Value is not { IsString: true, Text: "atproto-space-record" } ||
            components[0].Parameters.Count != 1 ||
            !components[0].Parameters.TryGetValue("sf", out SpaceStructuredFields.Bare? structured) || structured.Canonical != "?1" ||
            input.Parameters.TryGetValue("keyid", out SpaceStructuredFields.Bare? key) && (!key.IsString || key.Text != keyShare.Value) ||
            input.Parameters.TryGetValue("alg", out SpaceStructuredFields.Bare? algorithm) && (!algorithm.IsString || algorithm.Text != "hmac-sha256") ||
            !signatures.TryGetValue(label, out SpaceStructuredFields.Field? signature) ||
            signature.Items is not null || signature.Parameters.Count != 0 || signature.Value?.Bytes is not byte[] mac)
        {
            throw new InvalidDataException("The response MAC has invalid covered components, parameters or signature bytes.");
        }

        Dictionary<string, SpaceStructuredFields.Field> metadata = SpaceStructuredFields.Parse(Header(headers, "Atproto-Space-Record"));
        string signatureBase = "\"atproto-space-record\";sf: " + SpaceStructuredFields.Serialize(metadata) +
            "\n\"@signature-params\": " + input.SerializeMember();
        byte[] expected = HMACSHA256.HashData(secret, Encoding.ASCII.GetBytes(signatureBase));
        if (!CryptographicOperations.FixedTimeEquals(expected, mac) ||
            !IsString(metadata, "uri", uri.ToString()) || !IsString(metadata, "cid", cid.Value))
        {
            throw new InvalidDataException("The record or its response MAC could not be authenticated.");
        }
    }

    /// <summary>Checks a required, parameter-free structured string without coercion.</summary>
    private static bool IsString(Dictionary<string, SpaceStructuredFields.Field> fields, string name, string value) =>
        fields.TryGetValue(name, out SpaceStructuredFields.Field? field) &&
        field.Items is null && field.Parameters.Count == 0 && field.Value is { IsString: true } text && text.Text == value;

    /// <summary>Combines HTTP field lines as a single structured dictionary.</summary>
    private static string Header(HttpResponseHeaders headers, string name)
    {
        if (!headers.TryGetValues(name, out IEnumerable<string>? values))
        {
            throw new InvalidDataException("The response is missing its authentication headers.");
        }

        return string.Join(", ", values);
    }
}
