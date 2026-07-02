// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;

namespace idunno.Bluesky;

public partial class BlueskyAgent
{
    /// <summary>
    /// Updates the authenticated user's preferences for chat notifications.
    /// </summary>
    /// <param name="preferences">The new chat notification preferences.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="AuthenticationRequiredException">Thrown when the current agent is not authenticated.</exception>
    /// <exception cref="System.ArgumentNullException">Thrown when <paramref name="preferences"/> is <see langword="null"/>.</exception>
    public async Task<AtProtoHttpResult<Chat.Notifications.Preferences>> PutChatNotificationPreferences(
        Chat.Notifications.Preferences preferences)
    {
        ArgumentNullException.ThrowIfNull(preferences);

        if (!IsAuthenticated)
        {
            throw new AuthenticationRequiredException();
        }

        return await PutChatNotificationPreferences(preferences, default).ConfigureAwait(false);
    }

    /// <summary>
    /// Updates the authenticated user's preferences for chat notifications.
    /// </summary>
    /// <param name="preferences">The new chat notification preferences.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="AuthenticationRequiredException">Thrown when the current agent is not authenticated.</exception>
    /// <exception cref="System.ArgumentNullException">Thrown when <paramref name="preferences"/> is <see langword="null"/>.</exception>
    public async Task<AtProtoHttpResult<Chat.Notifications.Preferences>> PutChatNotificationPreferences(
        Chat.Notifications.Preferences preferences,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(preferences);

        if (!IsAuthenticated)
        {
            throw new AuthenticationRequiredException();
        }

        return await PutChatNotificationPreferences(
            chat: preferences.Chat,
            chatRequest: preferences.ChatRequest,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Updates the authenticated user's preferences for chat notifications. Either <paramref name="chat"/>, <paramref name="chatRequest"/>, or both must be specified.
    /// </summary>
    /// <param name="chat">The new chat notification preferences.</param>
    /// <param name="chatRequest">The new chat request notification preferences.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="AuthenticationRequiredException">Thrown when the current agent is not authenticated.</exception>
    /// <exception cref="ArgumentException">Thrown when both <paramref name="chat"/> and <paramref name="chatRequest"/> are <see langword="null"/>.</exception>
    public async Task<AtProtoHttpResult<Chat.Notifications.Preferences>> PutChatNotificationPreferences(
        Chat.Notifications.Preference? chat = null,
        Chat.Notifications.Preference? chatRequest = null,
        CancellationToken cancellationToken = default)
    {
        if (chat is null && chatRequest is null)
        {
            throw new ArgumentException("One or both of chat and chatRequest must be specified");
        }

        if (!IsAuthenticated)
        {
            throw new AuthenticationRequiredException();
        }

        return await BlueskyServer.PutChatNotificationPreferences(
            chat: chat,
            chatRequest: chatRequest,
            service: Service,
            accessCredentials: Credentials,
            httpClient: HttpClient,
            onCredentialsUpdated: InternalOnCredentialsUpdatedCallBack,
            loggerFactory: LoggerFactory,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}