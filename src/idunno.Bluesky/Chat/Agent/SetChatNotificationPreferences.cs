// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;

using idunno.AtProto;

namespace idunno.Bluesky;

public partial class BlueskyAgent
{
    /// <summary>
    /// Sets the authenticated user's preferences for chat notifications.
    /// </summary>
    /// <param name="preferences">The new chat notification preferences.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="AuthenticationRequiredException">Thrown when the current agent is not authenticated.</exception>
    /// <exception cref="System.ArgumentNullException">Thrown when <paramref name="preferences"/> is <see langword="null"/>.</exception>
    public async Task<AtProtoHttpResult<Chat.Notifications.Preferences>> SetChatNotificationPreferences(
        Chat.Notifications.Preferences preferences,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(preferences);

        if (!IsAuthenticated)
        {
            throw new AuthenticationRequiredException();
        }

        return await PutChatNotificationPreferences(
            preferences: preferences,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}
