// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.AtProto;

public partial class AtProtoAgent
{
    /// <summary>
    /// Gets a repository CAR file for a DID or handle.
    /// </summary>
    /// <param name="repo">The DID or handle of the repository to retrieve.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A result containing a stream for the CAR response. The stream owns the response and must be disposed.</returns>
    /// <exception cref="ArgumentException">The repository identifier cannot be resolved to a DID or personal data server.</exception>
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Usage",
        "RS0027",
        Justification = "The shorter overload preserves the existing optional-parameter API; the extended overload adds the lexicon option.")]
    public Task<AtProtoHttpResult<Stream>> GetRepo(
        AtIdentifier repo,
        CancellationToken cancellationToken = default)
    {
        return GetRepo(repo, since: null, cancellationToken);
    }

    /// <summary>
    /// Gets a repository CAR file or a CAR diff for a DID or handle.
    /// </summary>
    /// <param name="repo">The DID or handle of the repository to retrieve.</param>
    /// <param name="since">An optional repository revision identifier to download as a CAR diff.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A result containing a stream for the CAR response. The stream owns the response and must be disposed.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="repo"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The repository identifier cannot be resolved to a DID or personal data server.</exception>
    public async Task<AtProtoHttpResult<Stream>> GetRepo(
        AtIdentifier repo,
        TimestampIdentifier? since,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(repo);

        (Did did, Uri pds) = await ResolveSyncRepo(repo, cancellationToken).ConfigureAwait(false);

        return await AtProtoServer.GetRepo(
            did,
            since,
            pds,
            HttpClient,
            MaximumResponseSize,
            cancellationToken).ConfigureAwait(false);
    }
}
