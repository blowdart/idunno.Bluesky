// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;

using idunno.AtProto;
using idunno.AtProto.Repo;
using idunno.Bluesky.Actor;

namespace idunno.Bluesky;

public partial class BlueskyAgent
{
    /// <summary>
    /// Deletes the current authenticated user's content visibility record.
    /// </summary>
    /// <param name="swapCommit">Specified if the operation should compare and swap with the previous commit by cid.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="AuthenticationRequiredException">Thrown when the agent is not authenticated.</exception>
    public async Task<AtProtoHttpResult<Commit>> DeleteContentVisibilityDeclaration(Cid? swapCommit = null, CancellationToken cancellationToken = default)
    {
        if (!IsAuthenticated)
        {
            throw new AuthenticationRequiredException();
        }

        return await DeleteRecord(
            repo: Did,
            collection: "app.bsky.actor.contentVisibilityDeclaration",
            rKey: "self",
            swapCommit: swapCommit,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets an actor's preference for appearing in content discovery surfaces.
    /// Missing records must be treated as <see langword="false"/>.
    /// </summary>
    /// <param name="identifier">The identifier of the actor whose preference should be returned.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="identifier"/> is <see langword="null" />.</exception>
    /// <exception cref="HandleResolutionException">Thrown when <paramref name="identifier"/> is a handle which cannot be resolved to a <see cref="Did"/>.</exception>
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
        Justification = "All types are preserved in the JsonSerializerOptions call to CreateRecord().")]
    [UnconditionalSuppressMessage("AOT",
        "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
        Justification = "All types are preserved in the JsonSerializerOptions call to CreateRecord().")]
    public async Task<AtProtoHttpResult<bool>> GetContentVisibilityDeclaration(AtIdentifier identifier, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(identifier);

        Did? did;

        if (identifier is Handle handle)
        {
            did = await ResolveHandle(handle, cancellationToken).ConfigureAwait(false) ?? throw new HandleResolutionException($"The handle {identifier} could not be resolved.", handle);
        }
        else
        {
            did = (Did)identifier;
        }

        AtProtoHttpResult<AtProtoRepositoryRecord<ContentVisibilityDeclaration>> getRecordResult = await GetBlueskyRecord<ContentVisibilityDeclaration>(
            repo: did.Value,
            collection: "app.bsky.actor.contentVisibilityDeclaration",
            rKey: "self",
            cid: null,
            serviceProxy: null,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        if (getRecordResult.Succeeded)
        {
            return new AtProtoHttpResult<bool>(
                getRecordResult.Result.Value.HideFromAlgorithmicRecommendations,
                getRecordResult.StatusCode,
                getRecordResult.HttpResponseHeaders,
                getRecordResult.AtErrorDetail,
                getRecordResult.RateLimit);
        }
        else
        {
            return new AtProtoHttpResult<bool>(
                false,
                getRecordResult.StatusCode,
                getRecordResult.HttpResponseHeaders,
                getRecordResult.AtErrorDetail,
                getRecordResult.RateLimit);
        }
    }

    /// <summary>
    /// Gets the current authenticated user's preference for appearing in content discovery surfaces.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="AuthenticationRequiredException">Thrown when the agent is not authenticated.</exception>
    public async Task<AtProtoHttpResult<bool>> GetContentVisibilityDeclaration(CancellationToken cancellationToken = default)
    {
        if (!IsAuthenticated)
        {
            throw new AuthenticationRequiredException();
        }

        return await GetContentVisibilityDeclaration(Did, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Sets the content visibility record for the authenticated user.
    /// </summary>
    /// <param name="hideFromAlgorithmicRecommendations">Flag indicating whether the account requests that its posts be hidden from algorithmic recommendations.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="AuthenticationRequiredException">Thrown when the current agent is not authenticated.</exception>
    [UnconditionalSuppressMessage(
    "Trimming",
    "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
    Justification = "All types are preserved in the JsonSerializerOptions call to Put().")]
    [UnconditionalSuppressMessage("AOT",
    "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
    Justification = "All types are preserved in the JsonSerializerOptions call to Put().")]
    public async Task<AtProtoHttpResult<PutRecordResult>> SetContentVisibilityDeclaration(bool hideFromAlgorithmicRecommendations, CancellationToken cancellationToken = default)
    {
        if (!IsAuthenticated)
        {
            throw new AuthenticationRequiredException();
        }

        var declaration = new ContentVisibilityDeclaration(hideFromAlgorithmicRecommendations);

        return await PutRecord(
            record: declaration,
            collection: "app.bsky.actor.contentVisibilityDeclaration",
            rKey: "self",
            validate: null,
            swapCommit: null,
            swapRecord: null,
            jsonSerializerOptions: BlueskyServer.BlueskyJsonSerializerOptions,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Sets the content visibility record for the authenticated user.
    /// </summary>
    /// <param name="declaration">The declaration record to update.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="declaration"/> is <see langword="null"/>.</exception>
    /// <exception cref="AuthenticationRequiredException">Thrown when the current agent is not authenticated.</exception>
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
        Justification = "All types are preserved in the JsonSerializerOptions call to Put().")]
    [UnconditionalSuppressMessage("AOT",
        "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
        Justification = "All types are preserved in the JsonSerializerOptions call to Put().")]
    public async Task<AtProtoHttpResult<PutRecordResult>> SetContentVisibilityDeclaration(
        AtProtoRepositoryRecord<ContentVisibilityDeclaration> declaration,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(declaration);

        if (!IsAuthenticated)
        {
            throw new AuthenticationRequiredException();
        }

        return await PutRecord(
            record: declaration.Value,
            collection: "app.bsky.actor.contentVisibilityDeclaration",
            rKey: "self",
            validate: null,
            swapCommit: null,
            swapRecord: declaration.Cid,
            jsonSerializerOptions: BlueskyServer.BlueskyJsonSerializerOptions,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}
