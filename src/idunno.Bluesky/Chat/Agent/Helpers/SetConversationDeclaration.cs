// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;

using idunno.AtProto;
using idunno.AtProto.Repo;

namespace idunno.Bluesky;

public partial class BlueskyAgent
{
    /// <summary>
    /// Sets a conversation declaration record for the current user. Requires authentication.
    /// </summary>
    /// <param name="allowIncoming">Specifies whether incoming messages are allowed. Known values are specified in <see cref="Chat.Actor.AllowIncoming"/></param>
    /// <param name="allowGroupInvites">Specifies whether group invites are allowed. Known values are specified in <see cref="Chat.Actor.AllowGroupInvites"/></param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="allowIncoming"/> or <paramref name="allowGroupInvites"/> is <see langword="null"/> or whitespace.</exception>
    /// <exception cref="AuthenticationRequiredException">Thrown when the current session is not authenticated.</exception>
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
        Justification = "All types are preserved in the JsonSerializerOptions call to Put().")]
    [UnconditionalSuppressMessage("AOT",
        "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
        Justification = "All types are preserved in the JsonSerializerOptions call to Put().")]
    public async Task<AtProtoHttpResult<PutRecordResult>> SetConversationDeclaration(string allowIncoming, string allowGroupInvites)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(allowIncoming);
        ArgumentException.ThrowIfNullOrWhiteSpace(allowGroupInvites);

        return await SetConversationDeclaration(allowIncoming, allowGroupInvites, cancellationToken: default).ConfigureAwait(false);
    }

    /// <summary>
    /// Sets a conversation declaration record for the current user. Requires authentication.
    /// </summary>
    /// <param name="allowIncoming">Specifies whether incoming messages are allowed. Known values are specified in <see cref="Chat.Actor.AllowIncoming"/></param>
    /// <param name="allowGroupInvites">Specifies whether group invites are allowed. Known values are specified in <see cref="Chat.Actor.AllowGroupInvites"/></param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="allowIncoming"/> or <paramref name="allowGroupInvites"/> is <see langword="null"/> or whitespace.</exception>
    /// <exception cref="AuthenticationRequiredException">Thrown when the current session is not authenticated.</exception>
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
        Justification = "All types are preserved in the JsonSerializerOptions call to Put().")]
    [UnconditionalSuppressMessage("AOT",
        "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
        Justification = "All types are preserved in the JsonSerializerOptions call to Put().")]
    public async Task<AtProtoHttpResult<PutRecordResult>> SetConversationDeclaration(string allowIncoming, string allowGroupInvites, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(allowIncoming);
        ArgumentException.ThrowIfNullOrWhiteSpace(allowGroupInvites);

        var declaration = new Chat.Actor.Declaration(allowIncoming, allowGroupInvites);

        return await PutRecord(
            record: declaration,
            jsonSerializerOptions: BlueskyServer.BlueskyJsonSerializerOptions,
            collection: CollectionNsid.ChatDeclaration,
            rKey: "self",
            validate: null,
            swapCommit: null,
            swapRecord: null,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Update a conversation declaration record for the current user. Requires authentication.
    /// </summary>
    /// <param name="declaration">The declaration record to update.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="declaration"/> is <see langword="null"/>.</exception>
    /// <exception cref="AuthenticationRequiredException">Thrown when the current session is not authenticated.</exception>
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
        Justification = "All types are preserved in the JsonSerializerOptions call to Put().")]
    [UnconditionalSuppressMessage("AOT",
        "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
        Justification = "All types are preserved in the JsonSerializerOptions call to Put().")]
    [SuppressMessage("ApiDesign", "RS0027:API with optional parameter(s) should have the most parameters amongst its public overloads", Justification = "Using the strong record declaration as the default method.")]
    public async Task<AtProtoHttpResult<PutRecordResult>> SetConversationDeclaration(
        AtProtoRepositoryRecord<Chat.Actor.Declaration> declaration,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(declaration);

        if (!IsAuthenticated)
        {
            throw new AuthenticationRequiredException();
        }

        return await PutRecord(
            record: declaration.Value,
            jsonSerializerOptions: BlueskyServer.BlueskyJsonSerializerOptions,
            collection: CollectionNsid.ChatDeclaration,
            rKey: "self",
            validate: null,
            swapCommit: null,
            swapRecord: declaration.Cid,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}