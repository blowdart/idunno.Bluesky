// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;
using idunno.AtProto.Repo;
using idunno.Bluesky.Notifications;

namespace idunno.Bluesky;

public partial class BlueskyAgent
{
    /// <summary>
    /// Gets the notification declaration record for the current user.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="AuthenticationRequiredException">Thrown when the current agent is not authenticated.</exception>
    public async Task<AtProtoHttpResult<AtProtoRepositoryRecord<Declaration>>> GetNotificationDeclaration(CancellationToken cancellationToken = default)
    {
        if (!IsAuthenticated)
        {
            throw new AuthenticationRequiredException();
        }

        return await GetNotificationDeclaration(Did, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets the notification declaration record for the specified <paramref name="did"/>.
    /// </summary>
    /// <param name="did">The <see cref="Did"/> whose notification record should be retrieved.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="did"/> is <see langword="null"/>.</exception>
    public async Task<AtProtoHttpResult<AtProtoRepositoryRecord<Declaration>>> GetNotificationDeclaration(Did did, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(did);

        return
            await GetBlueskyRecord<Declaration>($"at://{did}/{CollectionNsid.NotificationDeclaration}/self", cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}
