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

public partial class BlueskyServer
{
    /// <summary>
    /// Returns a page of incoming conversation requests for the user.
    /// Direct conversation requests are returned as <see cref="ConversationView"/>;
    /// group join requests made by the user are returned as <see cref="Chat.Group.JoinRequestConversationView"/>.
    /// </summary>
    /// <param name="limit">The maximum number of requests to return. Must be between 1 and 100.</param>
    /// <param name="cursor">The cursor to use for pagination.</param>
    /// <param name="service">The <see cref="Uri"/> of the service to call.</param>
    /// <param name="accessCredentials">The <see cref="AccessCredentials"/> used to authenticate to <paramref name="service"/>.</param>
    /// <param name="httpClient">An <see cref="HttpClient"/> to use when making a request to the <paramref name="service"/>.</param>
    /// <param name="onCredentialsUpdated">An <see cref="Action{T}" /> to call if the credentials in the request need updating.</param>
    /// <param name="loggerFactory">An instance of <see cref="ILoggerFactory"/> to use to create a logger.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="service"/>, <paramref name="accessCredentials"/>, or <paramref name="httpClient"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="limit"/> is out of range.</exception>
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
        Justification = "All types are preserved in the JsonSerializerOptions call to Post().")]
    [UnconditionalSuppressMessage("AOT",
        "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
        Justification = "All types are preserved in the JsonSerializerOptions call to Post().")]
    public static async Task<AtProtoHttpResult<PagedViewReadOnlyCollection<ConversationViewBase>>> ListConversationRequests(
        int? limit,
        string? cursor,
        Uri service,
        AccessCredentials accessCredentials,
        HttpClient httpClient,
        Action<AtProtoCredential>? onCredentialsUpdated = null,
        ILoggerFactory? loggerFactory = default,
        CancellationToken cancellationToken = default)
    {
        if (limit.HasValue)
        {
            ArgumentOutOfRangeException.ThrowIfZero(limit.Value);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(limit.Value, 100);
        }

        ArgumentNullException.ThrowIfNull(service);
        ArgumentNullException.ThrowIfNull(accessCredentials);
        ArgumentNullException.ThrowIfNull(httpClient);

        StringBuilder queryStringBuilder = new ();
        if (limit.HasValue)
        {
            queryStringBuilder.Append(CultureInfo.InvariantCulture,$"limit={limit.Value}");
        }

        string queryString = queryStringBuilder.ToString();

        BlueskyHttpClient<Chat.Convo.Model.ListConvoRequestsResponse> client = new(ChatProxy, loggerFactory);

        AtProtoHttpResult<Chat.Convo.Model.ListConvoRequestsResponse> response = await client.Get(
            service,
            $"/xrpc/chat.bsky.convo.listConvoRequests?{queryString}",
            credentials: accessCredentials,
            httpClient: httpClient,
            jsonSerializerOptions: BlueskyJsonSerializerOptions,
            onCredentialsUpdated: onCredentialsUpdated,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        if (response.Succeeded)
        {
            return new AtProtoHttpResult<PagedViewReadOnlyCollection<ConversationViewBase>>(
                new PagedViewReadOnlyCollection<ConversationViewBase>(response.Result.Requests, response.Result.Cursor),
                statusCode: response.StatusCode,
                httpResponseHeaders: response.HttpResponseHeaders,
                atErrorDetail: response.AtErrorDetail,
                rateLimit: response.RateLimit);
        }
        else
        {
            return new AtProtoHttpResult<PagedViewReadOnlyCollection<ConversationViewBase>>(
                result: null,
                statusCode: response.StatusCode,
                httpResponseHeaders: response.HttpResponseHeaders,
                atErrorDetail: response.AtErrorDetail,
                rateLimit: response.RateLimit);
        }
    }
}
