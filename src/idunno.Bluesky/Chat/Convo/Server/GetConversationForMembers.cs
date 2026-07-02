// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;

using idunno.AtProto;
using idunno.AtProto.Authentication;
using idunno.Bluesky.Chat;
using idunno.Bluesky.Chat.Model;

using Microsoft.Extensions.Logging;

namespace idunno.Bluesky;

public static partial class BlueskyServer
{
    /// <summary>
    /// Gets a <see cref="ConversationView">view</see> over a conversation between <paramref name="members"/>.
    /// </summary>
    /// <param name="members">The <see cref="Did"/>s of the conversation members.</param>
    /// <param name="service">The <see cref="Uri"/> of the service to retrieve the conversation from.</param>
    /// <param name="accessCredentials">The <see cref="AccessCredentials"/> to use when accessing the <paramref name="service"/>.</param>
    /// <param name="httpClient">An <see cref="HttpClient"/> to use when making a request to the <paramref name="service"/>.</param>
    /// <param name="onCredentialsUpdated">An <see cref="Action{T}" /> to call if the credentials in the request need updating.</param>
    /// <param name="loggerFactory">An instance of <see cref="ILoggerFactory"/> to use to create a logger.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">
    ///   Thrown when <paramref name="members"/>, <paramref name="accessCredentials"/>, <paramref name="service"/> or <paramref name="httpClient"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="members"/> is empty or has greater than the maximum number of conversation members.</exception>
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
        Justification = "All types are preserved in the JsonSerializerOptions call to Get().")]
    [UnconditionalSuppressMessage("AOT",
        "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
        Justification = "All types are preserved in the JsonSerializerOptions call to Get().")]
    public static async Task<AtProtoHttpResult<ConversationView>> GetConversationForMembers(
        ICollection<Did> members,
        Uri service,
        AccessCredentials accessCredentials,
        HttpClient httpClient,
        Action<AtProtoCredential>? onCredentialsUpdated = null,
        ILoggerFactory? loggerFactory = default,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(members);
        ArgumentNullException.ThrowIfNull(service);
        ArgumentNullException.ThrowIfNull(accessCredentials);
        ArgumentNullException.ThrowIfNull(httpClient);

        ArgumentOutOfRangeException.ThrowIfZero(members.Count);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(members.Count, Maximum.ConversationMembers);

        BlueskyHttpClient<ConversationResponse> client = new(ChatProxy, loggerFactory);

        StringBuilder queryStringBuilder = new();
        foreach (Did did in members)
        {
            queryStringBuilder.Append(CultureInfo.InvariantCulture, $"members={Uri.EscapeDataString(did)}&");
        }
        queryStringBuilder.Length--;

        string queryString = queryStringBuilder.ToString();

        AtProtoHttpResult<ConversationResponse> response = await client.Get(
            service,
            $"/xrpc/chat.bsky.convo.getConvoForMembers?{queryString}",
            credentials: accessCredentials,
            httpClient: httpClient,
            jsonSerializerOptions: BlueskyJsonSerializerOptions,
            onCredentialsUpdated: onCredentialsUpdated,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        if (response.Succeeded)
        {
            return new AtProtoHttpResult<ConversationView>(
                response.Result.Conversation,
                response.StatusCode,
                response.HttpResponseHeaders,
                response.AtErrorDetail,
                response.RateLimit);
        }
        else
        {
            return new AtProtoHttpResult<ConversationView>(
                null,
                response.StatusCode,
                response.HttpResponseHeaders,
                response.AtErrorDetail,
                response.RateLimit);
        }
    }
}
