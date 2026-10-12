// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;

using idunno.AtProto;
using idunno.AtProto.Authentication;

using Microsoft.Extensions.Logging;

namespace idunno.Bluesky;

public static partial class BlueskyServer
{
    /// <summary>
    /// Exports the authenticated user's chat account data as JSON Lines.
    /// </summary>
    /// <param name="service">The service to export the account data from.</param>
    /// <param name="accessCredentials">The credentials used to authenticate to <paramref name="service"/>.</param>
    /// <param name="httpClient">The HTTP client to use when making the request.</param>
    /// <param name="onCredentialsUpdated">A callback to invoke if the credentials are updated during the request.</param>
    /// <param name="loggerFactory">A logger factory to use when creating a logger.</param>
    /// <param name="maximumResponseSize">The maximum number of bytes to read from an error response. Successful exports are streamed without this limit.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the request.</param>
    /// <returns>A result containing the JSON Lines stream. The stream owns the response and must be disposed.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="service"/>, <paramref name="accessCredentials"/> or <paramref name="httpClient"/> is <see langword="null"/>.</exception>
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
        Justification = "Successful exports are raw streams; error types are preserved by the supplied source-generated JsonSerializerOptions.")]
    [UnconditionalSuppressMessage("AOT",
        "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
        Justification = "Successful exports are raw streams; error types are preserved by the supplied source-generated JsonSerializerOptions.")]
    public static async Task<AtProtoHttpResult<Stream>> ExportChatAccountData(
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

        BlueskyHttpClient<Stream> client = new(ChatProxy, loggerFactory) { MaximumResponseSize = maximumResponseSize };

        return await client.Get(
            service,
            "/xrpc/chat.bsky.actor.exportAccountData",
            credentials: accessCredentials,
            httpClient: httpClient,
            onCredentialsUpdated: onCredentialsUpdated,
            jsonSerializerOptions: BlueskyJsonSerializerOptions,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}
