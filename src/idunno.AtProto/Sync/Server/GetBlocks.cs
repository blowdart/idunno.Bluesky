// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Text;

namespace idunno.AtProto;

public static partial class AtProtoServer
{
    /// <summary>
    /// Gets data blocks, by their <see cref="Cid"/>s, from a repository on a personal data server, returned as a CAR file.
    /// </summary>
    /// <param name="did">The DID of the repository containing the blocks.</param>
    /// <param name="cids">The CIDs of the blocks to retrieve.</param>
    /// <param name="service">The personal data server hosting the repository.</param>
    /// <param name="httpClient">The <see cref="HttpClient"/> to use for the request.</param>
    /// <param name="maximumResponseSize">The maximum number of bytes to read from an error response.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A result containing a stream for the CAR response. The stream owns the response and must be disposed.</returns>
    /// <exception cref="HttpRequestException">Thrown when the request failed due to an underlying issue such as network connectivity, DNS failure, server certificate validation, or timeout.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the request URI is invalid.</exception>
    /// <exception cref="TaskCanceledException">Thrown when the request was canceled.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="did"/>, <paramref name="cids"/>, <paramref name="service"/>, or <paramref name="httpClient"/> is <see langword="null" />.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="cids"/> is empty or contains a <see langword="null"/> entry.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="maximumResponseSize"/> is less than 1.</exception>
    /// <remarks>
    /// <para>The CAR content is streamed rather than buffered, so its size is not limited by <paramref name="maximumResponseSize"/>.</para>
    /// <para>The returned CAR has no meaningful root, and its blocks are not verified against the requested <paramref name="cids"/>.</para>
    /// </remarks>
    public static async Task<AtProtoHttpResult<Stream>> GetBlocks(
        Did did,
        IEnumerable<Cid> cids,
        Uri service,
        HttpClient httpClient,
        int maximumResponseSize = AtProtoHttpClient.DefaultMaximumResponseSize,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(did);
        ArgumentNullException.ThrowIfNull(cids);
        ArgumentNullException.ThrowIfNull(service);
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentOutOfRangeException.ThrowIfLessThan(maximumResponseSize, 1);

        List<Cid> cidList = [.. cids];
        if (cidList.Count == 0)
        {
            throw new ArgumentException("At least one CID must be specified.", nameof(cids));
        }

        StringBuilder queryBuilder = new($"/xrpc/com.atproto.sync.getBlocks?did={Uri.EscapeDataString(did.Value)}");
        foreach (Cid cid in cidList)
        {
            if (cid is null)
            {
                throw new ArgumentException("CIDs cannot contain a null entry.", nameof(cids));
            }

            queryBuilder.Append("&cids=").Append(Uri.EscapeDataString(cid.ToString()));
        }

        Uri requestUri = new(service, queryBuilder.ToString());

        return await GetStreamingResponse(
            requestUri,
            "application/vnd.ipld.car",
            httpClient,
            maximumResponseSize,
            cancellationToken).ConfigureAwait(false);
    }
}
