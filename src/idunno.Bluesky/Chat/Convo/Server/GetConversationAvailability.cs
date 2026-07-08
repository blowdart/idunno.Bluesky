// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;

using idunno.AtProto;
using idunno.AtProto.Authentication;
using idunno.Bluesky.Chat;

using Microsoft.Extensions.Logging;

namespace idunno.Bluesky;

public static partial class BlueskyServer
{
    /// <summary>
    /// Check whether the current authenticated user and the other actors can start a 1-1 chat.
    /// Only applicable to direct (non-group) conversations. If an existing conversation is found for these members, it is returned.
    /// Does not create a new conversation if it doesn't exist.
    /// </summary>
    /// <param name="members">A collection of <see cref="Did"/> of actors to check availability for..</param>
    /// <param name="service">The <see cref="Uri"/> of the service to the conversation availability from.</param>
    /// <param name="accessCredentials">The <see cref="AccessCredentials"/> to use when accessing the <paramref name="service"/>.</param>
    /// <param name="httpClient">An <see cref="HttpClient"/> to use when making a request to the <paramref name="service"/>.</param>
    /// <param name="onCredentialsUpdated">An <see cref="Action{T}" /> to call if the credentials in the request need updating.</param>
    /// <param name="loggerFactory">An instance of <see cref="ILoggerFactory"/> to use to create a logger.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="members"/> is empty or contains more than the maximum allowed members.</exception>
    /// <exception cref="ArgumentNullException">Thrown when any of <paramref name="accessCredentials"/>, <paramref name="service"/> or <paramref name="httpClient"/> are <see langword="null"/>.</exception>
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
        Justification = "All types are preserved in the JsonSerializerOptions call to Get().")]
    [UnconditionalSuppressMessage("AOT",
        "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
        Justification = "All types are preserved in the JsonSerializerOptions call to Get().")]
    public static async Task<AtProtoHttpResult<ConversationAvailability>> GetConversationAvailability(
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

        BlueskyHttpClient<ConversationAvailability> client = new(ChatProxy, loggerFactory);
        StringBuilder queryStringBuilder = new();
        foreach (Did did in members)
        {
            queryStringBuilder.Append(CultureInfo.InvariantCulture, $"members={Uri.EscapeDataString(did)}&");
        }
        queryStringBuilder.Length--;
        string queryString = queryStringBuilder.ToString();

        AtProtoHttpResult<ConversationAvailability> response = await client.Get(
            service,
            $"/xrpc/chat.bsky.convo.getConvoAvailability?{queryString}",
            credentials: accessCredentials,
            httpClient: httpClient,
            jsonSerializerOptions: BlueskyJsonSerializerOptions,
            onCredentialsUpdated: onCredentialsUpdated,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        if (response.Succeeded)
        {
            return response;
        }
        else
        {
            return new AtProtoHttpResult<ConversationAvailability>(
                null,
                response.StatusCode,
                response.HttpResponseHeaders,
                response.AtErrorDetail,
                response.RateLimit);
        }
    }
}