// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;

using idunno.AtProto;
using idunno.AtProto.Repo;
using idunno.Bluesky.Notifications;

namespace idunno.Bluesky;

public partial class BlueskyAgent
{
    /// <summary>
    /// Creates a notification declaration record for the current user.Requires authentication.
    /// </summary>
    /// <param name="notificationAllowedFrom">Indicates who will be allowed to subscribe to post notifications for the current user.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="AuthenticationRequiredException">Thrown when the current session is not authenticated.</exception>
    public async Task<AtProtoHttpResult<CreateRecordResult>> SetNotificationDeclaration(NotificationAllowedFrom notificationAllowedFrom, CancellationToken cancellationToken = default)
    {
        if (!IsAuthenticated)
        {
            throw new AuthenticationRequiredException();
        }

        return await CreateBlueskyRecord(
            record: new Notifications.Declaration(notificationAllowedFrom),
            collection: CollectionNsid.NotificationDeclaration,
            rKey: "self",
            validate: null,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Update a notification declaration record for the current user. Requires authentication.
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
    public async Task<AtProtoHttpResult<PutRecordResult>> SetNotificationDeclaration(AtProtoRepositoryRecord<Declaration> declaration, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(declaration);

        if (!IsAuthenticated)
        {
            throw new AuthenticationRequiredException();
        }

        return await PutRecord(
            record: declaration.Value,
            jsonSerializerOptions: BlueskyServer.BlueskyJsonSerializerOptions,
            collection: CollectionNsid.NotificationDeclaration,
            rKey: "self",
            validate: null,
            swapCommit: null,
            swapRecord: declaration.Cid,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}
