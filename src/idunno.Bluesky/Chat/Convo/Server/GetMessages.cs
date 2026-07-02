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
    /// Enumerates the messages in a conversation.
    /// </summary>
    /// <param name="id">The conversation identifier whose messages should be retrieved.</param>
    /// <param name="limit">An optional limit on the number of messages to retrieve in each page.</param>
    /// <param name="cursor">An optional cursor used for pagination.</param>
    /// <param name="service">The <see cref="Uri"/> of the service to retrieve the conversation messages from.</param>
    /// <param name="accessCredentials">The <see cref="AccessCredentials"/> to use when accessing the <paramref name="service"/>.</param>
    /// <param name="httpClient">An <see cref="HttpClient"/> to use when making a request to the <paramref name="service"/>.</param>
    /// <param name="onCredentialsUpdated">An <see cref="Action{T}" /> to call if the credentials in the request need updating.</param>
    /// <param name="loggerFactory">An instance of <see cref="ILoggerFactory"/> to use to create a logger.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="id"/> is <see langword="null"/> or empty.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="accessCredentials"/>, <paramref name="service"/> or <paramref name="httpClient"/> is <see langword="null"/>.
    /// </exception>
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
        Justification = "All types are preserved in the JsonSerializerOptions call to Get().")]
    [UnconditionalSuppressMessage("AOT",
        "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
        Justification = "All types are preserved in the JsonSerializerOptions call to Get().")]
    public static async Task<AtProtoHttpResult<Messages>> GetMessages(
        string id,
        int? limit,
        string? cursor,
        Uri service,
        AccessCredentials accessCredentials,
        HttpClient httpClient,
        Action<AtProtoCredential>? onCredentialsUpdated = null,
        ILoggerFactory? loggerFactory = default,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(id);

        ArgumentNullException.ThrowIfNull(service);
        ArgumentNullException.ThrowIfNull(accessCredentials);
        ArgumentNullException.ThrowIfNull(httpClient);

        if (limit is not null)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan((int)limit, 1);
            ArgumentOutOfRangeException.ThrowIfGreaterThan((int)limit, Maximum.MessagesToList);
        }

        StringBuilder queryStringBuilder = new();
        queryStringBuilder.Append(CultureInfo.InvariantCulture, $"convoId={Uri.EscapeDataString(id)}");

        if (cursor != null)
        {
            queryStringBuilder.Append(CultureInfo.InvariantCulture, $"&cursor={Uri.EscapeDataString(cursor)}");
        }

        if (limit != null)
        {
            queryStringBuilder.Append(CultureInfo.InvariantCulture, $"&limit={limit}");
        }

        string queryString = queryStringBuilder.ToString();

        BlueskyHttpClient<GetMessagesResponse> client = new(ChatProxy, loggerFactory);

        AtProtoHttpResult<GetMessagesResponse> response = await client.Get(
            service,
            $"/xrpc/chat.bsky.convo.getMessages?{queryString}",
            credentials: accessCredentials,
            httpClient: httpClient,
            jsonSerializerOptions: BlueskyJsonSerializerOptions,
            onCredentialsUpdated: onCredentialsUpdated,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        if (response.Succeeded)
        {
            return new AtProtoHttpResult<Messages>(
                new Messages(response.Result.Messages, response.Result.Cursor),
                response.StatusCode,
                response.HttpResponseHeaders,
                response.AtErrorDetail,
                response.RateLimit);
        }
        else
        {
            return new AtProtoHttpResult<Messages>(
                null,
                response.StatusCode,
                response.HttpResponseHeaders,
                response.AtErrorDetail,
                response.RateLimit);
        }
    }
}
