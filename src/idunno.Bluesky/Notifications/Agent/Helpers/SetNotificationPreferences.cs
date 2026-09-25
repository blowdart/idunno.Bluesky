// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;

namespace idunno.Bluesky;

public partial class BlueskyAgent
{
    /// <summary>
    /// Sets the current user's notification preferences.
    /// </summary>
    /// <param name="preferences">The notification preferences to set.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="preferences"/> is <see langword="null"/>.</exception>
    /// <exception cref="AuthenticationRequiredException">Thrown when the agent is not authenticated.</exception>
    public async Task<AtProtoHttpResult<Notifications.Preferences>> SetNotificationPreferences(
        Notifications.Preferences preferences,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(preferences);

        if (!IsAuthenticated)
        {
            throw new AuthenticationRequiredException();
        }

        return await PutNotificationPreferences(preferences, cancellationToken).ConfigureAwait(false);
    }
}
