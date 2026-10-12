// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.AtProto;

public static partial class AtProtoServer
{
    /// <summary>
    /// Gets a repository CAR file from a personal data server.
    /// </summary>
    /// <param name="repo">The DID of the repository to retrieve.</param>
    /// <param name="service">The personal data server hosting the repository.</param>
    /// <param name="httpClient">The <see cref="HttpClient"/> to use for the request.</param>
    /// <param name="maximumResponseSize">The maximum number of bytes to read from an error response.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A result containing a stream for the CAR response. The stream owns the response and must be disposed.</returns>
    /// <exception cref="HttpRequestException">Thrown when the request failed due to an underlying issue such as network connectivity, DNS failure, server certificate validation, or timeout.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the request URI is invalid.</exception>
    /// <exception cref="TaskCanceledException">Thrown when the request was canceled.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="repo"/>, <paramref name="service"/>, or <paramref name="httpClient"/> is <see langword="null" />.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="maximumResponseSize"/> is less than 1.</exception>
    /// <exception cref="Exception">Thrown when an unexpected error occurs.</exception>
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Usage",
        "RS0027",
        Justification = "The shorter overload preserves the existing optional-parameter API; the extended overload adds the lexicon option.")]
    public static Task<AtProtoHttpResult<Stream>> GetRepo(
        Did repo,
        Uri service,
        HttpClient httpClient,
        int maximumResponseSize = AtProtoHttpClient.DefaultMaximumResponseSize,
        CancellationToken cancellationToken = default)
    {
        return GetRepo(repo, since: null, service, httpClient, maximumResponseSize, cancellationToken);
    }

    /// <summary>
    /// Gets a repository CAR file or a CAR diff from a personal data server.
    /// </summary>
    /// <param name="repo">The DID of the repository to retrieve.</param>
    /// <param name="since">An optional repository revision identifier to download as a CAR diff.</param>
    /// <param name="service">The personal data server hosting the repository.</param>
    /// <param name="httpClient">The <see cref="HttpClient"/> to use for the request.</param>
    /// <param name="maximumResponseSize">The maximum number of bytes to read from an error response.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A result containing a stream for the CAR response. The stream owns the response and must be disposed.</returns>
    /// <exception cref="HttpRequestException">The request failed due to an underlying network, certificate or timeout issue.</exception>
    /// <exception cref="InvalidOperationException">The request URI is invalid.</exception>
    /// <exception cref="TaskCanceledException">The request was canceled.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="repo"/>, <paramref name="service"/> or <paramref name="httpClient"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maximumResponseSize"/> is less than 1.</exception>
    /// <exception cref="Exception">An unexpected error occurs.</exception>
    public static async Task<AtProtoHttpResult<Stream>> GetRepo(
        Did repo,
        TimestampIdentifier? since,
        Uri service,
        HttpClient httpClient,
        int maximumResponseSize,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(repo);
        ArgumentNullException.ThrowIfNull(service);
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentOutOfRangeException.ThrowIfLessThan(maximumResponseSize, 1);

        Uri requestUri = new(
            service,
            $"/xrpc/com.atproto.sync.getRepo?did={Uri.EscapeDataString(repo.Value)}{(since is null ? string.Empty : $"&since={Uri.EscapeDataString(since.Value)}")}");

        return await GetStreamingResponse(
            requestUri,
            "application/vnd.ipld.car",
            httpClient,
            maximumResponseSize,
            cancellationToken).ConfigureAwait(false);
    }
}
