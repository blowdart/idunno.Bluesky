// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;

using idunno.AtProto;
using idunno.AtProto.Authentication;
using idunno.Bluesky.Graph.Model;

using Microsoft.Extensions.Logging;

namespace idunno.Bluesky;

public partial class BlueskyServer
{
    /// <summary>
    /// Creates or updates a mute relationship for the specified account.
    /// If a mute already exists for the account, it is updated in place: the stored scope is replaced with the scope in this request.
    /// Requires authentication.
    /// </summary>
    /// <param name="actor">The <see cref="AtIdentifier"/> of the actor to mute</param>
    /// <param name="onlyReposts">Flag indicating whether to restrict the mute to the account's reposts. When <see langword="true"/>, just the scoped content is muted; when no scoped mutes are set the account is fully muted.</param>
    /// <param name="onlyQuotePosts">Flag indicating whether to restrict the mute to the account's quotes. When <see langword="true"/>, just the scoped content is muted; when no scoped mutes are set the account is fully muted.</param>
    /// <param name="service">The <see cref="Uri"/> of the service cerate the mute on.</param>
    /// <param name="accessCredentials">The <see cref="AccessCredentials"/> used to authenticate to <paramref name="service"/>.</param>
    /// <param name="httpClient">An <see cref="HttpClient"/> to use when making a request to the <paramref name="service"/>.</param>
    /// <param name="onCredentialsUpdated">An <see cref="Action{T}" /> to call if the credentials in the request need updating.</param>
    /// <param name="loggerFactory">An instance of <see cref="ILoggerFactory"/> to use to create a logger.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when any of <paramref name="actor"/>, <paramref name="service"/>, <paramref name="accessCredentials"/> or <paramref name="httpClient"/> are <see langword="null"/>.</exception>
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
        Justification = "All types are preserved in the JsonSerializerOptions call to Post().")]
    [UnconditionalSuppressMessage("AOT",
        "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
        Justification = "All types are preserved in the JsonSerializerOptions call to Post().")]
    public static async Task<AtProtoHttpResult<EmptyResponse>> MuteActor(
        AtIdentifier actor,
        bool? onlyReposts,
        bool? onlyQuotePosts,
        Uri service,
        AccessCredentials accessCredentials,
        HttpClient httpClient,
        Action<AtProtoCredential>? onCredentialsUpdated = null,
        ILoggerFactory? loggerFactory = default,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(service);
        ArgumentNullException.ThrowIfNull(accessCredentials);
        ArgumentNullException.ThrowIfNull(httpClient);

        BlueskyHttpClient<EmptyResponse> client = new(AppViewProxy, loggerFactory);
        AtProtoHttpResult<EmptyResponse> response = await client.Post(
            service,
            $"/xrpc/app.bsky.graph.muteActor",
            new MuteActorRequest(actor)
            {
                OnlyReposts = onlyReposts,
                OnlyQuoteposts = onlyQuotePosts
            },
            credentials: accessCredentials,
            httpClient: httpClient,
            jsonSerializerOptions: BlueskyJsonSerializerOptions,
            onCredentialsUpdated: onCredentialsUpdated,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return response;
    }
}
