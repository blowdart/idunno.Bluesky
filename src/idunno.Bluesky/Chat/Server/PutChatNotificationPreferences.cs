// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;

using idunno.AtProto;
using idunno.AtProto.Authentication;
using idunno.Bluesky.Chat.Model;
using idunno.Bluesky.Chat.Notifications;

using Microsoft.Extensions.Logging;

namespace idunno.Bluesky;

public static partial class BlueskyServer
{
    /// <summary>
    /// Set the requesting account's chat notification preferences. Only the provided preferences are updated; omitted preferences are left unchanged.
    /// </summary>
    /// <param name="chat">Notification preferences for chats.</param>
    /// <param name="chatRequest">Notification preferences for chat requests.</param>
    /// <param name="service">The <see cref="Uri"/> of the service to leave the conversation on.</param>
    /// <param name="accessCredentials">The <see cref="AccessCredentials"/> to use when accessing the <paramref name="service"/>.</param>
    /// <param name="httpClient">An <see cref="HttpClient"/> to use when making a request to the <paramref name="service"/>.</param>
    /// <param name="onCredentialsUpdated">An <see cref="Action{T}" /> to call if the credentials in the request need updating.</param>
    /// <param name="loggerFactory">An instance of <see cref="ILoggerFactory"/> to use to create a logger.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="accessCredentials"/>, <paramref name="service"/> or <paramref name="httpClient"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="System.ArgumentException">Thrown when both <paramref name="chat"/> and <paramref name="chatRequest"/> are <see langword="null"/>.</exception>
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
        Justification = "All types are preserved in the JsonSerializerOptions call to Post().")]
    [UnconditionalSuppressMessage("AOT",
        "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
        Justification = "All types are preserved in the JsonSerializerOptions call to Post().")]
    public static async Task<AtProtoHttpResult<Chat.Notifications.Preferences>> PutChatNotificationPreferences(
        Preference? chat,
        Preference? chatRequest,
        Uri service,
        AccessCredentials accessCredentials,
        HttpClient httpClient,
        Action<AtProtoCredential>? onCredentialsUpdated = null,
        ILoggerFactory? loggerFactory = default,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(service);
        ArgumentNullException.ThrowIfNull(accessCredentials);
        ArgumentNullException.ThrowIfNull(httpClient);

        if (chat is null && chatRequest is null)
        {
            throw new ArgumentException("One or both of chat and chatRequest must be specified");
        }

        BlueskyHttpClient<PutPreferencesResponse> client = new(ChatProxy, loggerFactory);

        AtProtoHttpResult<PutPreferencesResponse> response = await client.Post(
            service,
            "/xrpc/chat.bsky.notification.putPreferences",
            new PutPreferencesRequest(chat, chatRequest),
            credentials: accessCredentials,
            httpClient: httpClient,
            jsonSerializerOptions: BlueskyJsonSerializerOptions,
            onCredentialsUpdated: onCredentialsUpdated,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        if (response.Succeeded)
        {
            return new AtProtoHttpResult<Chat.Notifications.Preferences>(
                response.Result.Preferences,
                response.StatusCode,
                response.HttpResponseHeaders,
                response.AtErrorDetail,
                response.RateLimit);
        }
        else
        {
            return new AtProtoHttpResult<Chat.Notifications.Preferences>(
                null,
                response.StatusCode,
                response.HttpResponseHeaders,
                response.AtErrorDetail,
                response.RateLimit);
        }
    }
}
