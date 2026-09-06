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
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="uri"/> or <paramref name="did"/> is <see langword="null"/>.</exception>
    /// <exception cref="AuthenticationRequiredException">Thrown when the current agent is not authenticated.</exception>
    public async Task<AtProtoHttpResult<CreateRecordResult>> AddToList(
        AtUri uri,
        Did did,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(uri);
        ArgumentNullException.ThrowIfNull(did);

        if (!IsAuthenticated)
        {
            throw new AuthenticationRequiredException();
        }

        ListItem listItem = new() { List = uri, Subject = did };

        return await CreateBlueskyRecord<BlueskyTimestampedRecord>(
            record: listItem,
            collection: CollectionNsid.ListItem,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Adds the <paramref name="handle"/> to the specified <paramref name="uri"/>.
    /// </summary>
    /// <param name="uri">The <see cref="AtUri"/> of the list to add the <paramref name="handle"/> to.</param>
    /// <param name="handle">The <see cref="Did"/> of the actor to add to the list.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="uri"/> or <paramref name="handle"/> is <see langword="null"/>.</exception>
    /// <exception cref="AuthenticationRequiredException">Thrown when the current agent is not authenticated.</exception>
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
        Justification = "All types are preserved in the JsonSerializerOptions call to Put().")]
    [UnconditionalSuppressMessage("AOT",
        "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
        Justification = "All types are preserved in the JsonSerializerOptions call to Put().")]
    public async Task<AtProtoHttpResult<CreateRecordResult>> AddToList(
        AtUri uri,
        Handle handle,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(uri);
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
            uri: uri,
            did: did,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}
