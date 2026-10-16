// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Net;

using idunno.AtProto;
using idunno.AtProto.Repo;
using idunno.Bluesky.Graph;
using idunno.Bluesky.Record;

namespace idunno.Bluesky;

public partial class BlueskyAgent
{
    /// <summary>
    /// Adds the <paramref name="did"/> to the specified <paramref name="uri"/>.
    /// </summary>
    /// <param name="uri">The <see cref="AtUri"/> of the list to add the <paramref name="did"/> to.</param>
    /// <param name="did">The <see cref="Did"/> of the actor to add to the list.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="uri"/>, its collection property, or <paramref name="did"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="uri"/> does not point to a list.</exception>
    /// <exception cref="AuthenticationRequiredException">Thrown when the current agent is not authenticated.</exception>
    public async Task<AtProtoHttpResult<CreateRecordResult>> AddToList(
        AtUri uri,
        Did did,
        CancellationToken cancellationToken = default)
    {
        return await AddToList(rKey: null, uri, did, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Adds the specified <paramref name="did"/> to the list using the specified <paramref name="rKey"/>.
    /// </summary>
    /// <param name="rKey">The record key to use for the list item.</param>
    /// <param name="uri">The <see cref="AtUri"/> of the list to add the <paramref name="did"/> to.</param>
    /// <param name="did">The <see cref="Did"/> of the actor to add to the list.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="uri"/>, its collection property, or <paramref name="did"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="uri"/> does not point to a list.</exception>
    /// <exception cref="AuthenticationRequiredException">Thrown when the current agent is not authenticated.</exception>
    [SuppressMessage("ApiDesign", "RS0026:Do not add multiple public overloads with optional parameters", Justification = "The caller-supplied-key overload preserves the existing signature and cancellation-token call patterns.")]
    public async Task<AtProtoHttpResult<CreateRecordResult>> AddToList(
        RecordKey? rKey,
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

        ListItem listItem = new() { List = uri, Subject = did };

        return await CreateBlueskyRecord<BlueskyTimestampedRecord>(
            record: listItem,
            collection: CollectionNsid.ListItem,
            rKey: rKey,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Adds the <paramref name="handle"/> to the specified <paramref name="uri"/>.
    /// </summary>
    /// <param name="uri">The <see cref="AtUri"/> of the list to add the <paramref name="handle"/> to.</param>
    /// <param name="handle">The <see cref="Did"/> of the actor to add to the list.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="uri"/>, its collection property, or <paramref name="handle"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="uri"/> does not point to a list.</exception>
    /// <exception cref="AuthenticationRequiredException">Thrown when the current agent is not authenticated.</exception>
    public async Task<AtProtoHttpResult<CreateRecordResult>> AddToList(
        AtUri uri,
        Handle handle,
        CancellationToken cancellationToken = default)
    {
        return await AddToList(rKey: null, uri, handle, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Adds the specified <paramref name="handle"/> to the list using the specified <paramref name="rKey"/>.
    /// </summary>
    /// <param name="rKey">The record key to use for the list item.</param>
    /// <param name="uri">The <see cref="AtUri"/> of the list to add the <paramref name="handle"/> to.</param>
    /// <param name="handle">The handle of the actor to add to the list.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="uri"/>, its collection property, or <paramref name="handle"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="uri"/> does not point to a list.</exception>
    /// <exception cref="AuthenticationRequiredException">Thrown when the current agent is not authenticated.</exception>
    [SuppressMessage("ApiDesign", "RS0026:Do not add multiple public overloads with optional parameters", Justification = "The caller-supplied-key overload preserves the existing signature and cancellation-token call patterns.")]
    public async Task<AtProtoHttpResult<CreateRecordResult>> AddToList(
        RecordKey? rKey,
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

        Did? did = await ResolveHandle(handle, cancellationToken: cancellationToken).ConfigureAwait(false);

        if (did is null)
        {
            return new AtProtoHttpResult<CreateRecordResult>(
                result: null,
                statusCode: HttpStatusCode.NotFound,
                httpResponseHeaders: null,
                atErrorDetail: new AtErrorDetail("NotFound", $"{handle} cannot be resolved"),
                rateLimit: null);
        }

        return await AddToList(
            rKey: rKey,
            uri: uri,
            did: did,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}
