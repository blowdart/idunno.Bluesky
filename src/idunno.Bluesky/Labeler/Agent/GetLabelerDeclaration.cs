// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;

using idunno.AtProto;
using idunno.AtProto.Repo;
using idunno.Bluesky.Labeler;

namespace idunno.Bluesky;

public partial class BlueskyAgent
{
    /// <summary>
    /// Gets the labeler declaration record value for the specified <paramref name="did"/>.
    /// </summary>
    /// <param name="did">The labeler <see cref="AtProto.Did" /> whose declaration record should be returned.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="did"/> is <see langword="null"/>.</exception>
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
        Justification = "All types are preserved in the JsonSerializerOptions call to Get().")]
    [UnconditionalSuppressMessage("AOT",
        "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
        Justification = "All types are preserved in the JsonSerializerOptions call to Get().")]
    public async Task<AtProtoHttpResult<Service>> GetLabelerDeclaration(
        Did did,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(did);

        AtProtoHttpResult<AtProtoRepositoryRecord<Service>> getRecordResult = await GetRecord<Service>(
            new AtUri($"at://{did}/app.bsky.labeler.service/self"),
            jsonSerializerOptions: BlueskyServer.BlueskyJsonSerializerOptions,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        if (getRecordResult.Succeeded)
        {
            return new AtProtoHttpResult<Service>(
                getRecordResult.Result.Value,
                statusCode: getRecordResult.StatusCode,
                httpResponseHeaders: getRecordResult.HttpResponseHeaders,
                atErrorDetail: getRecordResult.AtErrorDetail,
                rateLimit: getRecordResult.RateLimit);
        }
        else
        {
            return new AtProtoHttpResult<Service>(
                null,
                statusCode: getRecordResult.StatusCode,
                httpResponseHeaders: getRecordResult.HttpResponseHeaders,
                atErrorDetail: getRecordResult.AtErrorDetail,
                rateLimit: getRecordResult.RateLimit);
        }
    }

    /// <summary>
    /// Gets the labeler declaration record value for the specified <paramref name="did"/>.
    /// </summary>
    /// <param name="did">The labeler <see cref="AtProto.Did" /> whose declaration record should be returned.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    public async Task<AtProtoHttpResult<Service>> GetLabelerDeclaration(
        Did did)
    {
        return await GetLabelerDeclaration(did, default).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets the labeler declaration record value for the specified <paramref name="handle"/>.
    /// </summary>
    /// <param name="handle">The labeler <see cref="Handle"/> whose declaration record should be returned.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="handle"/> is <see langword="null"/>.</exception>
    public async Task<AtProtoHttpResult<Service>> GetLabelerDeclaration(
        Handle handle,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(handle);

        Did? did = await ResolveHandle(handle, cancellationToken: cancellationToken).ConfigureAwait(false);

        if (did == null)
        {
            return new AtProtoHttpResult<Service>(
                null,
                statusCode: System.Net.HttpStatusCode.NotFound,
                httpResponseHeaders: null);
        }

        return await GetLabelerDeclaration(did, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets the labeler declaration record value for the specified <paramref name="handle"/>.
    /// </summary>
    /// <param name="handle">The labeler <see cref="Handle"/> whose declaration record should be returned.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    public async Task<AtProtoHttpResult<Service>> GetLabelerDeclaration(
        Handle handle)
    {
        return await GetLabelerDeclaration(handle, default).ConfigureAwait(false);
    }


    /// <summary>
    /// Gets the labeler declaration record value for the specified <paramref name="identifier"/>.
    /// </summary>
    /// <param name="identifier">The labeler <see cref="Handle"/> whose declaration record should be returned.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="identifier"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="identifier"/> cannot be converted to a <see cref="Did"/> or <see cref="Handle"/>.</exception>
    public async Task<AtProtoHttpResult<Service>> GetLabelerDeclaration(
        AtIdentifier identifier,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(identifier);

        if (identifier is Did did)
        {
            return await GetLabelerDeclaration(did, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        else if (identifier is Handle handle)
        {
            return await GetLabelerDeclaration(handle, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        else
        {
            throw new ArgumentException("identifier is neither a handle or a did.", nameof(identifier));
        }
    }

    /// <summary>
    /// Gets the labeler declaration record value for the specified <paramref name="identifier"/>.
    /// </summary>
    /// <param name="identifier">The labeler <see cref="Handle"/> whose declaration record should be returned.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    public async Task<AtProtoHttpResult<Service>> GetLabelerDeclaration(
        AtIdentifier identifier)
    {
        return await GetLabelerDeclaration(identifier, default).ConfigureAwait(false);
    }
}
