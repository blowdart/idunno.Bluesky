// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;

using idunno.AtProto;
using idunno.AtProto.Authentication;
using idunno.Bluesky.Chat.Group.Model;

using Microsoft.Extensions.Logging;

namespace idunno.Bluesky;

public partial class BlueskyServer
{
    /// <summary>
    /// Get public information about groups from join links. The output array matches the input codes one-to-one by position (and each view also carries its 'code'). Disabled codes return a disabledJoinLinkPreviewView, and codes that do not map to a previewable link return an invalidJoinLinkPreviewView.
    /// </summary>
    /// <param name="codes">The codes of the group conversations to get join link previews for.</param>
    /// <param name="accessCredentials">The <see cref="AccessCredentials"/> used to authenticate to the service.</param>
    /// <param name="service">The <see cref="Uri"/> of the service to use when making a request.</param>
    /// <param name="httpClient">An <see cref="HttpClient"/> to use when making a request to the service.</param>
    /// <param name="onCredentialsUpdated">An <see cref="Action{T}" /> to call if the credentials in the request need updating.</param>
    /// <param name="loggerFactory">An instance of <see cref="ILoggerFactory"/> to use to create a logger.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="codes"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="codes"/> is empty.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="codes"/> contains more than 50 items.</exception>
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
        Justification = "All types are preserved in the JsonSerializerOptions call to Post().")]
    [UnconditionalSuppressMessage("AOT",
        "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
        Justification = "All types are preserved in the JsonSerializerOptions call to Post().")]
    public static async Task<AtProtoHttpResult<GetJoinLinkPreviewsResponse>> GetJoinLinkPreviews(
        ICollection<string> codes,
        AccessCredentials? accessCredentials,
        Uri service,
        HttpClient httpClient,
        Action<AtProtoCredential>? onCredentialsUpdated = null,
        ILoggerFactory? loggerFactory = default,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(codes);
        ArgumentOutOfRangeException.ThrowIfZero(codes.Count);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(codes.Count, 50);

        string queryString = string.Join("&", codes.Select(code => $"codes={Uri.EscapeDataString(code)}"));

        AtProtoHttpClient<GetJoinLinkPreviewsResponse> client = new(ChatProxy, loggerFactory);
        AtProtoHttpResult<GetJoinLinkPreviewsResponse> result = await client.Get(
            service,
            $"/xrpc/chat.bsky.group.getJoinLinkPreviews?{queryString}",
            credentials: accessCredentials,
            httpClient: httpClient,
            jsonSerializerOptions: BlueskyJsonSerializerOptions,
            onCredentialsUpdated: onCredentialsUpdated,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        if (!result.Succeeded && result.AtErrorDetail is not null)
        {
            result = new AtProtoHttpResult<GetJoinLinkPreviewsResponse>(
                result: result.Result,
                statusCode: result.StatusCode,
                httpResponseHeaders: result.HttpResponseHeaders,
                atErrorDetail: BlueskyError.Map(result.AtErrorDetail),
                rateLimit: result.RateLimit);
        }

        return result;
    }
}
