// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.AtProto;

public static partial class AtProtoServer
{
    /// <summary>
    /// Gets the data blocks needed to prove the existence, or non-existence, of a record in the current version of a repository,
    /// returned as a CAR file.
    /// </summary>
    /// <param name="did">The DID of the repository.</param>
    /// <param name="collection">The NSID of the collection containing the record.</param>
    /// <param name="rKey">The record key of the record.</param>
    /// <param name="service">The personal data server hosting the repository.</param>
    /// <param name="httpClient">The <see cref="HttpClient"/> to use for the request.</param>
    /// <param name="maximumResponseSize">The maximum number of bytes to read from an error response.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A result containing a stream for the CAR response. The stream owns the response and must be disposed.</returns>
    /// <exception cref="HttpRequestException">Thrown when the request failed due to an underlying issue such as network connectivity, DNS failure, server certificate validation, or timeout.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the request URI is invalid.</exception>
    /// <exception cref="TaskCanceledException">Thrown when the request was canceled.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="did"/>, <paramref name="collection"/>, <paramref name="rKey"/>, <paramref name="service"/>, or <paramref name="httpClient"/> is <see langword="null" />.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="maximumResponseSize"/> is less than 1.</exception>
    /// <remarks>
    /// <para>This calls <c>com.atproto.sync.getRecord</c>. To retrieve a record as JSON, via <c>com.atproto.repo.getRecord</c>, use <c>GetRecord</c>.</para>
    /// <para>The CAR content is streamed rather than buffered, so its size is not limited by <paramref name="maximumResponseSize"/>.</para>
    /// <para>The server may return the following errors: <c>RecordNotFound</c>, <c>RepoNotFound</c>, <c>RepoTakendown</c>,
    /// <c>RepoSuspended</c> and <c>RepoDeactivated</c>.</para>
    /// </remarks>
    public static async Task<AtProtoHttpResult<Stream>> GetSyncRecord(
        Did did,
        Nsid collection,
        RecordKey rKey,
        Uri service,
        HttpClient httpClient,
        int maximumResponseSize = AtProtoHttpClient.DefaultMaximumResponseSize,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(did);
        ArgumentNullException.ThrowIfNull(collection);
        ArgumentNullException.ThrowIfNull(rKey);
        ArgumentNullException.ThrowIfNull(service);
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentOutOfRangeException.ThrowIfLessThan(maximumResponseSize, 1);

        Uri requestUri = new(
            service,
            $"/xrpc/com.atproto.sync.getRecord?did={Uri.EscapeDataString(did.Value)}&collection={Uri.EscapeDataString(collection.ToString())}&rkey={Uri.EscapeDataString(rKey.Value)}");

        return await GetStreamingResponse(
            requestUri,
            "application/vnd.ipld.car",
            httpClient,
            maximumResponseSize,
            cancellationToken).ConfigureAwait(false);
    }
}
