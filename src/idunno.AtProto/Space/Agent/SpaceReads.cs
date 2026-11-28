// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;

using idunno.AtProto.Repo;
using idunno.AtProto.Space;

namespace idunno.AtProto;

public partial class AtProtoAgent
{
    /// <summary>Discovers the record host and verifies a space record's response MAC and original CID.</summary>
    /// <param name="uri">The full space-record URI.</param>
    /// <param name="credential">The live credential for <paramref name="uri"/>'s space.</param>
    /// <param name="cancellationToken">A token that can cancel the operation.</param>
    /// <returns>The authenticated record and HTTP response metadata.</returns>
    /// <exception cref="ArgumentNullException">A required argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The reference does not address a record, its host cannot be resolved, or the credential has another scope.</exception>
    /// <exception cref="InvalidOperationException">The credential has expired.</exception>
    /// <exception cref="ObjectDisposedException">The credential has been disposed.</exception>
    /// <exception cref="InvalidDataException">The response MAC, author KEM key, record identity or CID is invalid.</exception>
    /// <remarks>
    /// <para>The endpoint may return <c>InvalidSpaceMac</c>, <c>RecordNotFound</c>, <c>SpaceNotFound</c>,
    /// <c>RepoNotFound</c>, <c>RepoTakendown</c>, <c>RepoSuspended</c> or <c>RepoDeactivated</c>.</para>
    /// <para>No account session is required or forwarded. Every request uses a fresh ephemeral P-256 key.
    /// MAC verification does not encrypt records or establish transferable proof of authorship.</para>
    /// </remarks>
    [Experimental("BSKYSpaces", UrlFormat = "https://bluesky.idunno.dev/docs/spaces.html")]
    public async Task<AtProtoHttpResult<AtProtoRepositoryRecord>> GetSpaceRecord(
        SpaceAtUri uri,
        SpaceCredential credential,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(uri);
        ArgumentNullException.ThrowIfNull(credential);
        if (!uri.IsRecord)
        {
            throw new ArgumentException("A full space-record URI is required.", nameof(uri));
        }

        credential.EnsureUsable(new SpaceAtUri(uri.SpaceDid, uri.SpaceType, uri.SpaceKey));
        (_, Uri service) = await ResolveSyncRepo(uri.AuthorDid, cancellationToken).ConfigureAwait(false);
        return await AtProtoServer.GetSpaceRecordCore(
            uri: uri,
            credential: credential,
            service: service,
            httpClient: HttpClient,
            loggerFactory: LoggerFactory,
            maximumResponseSize: MaximumResponseSize,
            plcDirectory: Options?.PlcDirectoryServer,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Lists records in a space repository using a signed space credential.</summary>
    /// <param name="space">The space reference, not a record URI.</param>
    /// <param name="repo">The author's DID or handle, resolved to a DID and repository host.</param>
    /// <param name="credential">The live credential for this space.</param>
    /// <param name="collection">The optional collection filter.</param>
    /// <param name="limit">The maximum page size, between 1 and 1000. The server default is 50.</param>
    /// <param name="cursor">The optional pagination cursor.</param>
    /// <param name="excludeValues"><see langword="true"/> to omit record values; otherwise, <see langword="false"/>.</param>
    /// <param name="cancellationToken">A token that can cancel the operation.</param>
    /// <returns>The page of records and HTTP response metadata.</returns>
    /// <exception cref="ArgumentNullException">A required argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The URI addresses a record, the repository cannot be resolved, or the credential has another scope.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The limit is outside 1 through 1000.</exception>
    /// <exception cref="InvalidOperationException">The credential has expired.</exception>
    /// <exception cref="ObjectDisposedException">The credential has been disposed.</exception>
    /// <exception cref="InvalidDataException">The returned listing has an invalid identity, value or CID.</exception>
    /// <remarks>
    /// <para>The endpoint may return <c>SpaceNotFound</c>, <c>RepoNotFound</c>, <c>RepoTakendown</c>,
    /// <c>RepoSuspended</c>, <c>RepoDeactivated</c> or <c>InvalidCursor</c>.</para>
    /// <para>The alpha endpoint has no response-MAC profile. CID checking detects inconsistent values, but does not
    /// authenticate the listing, its completeness or authorship. Authenticate individual records with
    /// <see cref="GetSpaceRecord"/>. No account session is required or forwarded.</para>
    /// </remarks>
    [Experimental("BSKYSpaces", UrlFormat = "https://bluesky.idunno.dev/docs/spaces.html")]
    public async Task<AtProtoHttpResult<PagedReadOnlyCollection<AtProtoRepositoryRecord>>> ListSpaceRecords(
        SpaceAtUri space,
        AtIdentifier repo,
        SpaceCredential credential,
        Nsid? collection = null,
        int? limit = null,
        string? cursor = null,
        bool excludeValues = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(space);
        ArgumentNullException.ThrowIfNull(repo);
        ArgumentNullException.ThrowIfNull(credential);
        if (space.IsRecord)
        {
            throw new ArgumentException("A space reference is required.", nameof(space));
        }

        if (limit is < 1 or > 1000)
        {
            throw new ArgumentOutOfRangeException(nameof(limit), limit, "limit must be between 1 and 1000.");
        }

        credential.EnsureUsable(space);
        (Did did, Uri service) = await ResolveSyncRepo(repo, cancellationToken).ConfigureAwait(false);
        return await AtProtoServer.ListSpaceRecords(
            space: space,
            repo: did,
            credential: credential,
            service: service,
            httpClient: HttpClient,
            collection: collection,
            limit: limit,
            cursor: cursor,
            excludeValues: excludeValues,
            loggerFactory: LoggerFactory,
            maximumResponseSize: MaximumResponseSize,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}
