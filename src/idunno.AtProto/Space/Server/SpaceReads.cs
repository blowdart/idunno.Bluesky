// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

using idunno.AtProto.Repo;
using idunno.AtProto.Space;
using idunno.AtProto.Space.Models;

using Microsoft.Extensions.Logging;

namespace idunno.AtProto;

public static partial class AtProtoServer
{
    /// <summary>Reads a space record and verifies its response MAC and original record CID.</summary>
    /// <param name="uri">The full space-record URI.</param>
    /// <param name="credential">The live credential for <paramref name="uri"/>'s space.</param>
    /// <param name="service">The record author's repository host.</param>
    /// <param name="httpClient">The HTTP client used for the request and author DID resolution.</param>
    /// <param name="loggerFactory">The optional logger factory.</param>
    /// <param name="maximumResponseSize">The maximum buffered response size.</param>
    /// <param name="cancellationToken">A token that can cancel the operation.</param>
    /// <returns>The authenticated record and HTTP response metadata.</returns>
    /// <exception cref="ArgumentNullException">A required argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The reference is not a record or the credential belongs to another space.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The maximum buffered response size is not positive.</exception>
    /// <exception cref="InvalidOperationException">The credential is expired.</exception>
    /// <exception cref="ObjectDisposedException">The credential has been disposed.</exception>
    /// <exception cref="InvalidDataException">The author's KEM key, response MAC, record identity or CID cannot be verified.</exception>
    /// <remarks>
    /// <para>The endpoint may return <c>InvalidSpaceMac</c>, <c>RecordNotFound</c>, <c>SpaceNotFound</c>,
    /// <c>RepoNotFound</c>, <c>RepoTakendown</c>, <c>RepoSuspended</c> or <c>RepoDeactivated</c>.</para>
    /// <para>A fresh ephemeral P-256 key is used for every request. The client must protect DID resolution against SSRF
    /// and disable automatic redirects. Invalid authentication throws rather than returning unverified content.
    /// A MAC is not transferable proof of authorship and does not encrypt the record.</para>
    /// </remarks>
    [Experimental("BSKYSpaces", UrlFormat = "https://bluesky.idunno.dev/docs/spaces.html")]
    public static Task<AtProtoHttpResult<AtProtoRepositoryRecord>> GetSpaceRecord(
        SpaceAtUri uri,
        SpaceCredential credential,
        Uri service,
        HttpClient httpClient,
        ILoggerFactory? loggerFactory = null,
        int maximumResponseSize = AtProtoHttpClient.DefaultMaximumResponseSize,
        CancellationToken cancellationToken = default) =>
        GetSpaceRecordCore(
            uri: uri,
            credential: credential,
            service: service,
            httpClient: httpClient,
            loggerFactory: loggerFactory,
            maximumResponseSize: maximumResponseSize,
            plcDirectory: null,
            cancellationToken: cancellationToken);

    /// <summary>Verifies a record using the agent's configured PLC directory for the author's KEM key.</summary>
    [Experimental("BSKYSpaces", UrlFormat = "https://bluesky.idunno.dev/docs/spaces.html")]
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "The response is registered in SpaceSourceGenerationContext.")]
    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = "The response is registered in SpaceSourceGenerationContext.")]
    internal static async Task<AtProtoHttpResult<AtProtoRepositoryRecord>> GetSpaceRecordCore(
        SpaceAtUri uri,
        SpaceCredential credential,
        Uri service,
        HttpClient httpClient,
        ILoggerFactory? loggerFactory,
        int maximumResponseSize,
        Uri? plcDirectory,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(uri);
        ArgumentNullException.ThrowIfNull(credential);
        ArgumentNullException.ThrowIfNull(service);
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumResponseSize);
        if (!uri.IsRecord)
        {
            throw new ArgumentException("A full space-record URI is required.", nameof(uri));
        }

        SpaceAtUri space = new(uri.SpaceDid, uri.SpaceType, uri.SpaceKey);
        credential.EnsureUsable(space);
        DidDocument? document = await IdentityResolution.ResolveDidDocumentAsync(
            did: uri.AuthorDid,
            plcDirectory: plcDirectory,
            httpClient: httpClient,
            cancellationToken: cancellationToken).ConfigureAwait(false);
        SigningKey key = SigningKeyVerifier.GetKey(document, uri.AuthorDid, "atproto_kem");
        if (document?.VerificationMethods?.Single(method => method.Id == $"{uri.AuthorDid}#atproto_kem").Type != "Multikey")
        {
            throw new InvalidDataException("The author's KEM verification method must be a Multikey.");
        }

        using ECDiffieHellman recipient = key.CreateKeyAgreement();
        using ECDiffieHellman ephemeral = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
        Did keyShare = SpaceCrypto.ToDidKey(ephemeral.ExportParameters(false));
        byte[] secret = SpaceCrypto.Export(
            sender: ephemeral,
            recipient: recipient,
            info: "atproto-mac"u8,
            exporterContext: [],
            length: 32);
        try
        {
            AtProtoHttpClient<SpaceRecordResponse> client = CreateClient<SpaceRecordResponse>(null, loggerFactory, maximumResponseSize);
            client.OnSendingRequest = (request, _) =>
            {
                credential.SignRequest(request, space, uri.AuthorDid);
                request.Headers.Add("Accept-Signature", SpaceResponseMac.Request(keyShare));
                return Task.CompletedTask;
            };
            client.OnResponseReceived = async (response, token) =>
            {
                if (response.IsSuccessStatusCode)
                {
                    byte[] bytes = await response.Content.ReadAsByteArrayAsync(token).ConfigureAwait(false);
                    SpaceRecordResponse record;
                    try
                    {
                        record = JsonSerializer.Deserialize(bytes, SpaceSourceGenerationContext.Default.SpaceRecordResponse) ??
                            throw new InvalidDataException("The response contains no record.");
                    }
                    catch (JsonException exception)
                    {
                        throw new InvalidDataException("The response contains an invalid space record.", exception);
                    }

                    if (record.Cid is null)
                    {
                        throw new InvalidDataException("The response contains no record CID.");
                    }

                    SpaceResponseMac.Verify(
                        headers: response.Headers,
                        keyShare: keyShare,
                        secret: secret,
                        uri: uri,
                        cid: record.Cid);
                    ValidateSpaceRecord(record, uri);
                }
            };
            AtProtoHttpResult<SpaceRecordResponse> response = await client.Get(
                service: service,
                endpoint: $"/xrpc/com.atproto.space.getRecord?space={Uri.EscapeDataString(space.ToString())}&repo={Uri.EscapeDataString(uri.AuthorDid.Value)}&collection={Uri.EscapeDataString(uri.Collection!.ToString())}&rkey={Uri.EscapeDataString(uri.RecordKey!.Value)}",
                credentials: null,
                httpClient: httpClient,
                jsonSerializerOptions: AtProtoJsonSerializerOptions,
                cancellationToken: cancellationToken).ConfigureAwait(false);
            return new AtProtoHttpResult<AtProtoRepositoryRecord>(
                result: response.Succeeded ? new AtProtoRepositoryRecord(uri, response.Result.Cid, JsonObject.Create(response.Result.Value)) : null,
                statusCode: response.StatusCode,
                httpResponseHeaders: response.HttpResponseHeaders,
                atErrorDetail: response.AtErrorDetail,
                rateLimit: response.RateLimit);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(secret);
        }
    }

    /// <summary>Lists a space repository's records using a signed space credential.</summary>
    /// <param name="space">The space reference, not a record URI.</param>
    /// <param name="repo">The record author's DID.</param>
    /// <param name="credential">The live credential for this space.</param>
    /// <param name="service">The author's repository host.</param>
    /// <param name="httpClient">The HTTP client used for the request.</param>
    /// <param name="collection">The optional collection filter.</param>
    /// <param name="limit">The maximum page size, between 1 and 1000. The server default is 50.</param>
    /// <param name="cursor">The optional pagination cursor.</param>
    /// <param name="excludeValues"><see langword="true"/> to omit record values; otherwise, <see langword="false"/>.</param>
    /// <param name="loggerFactory">The optional logger factory.</param>
    /// <param name="maximumResponseSize">The maximum buffered response size.</param>
    /// <param name="cancellationToken">A token that can cancel the operation.</param>
    /// <returns>The page of records and HTTP response metadata.</returns>
    /// <exception cref="ArgumentNullException">A required argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The URI addresses a record or the credential belongs to another space.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The requested limit is outside 1 through 1000.</exception>
    /// <exception cref="InvalidOperationException">The credential is expired.</exception>
    /// <exception cref="ObjectDisposedException">The credential has been disposed.</exception>
    /// <exception cref="InvalidDataException">A returned record identity, value or CID is invalid.</exception>
    /// <remarks>
    /// <para>The endpoint may return <c>SpaceNotFound</c>, <c>RepoNotFound</c>, <c>RepoTakendown</c>,
    /// <c>RepoSuspended</c>, <c>RepoDeactivated</c> or <c>InvalidCursor</c>.</para>
    /// <para>The alpha protocol defines no response MAC for this endpoint. Returned values are checked against their
    /// CIDs, but neither those CIDs, the listing's completeness nor record authorship is authenticated.
    /// Use <see cref="GetSpaceRecord"/> to authenticate individual records. Disable automatic redirects in the client.</para>
    /// </remarks>
    [Experimental("BSKYSpaces", UrlFormat = "https://bluesky.idunno.dev/docs/spaces.html")]
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "The response is registered in SpaceSourceGenerationContext.")]
    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = "The response is registered in SpaceSourceGenerationContext.")]
    public static async Task<AtProtoHttpResult<PagedReadOnlyCollection<AtProtoRepositoryRecord>>> ListSpaceRecords(
        SpaceAtUri space,
        Did repo,
        SpaceCredential credential,
        Uri service,
        HttpClient httpClient,
        Nsid? collection = null,
        int? limit = null,
        string? cursor = null,
        bool excludeValues = false,
        ILoggerFactory? loggerFactory = null,
        int maximumResponseSize = AtProtoHttpClient.DefaultMaximumResponseSize,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(space);
        ArgumentNullException.ThrowIfNull(repo);
        ArgumentNullException.ThrowIfNull(credential);
        ArgumentNullException.ThrowIfNull(service);
        ArgumentNullException.ThrowIfNull(httpClient);
        ValidateSpaceReference(space);
        credential.EnsureUsable(space);
        if (limit is < 1 or > 1000)
        {
            throw new ArgumentOutOfRangeException(nameof(limit), limit, "limit must be between 1 and 1000.");
        }

        StringBuilder endpoint = new($"/xrpc/com.atproto.space.listRecords?space={Uri.EscapeDataString(space.ToString())}&repo={Uri.EscapeDataString(repo.Value)}");
        if (collection is not null)
        {
            endpoint.Append("&collection=").Append(Uri.EscapeDataString(collection.ToString()));
        }

        if (limit is not null)
        {
            endpoint.Append("&limit=").Append(limit.Value.ToString(CultureInfo.InvariantCulture));
        }

        if (cursor is not null)
        {
            endpoint.Append("&cursor=").Append(Uri.EscapeDataString(cursor));
        }

        endpoint.Append("&excludeValues=").Append(excludeValues ? "true" : "false");
        AtProtoHttpClient<ListSpaceRecordsResponse> client = CreateClient<ListSpaceRecordsResponse>(null, loggerFactory, maximumResponseSize);
        client.OnSendingRequest = (request, _) =>
        {
            credential.SignRequest(request, space, repo);
            return Task.CompletedTask;
        };
        AtProtoHttpResult<ListSpaceRecordsResponse> response = await client.Get(
            service: service,
            endpoint: endpoint.ToString(),
            credentials: null,
            httpClient: httpClient,
            jsonSerializerOptions: AtProtoJsonSerializerOptions,
            cancellationToken: cancellationToken).ConfigureAwait(false);
        List<AtProtoRepositoryRecord>? records = null;
        if (response.Succeeded)
        {
            if (response.Result.Records is null || response.Result.Records.Count > (limit ?? 50))
            {
                throw new InvalidDataException("The record listing is missing or exceeds the requested page size.");
            }

            records = [];
            HashSet<string> paths = [with(StringComparer.Ordinal)];
            foreach (SpaceRecordListEntry entry in response.Result.Records)
            {
                if (entry is null || entry.Collection is null || entry.Rkey is null || entry.Cid is null ||
                    collection is not null && entry.Collection != collection || !paths.Add($"{entry.Collection}/{entry.Rkey}"))
                {
                    throw new InvalidDataException("The record listing contains a missing, duplicate or unexpected identity.");
                }

                SpaceAtUri uri = new(
                    spaceDid: space.SpaceDid,
                    spaceType: space.SpaceType,
                    spaceKey: space.SpaceKey,
                    authorDid: repo,
                    collection: entry.Collection,
                    recordKey: entry.Rkey);
                JsonObject? value = null;
                if (entry.Value is JsonElement recordValue)
                {
                    ValidateSpaceRecord(new SpaceRecordResponse(uri, entry.Cid, recordValue), uri);
                    value = JsonObject.Create(recordValue);
                }
                else if (!excludeValues)
                {
                    throw new InvalidDataException("The record listing omitted a requested value.");
                }

                records.Add(new AtProtoRepositoryRecord(uri, entry.Cid, value));
            }
        }

        return new AtProtoHttpResult<PagedReadOnlyCollection<AtProtoRepositoryRecord>>(
            result: records is null ? null : new PagedReadOnlyCollection<AtProtoRepositoryRecord>(records, response.Result!.Cursor),
            statusCode: response.StatusCode,
            httpResponseHeaders: response.HttpResponseHeaders,
            atErrorDetail: response.AtErrorDetail,
            rateLimit: response.RateLimit);
    }

    /// <summary>Checks a record's original identity and canonical value before it can leave the read path.</summary>
    [Experimental("BSKYSpaces", UrlFormat = "https://bluesky.idunno.dev/docs/spaces.html")]
    private static void ValidateSpaceRecord(SpaceRecordResponse record, SpaceAtUri expectedUri)
    {
        if (record.Uri != expectedUri || record.Cid is null || record.Value.ValueKind != JsonValueKind.Object ||
            Cid.FromDagCbor(DagCbor.FromJsonElement(record.Value)) != record.Cid)
        {
            throw new InvalidDataException("The record identity or original value does not match its CID.");
        }
    }
}
