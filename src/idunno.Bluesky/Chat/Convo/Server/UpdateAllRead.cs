// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;

using idunno.AtProto;
using idunno.AtProto.Authentication;
using idunno.Bluesky.Chat;
using idunno.Bluesky.Chat.Convo.Model;
using idunno.Bluesky.Chat.Model;

using Microsoft.Extensions.Logging;

namespace idunno.Bluesky;

public static partial class BlueskyServer
{
    /// <summary>
    /// Marks all conversations with the specified <paramref name="status"/> as read.
    /// </summary>
    /// <param name="status">The status of the conversations to mark as read. Known values are held in <see cref="ConversationStatus"/>.</param>
    /// <param name="service">The <see cref="Uri"/> of the service to mark the conversation on.</param>
    /// <param name="accessCredentials">The <see cref="AccessCredentials"/> to use when accessing the <paramref name="service"/>.</param>
    /// <param name="httpClient">An <see cref="HttpClient"/> to use when making a request to the <paramref name="service"/>.</param>
    /// <param name="onCredentialsUpdated">An <see cref="Action{T}" /> to call if the credentials in the request need updating.</param>
    /// <param name="loggerFactory">An instance of <see cref="ILoggerFactory"/> to use to create a logger.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="status"/>, <paramref name="accessCredentials"/>, <paramref name="service"/> or <paramref name="httpClient"/> is <see langword="null"/>.
    /// </exception>
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
        Justification = "All types are preserved in the JsonSerializerOptions call to Post().")]
    [UnconditionalSuppressMessage("AOT",
        "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
        Justification = "All types are preserved in the JsonSerializerOptions call to Post().")]
    public static async Task<AtProtoHttpResult<ulong>> UpdateAllRead(
        string status,
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

        BlueskyHttpClient<UpdateAllReadResponse> client = new(ChatProxy, loggerFactory);

        UpdateAllReadRequest request = new(status);

        AtProtoHttpResult<UpdateAllReadResponse> response = await client.Post(
            service,
            "/xrpc/chat.bsky.convo.updateAllRead",
            request,
            credentials: accessCredentials,
            httpClient: httpClient,
            jsonSerializerOptions: BlueskyJsonSerializerOptions,
            onCredentialsUpdated: onCredentialsUpdated,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        if (response.Succeeded)
        {
            return new AtProtoHttpResult<ulong>(
                response.Result.UpdatedCount,
                response.StatusCode,
                response.HttpResponseHeaders,
                response.AtErrorDetail,
                response.RateLimit);
        }
        else
        {
            return new AtProtoHttpResult<ulong>(
                0,
                response.StatusCode,
                response.HttpResponseHeaders,
                response.AtErrorDetail,
                response.RateLimit);
        }
    }

}
