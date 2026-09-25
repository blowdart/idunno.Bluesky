// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Net;

using idunno.AtProto;
using idunno.AtProto.Repo;
using idunno.Bluesky.Graph;

namespace idunno.Bluesky;

public partial class BlueskyAgent
{
    /// <summary>
    /// Deletes the list entry referred to by the <paramref name="uri"/>.
    /// </summary>
    /// <param name="uri">The <see cref="AtUri"/> of the record to delete.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="uri"/> or its collection property is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="uri"/> does not point to the list item collection.</exception>
    /// <exception cref="AuthenticationRequiredException">Thrown when the current agent is not authenticated.</exception>
    /// <remarks>
    ///     <para>To get a <see cref="AtUri"/> for a particular subject in a list call
    ///           <see cref="GetList(AtUri, int?, string?, IEnumerable{Did}?, CancellationToken)"/>, then, while paging through the results
    ///           search the subject <see cref="Did"/> or <see cref="Handle"/>.
    ///     <example>
    ///      <code>var listEntry = listEntriesResult.Result.FirstOrDefault(listEntry => listEntry.Subject.Handle == "blowdart.me")</code>
    ///      </example>
    ///     </para>
    /// </remarks>
    [SuppressMessage("ApiDesign", "RS0026:Do not add multiple public overloads with optional parameters", Justification = "Pre-existing overload set. The return type changed in this release, which makes the analyzer treat these as newly added overloads.")]
    public async Task<AtProtoHttpResult<DeleteResult>> DeleteFromList(
        AtUri uri,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(uri);
        ArgumentNullException.ThrowIfNull(uri.Collection);
        ArgumentOutOfRangeException.ThrowIfNotEqual(uri.Collection, CollectionNsid.ListItem);

        if (!IsAuthenticated)
        {
            throw new AuthenticationRequiredException();
        }

        return await DeleteRecord(
            uri: uri,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Deletes the list entry referred to by the <paramref name="did"/> from the list referred to by <see cref="Uri"/>.
    /// </summary>
    /// <param name="uri">The <see cref="AtUri"/> of the list to delete the subject whose <see cref="Did"/> matches <paramref name="did"/>.</param>
    /// <param name="did">The <see cref="Did"/> of the subject to delete from the list</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="did"/>, <paramref name="uri"/> or the uri collection property is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="uri"/> does not point to the list collection.</exception>
    /// <exception cref="AuthenticationRequiredException">Thrown when the current agent is not authenticated.</exception>
    /// <remarks>
    ///    <para>This method iterates through the list members search for the specified <see cref="Did"/>. This may result in multiple API calls
    ///    depending on the size of the list.</para>
    /// </remarks>
    [SuppressMessage("ApiDesign", "RS0026:Do not add multiple public overloads with optional parameters", Justification = "Pre-existing overload set. The return type changed in this release, which makes the analyzer treat these as newly added overloads.")]
    public async Task<AtProtoHttpResult<DeleteResult>> DeleteFromList(
        AtUri uri,
        Did did,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(uri);
        ArgumentNullException.ThrowIfNull(uri.Collection);
        ArgumentOutOfRangeException.ThrowIfNotEqual(uri.Collection, CollectionNsid.List);

        ArgumentNullException.ThrowIfNull(did);

        if (!IsAuthenticated)
        {
            throw new AuthenticationRequiredException();
        }

        return await DeleteFromList(
            uri: uri,
            predicate: listEntry => listEntry.Subject.Did == did,
            subject: did.ToString(),
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Deletes the list entry referred to by the <paramref name="handle"/> from the list referred to by <see cref="Uri"/>.
    /// </summary>
    /// <param name="uri">The <see cref="AtUri"/> of the list to delete the subject whose <see cref="Did"/> matches <paramref name="handle"/>.</param>
    /// <param name="handle">The <see cref="Handle"/> of the subject to delete from the list</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="handle"/>, <paramref name="uri"/> or the uri collection property is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="uri"/> does not point to the list collection.</exception>
    /// <exception cref="AuthenticationRequiredException">Thrown when the current agent is not authenticated.</exception>
    /// <remarks>
    ///    <para>This method iterates through the list members search for the specified <see cref="Did"/>. This may result in multiple API calls
    ///    depending on the size of the list.</para>
    /// </remarks>
    [SuppressMessage("ApiDesign", "RS0026:Do not add multiple public overloads with optional parameters", Justification = "Pre-existing overload set. The return type changed in this release, which makes the analyzer treat these as newly added overloads.")]
    public async Task<AtProtoHttpResult<DeleteResult>> DeleteFromList(
        AtUri uri,
        Handle handle,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(uri);
        ArgumentNullException.ThrowIfNull(uri.Collection);
        ArgumentOutOfRangeException.ThrowIfNotEqual(uri.Collection, CollectionNsid.List);

        ArgumentNullException.ThrowIfNull(handle);

        if (!IsAuthenticated)
        {
            throw new AuthenticationRequiredException();
        }

        return await DeleteFromList(
            uri: uri,
            predicate: listEntry => listEntry.Subject.Handle == handle,
            subject: handle.ToString(),
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Pages through the members of the list referred to by <paramref name="uri"/> and deletes the first entry matching <paramref name="predicate"/>.
    /// </summary>
    /// <param name="uri">The <see cref="AtUri"/> of the list to search.</param>
    /// <param name="predicate">A predicate used to identify the list entry to delete.</param>
    /// <param name="subject">A description of the subject being searched for, used in the error detail when no entry matches.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    private async Task<AtProtoHttpResult<DeleteResult>> DeleteFromList(
        AtUri uri,
        Func<ListItemView, bool> predicate,
        string subject,
        CancellationToken cancellationToken)
    {
        string? cursor = null;
        AtProtoHttpResult<ListViewWithItems> listEntriesResult;

        do
        {
            cancellationToken.ThrowIfCancellationRequested();

            listEntriesResult = await GetList(
                uri,
                limit: 100,
                cursor: cursor,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            if (!listEntriesResult.Succeeded)
            {
                return new AtProtoHttpResult<DeleteResult>(
                    result: null,
                    statusCode: listEntriesResult.StatusCode,
                    httpResponseHeaders: listEntriesResult.HttpResponseHeaders,
                    atErrorDetail: listEntriesResult.AtErrorDetail,
                    rateLimit: listEntriesResult.RateLimit);
            }

            ListItemView? hit = listEntriesResult.Result.FirstOrDefault(predicate);

            if (hit is not null)
            {
                return await DeleteFromList(hit.Uri, cancellationToken: cancellationToken).ConfigureAwait(false);
            }

            cursor = listEntriesResult.Result.Cursor;
        }
        while (!string.IsNullOrEmpty(cursor));

        return new AtProtoHttpResult<DeleteResult>(
            result: null,
            statusCode: HttpStatusCode.NotFound,
            httpResponseHeaders: listEntriesResult.HttpResponseHeaders,
            atErrorDetail: new AtErrorDetail("NotFound", $"{subject} not found in list {uri}"),
            rateLimit: listEntriesResult.RateLimit);
    }
}
