// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;

using idunno.AtProto;
using idunno.AtProto.Authentication;
using idunno.Bluesky.Actor;
using idunno.Bluesky.Chat;
using idunno.Bluesky.Chat.Model;

using Microsoft.Extensions.Logging;

namespace idunno.Bluesky;

public static partial class BlueskyServer
{
    /// <summary>
    /// Enumerates the conversation log.
    /// </summary>
    /// <param name="cursor">An optional cursor used for pagination.</param>
    /// <param name="service">The <see cref="Uri"/> of the service to retrieve the conversation log from.</param>
    /// <param name="accessCredentials">The <see cref="AccessCredentials"/> to use when accessing the <paramref name="service"/>.</param>
    /// <param name="httpClient">An <see cref="HttpClient"/> to use when making a request to the <paramref name="service"/>.</param>
    /// <param name="onCredentialsUpdated">An <see cref="Action{T}" /> to call if the credentials in the request need updating.</param>
    /// <param name="loggerFactory">An instance of <see cref="ILoggerFactory"/> to use to create a logger.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="accessCredentials"/>, <paramref name="service"/> or <paramref name="httpClient"/> is <see langword="null"/>.</exception>
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
        Justification = "All types are preserved in the JsonSerializerOptions call to Get().")]
    [UnconditionalSuppressMessage("AOT",
        "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
        Justification = "All types are preserved in the JsonSerializerOptions call to Get().")]
    public static async Task<AtProtoHttpResult<Logs>> GetConversationLog(
        string? cursor,
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

        string queryString = string.Empty;
        if (cursor != null)
        {
            queryString = $"?cursor={Uri.EscapeDataString(cursor)}";
        }

        BlueskyHttpClient<GetLogResponse> client = new(ChatProxy, loggerFactory);

        AtProtoHttpResult<GetLogResponse> response = await client.Get(
            service,
            $"xrpc/chat.bsky.convo.getLog{queryString}",
            credentials: accessCredentials,
            httpClient: httpClient,
            jsonSerializerOptions: BlueskyJsonSerializerOptions,
            onCredentialsUpdated: onCredentialsUpdated,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        if (response.Succeeded)
        {
            return new AtProtoHttpResult<Logs>(
                new Logs(response.Result.Logs, response.Result.Cursor),
                response.StatusCode,
                response.HttpResponseHeaders,
                response.AtErrorDetail,
                response.RateLimit);
        }
        else
        {
            return new AtProtoHttpResult<Logs>(
                null,
                response.StatusCode,
                response.HttpResponseHeaders,
                response.AtErrorDetail,
                response.RateLimit);
        }
    }
}
