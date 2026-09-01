// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;

using idunno.AtProto;
using idunno.AtProto.Repo;

namespace idunno.Bluesky;

public partial class BlueskyAgent
{
    /// <summary>
    /// Gets the conversation declaration record for authenticated user.
    /// </summary>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="AuthenticationRequiredException">Thrown when the current agent is not authenticated.</exception>
    public async Task<AtProtoHttpResult<AtProtoRepositoryRecord<Chat.Actor.Declaration>>> GetConversationDeclaration()
    {
        if (!IsAuthenticated)
        {
            throw new AuthenticationRequiredException();
        }

        return await GetConversationDeclaration(cancellationToken: default).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets the conversation declaration record for authenticated user.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="AuthenticationRequiredException">Thrown when the current agent is not authenticated.</exception>
    public async Task<AtProtoHttpResult<AtProtoRepositoryRecord<Chat.Actor.Declaration>>> GetConversationDeclaration(CancellationToken cancellationToken)
    {
        if (!IsAuthenticated)
        {
            throw new AuthenticationRequiredException();
        }

        return await GetConversationDeclaration(Did, cancellationToken).ConfigureAwait(false);
    }


    /// <summary>
    /// Gets the conversation declaration record for the specified <paramref name="did"/>.
    /// </summary>
    /// <param name="did">The <see cref="Did"/> whose conversation record should be retrieved.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="did"/> is <see langword="null"/>.</exception>
    public async Task<AtProtoHttpResult<AtProtoRepositoryRecord<Chat.Actor.Declaration>>> GetConversationDeclaration(Did did, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(did);

        return await
            GetBlueskyRecord<Chat.Actor.Declaration>($"at://{did}/{CollectionNsid.ChatDeclaration}/self", cancellationToken: cancellationToken).ConfigureAwait(false);
    }