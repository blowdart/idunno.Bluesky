// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Net;
using System.Text;
using System.Text.Json;

namespace idunno.Bluesky.Labeler.Test;

/// <summary>Mocks PLC-directory and PDS responses consumed by the production target resolver.</summary>
/// <remarks>
/// <para>The handler returns a DID document with a PDS service and echoes record identity from
/// <c>getRecord</c> query parameters, allowing service tests to exercise real SDK resolution without
/// contacting public infrastructure.</para>
/// </remarks>
internal sealed class TargetResolutionHandler : HttpMessageHandler
{
    internal const string SubjectDid = "did:plc:label-test-subject";
    internal const string RecordCid = "BAFYREIGH5FJR6AN3QUJVFBM6XMSO6JYHIDO2DGUIXAEQP3JTCQFT2GOFBA";

    internal bool Offline { get; set; }

    internal bool RecordNotFound { get; set; }

    internal string Cid { get; set; } = RecordCid;

    internal int Requests { get; private set; }

    /// <summary>Routes identity and record lookups to deterministic responses and rejects unexpected endpoints.</summary>
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Requests++;
        if (Offline)
        {
            throw new HttpRequestException("The test resolver is offline.");
        }

        if (request.RequestUri is null)
        {
            throw new InvalidOperationException("The resolver request has no URI.");
        }

        if (string.Equals(request.RequestUri.Host, "plc.directory", StringComparison.Ordinal))
        {
            return Task.FromResult(
                JsonResponse(
                    HttpStatusCode.OK,
                    $$"""
                {"id":"{{SubjectDid}}","alsoKnownAs":[],"verificationMethod":[],"service":[
                {"id":"#atproto_pds","type":"AtprotoPersonalDataServer","serviceEndpoint":"https://pds.example"}]}
                """));
        }

        if (string.Equals(request.RequestUri.Host, "pds.example", StringComparison.Ordinal) &&
            string.Equals(request.RequestUri.AbsolutePath, "/xrpc/com.atproto.repo.getRecord", StringComparison.Ordinal))
        {
            if (RecordNotFound)
            {
                return Task.FromResult(JsonResponse(HttpStatusCode.NotFound, """{"error":"RecordNotFound"}"""));
            }

            Dictionary<string, string> query = ParseQuery(request.RequestUri.Query);
            string repository = query["repo"];
            string collection = query["collection"];
            string recordKey = query["rkey"];
            return Task.FromResult(
                JsonResponse(
                    HttpStatusCode.OK,
                    $$$"""{"uri":"at://{{{repository}}}/{{{collection}}}/{{{recordKey}}}","cid":"{{{Cid}}}","value":{}}"""));
        }

        return Task.FromResult(JsonResponse(HttpStatusCode.NotFound, """{"error":"NotFound"}"""));
    }

    /// <summary>Decodes the resolver's request parameters so successful responses echo the requested record identity.</summary>
    private static Dictionary<string, string> ParseQuery(string query)
    {
        Dictionary<string, string> values = new(StringComparer.Ordinal);
        foreach (string pair in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            string[] parts = pair.Split('=', 2);
            values[WebUtility.UrlDecode(parts[0])] = parts.Length == 2 ? WebUtility.UrlDecode(parts[1]) : string.Empty;
        }

        return values;
    }

    /// <summary>Builds a JSON HTTP response consumed by the real SDK deserializer rather than bypassing it.</summary>
    private static HttpResponseMessage JsonResponse(HttpStatusCode statusCode, string json) =>
        new(statusCode)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
}
