// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;

using idunno.AtProto;
using idunno.AtProto.Authentication;
using idunno.Bluesky.Chat;
using idunno.Bluesky.Chat.Convo.Model;

using Microsoft.Extensions.Logging;

namespace idunno.Bluesky;

public static partial class BlueskyServer
{
    /// <summary>
    /// Enumerates a list of conversations the current user is a part of.
    /// </summary>
    /// <param name="limit">The number of conversations to return.</param>
    /// <param name="cursor">A cursor used for pagination.</param>
    /// <param name="readState">An optional filter limiting the results to conversations in the specified read state. Known values are defined in <see cref="ConversationReadState"/>.</param>
    /// <param name="status">An optional filter limiting the results to conversations with the specified status. Known values are defined in <see cref="ConversationStatus"/>.</param>
    /// <param name="kind">An optional filter limiting the results to conversations of the specified kind. Known values are defined in <see cref="ConversationKind"/>.</param>
    /// <param name="lockStatus">An optional filter limiting the results to conversations with the specified lock status. Known values are defined in <see cref="ConversationLockStatus"/>.</param>
    /// <param name="service">The <see cref="Uri"/> of the service to retrieve the conversations from.</param>
    /// <param name="accessCredentials">The <see cref="AccessCredentials"/> to use when accessing the <paramref name="service"/>.</param>
    /// <param name="httpClient">An <see cref="HttpClient"/> to use when making a request to the <paramref name="service"/>.</param>
    /// <param name="onCredentialsUpdated">An <see cref="Func{T1, T2, TResult}" /> to await if the credentials in the request need updating.</param>
    /// <param name="loggerFactory">An instance of <see cref="ILoggerFactory"/> to use to create a logger.</param>
    /// <param name="maximumResponseSize">The maximum number of bytes to read from the response body.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentException">Thrown when any of <paramref name="readState"/>, <paramref name="status"/>, <paramref name="kind"/> or <paramref name="lockStatus"/> is specified and is whitespace.</exception>
    /// <exception cref="ArgumentNullException">Thrown when any of <paramref name="accessCredentials"/>, <paramref name="service"/> or <paramref name="httpClient"/> are <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="limit"/>is &lt;1 or &gt; the maximum number of conversations to list.</exception>
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
        Justification = "All types are preserved in the JsonSerializerOptions call to Get().")]
    [UnconditionalSuppressMessage("AOT",
        "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
        Justification = "All types are preserved in the JsonSerializerOptions call to Get().")]
    public static async Task<AtProtoHttpResult<Conversations>> ListConversations(
        int? limit,
        string? cursor,
        string? readState,
        string? status,
        string? kind,
        string? lockStatus,
        Uri service,
        AccessCredentials accessCredentials,
        HttpClient httpClient,
        Func<AtProtoCredential, CancellationToken, Task>? onCredentialsUpdated = null,
        ILoggerFactory? loggerFactory = default,
        int maximumResponseSize = AtProtoHttpClient.DefaultMaximumResponseSize,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(service);
        ArgumentNullException.ThrowIfNull(accessCredentials);
        ArgumentNullException.ThrowIfNull(httpClient);

        if (limit is not null)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan((int)limit, 1);
            ArgumentOutOfRangeException.ThrowIfGreaterThan((int)limit, Maximum.ConversationsToList);
        }

        StringBuilder queryStringBuilder = new();

        if (limit is not null)
        {
            queryStringBuilder.Append(CultureInfo.InvariantCulture, $"limit={limit}");
        }

        AppendFilter("cursor", cursor);
        AppendFilter("readState", readState);
        AppendFilter("status", status);
        AppendFilter("kind", kind);
        AppendFilter("lockStatus", lockStatus);

        string queryString = queryStringBuilder.ToString();

        BlueskyHttpClient<ListConversationsResponse> client = new(ChatProxy, loggerFactory) { MaximumResponseSize = maximumResponseSize };

        AtProtoHttpResult<ListConversationsResponse> response = await client.Get(
            service,
            $"/xrpc/chat.bsky.convo.listConvos?{queryString}",
            credentials: accessCredentials,
            httpClient: httpClient,
            jsonSerializerOptions: BlueskyJsonSerializerOptions,
            onCredentialsUpdated: onCredentialsUpdated,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        if (response.Succeeded)
        {
            return new AtProtoHttpResult<Conversations>(
                new Conversations(WithoutNullEntries(response.Result.Conversations, service, nameof(response.Result.Conversations), loggerFactory), response.Result.Cursor),
                response.StatusCode,
                response.HttpResponseHeaders,
                response.AtErrorDetail,
                response.RateLimit);
        }
        else
        {
            return new AtProtoHttpResult<Conversations>(
                null,
                response.StatusCode,
                response.HttpResponseHeaders,
                response.AtErrorDetail,
                response.RateLimit);
        }

        void AppendFilter(string name, string? value)
        {
            if (value is null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException($"'{name}' cannot be empty or whitespace.", name);
            }

            if (queryStringBuilder.Length != 0)
            {
                queryStringBuilder.Append('&');
            }

            queryStringBuilder.Append(CultureInfo.InvariantCulture, $"{name}={Uri.EscapeDataString(value)}");
        }
    }

}