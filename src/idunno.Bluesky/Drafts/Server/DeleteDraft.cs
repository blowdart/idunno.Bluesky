// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;

using idunno.AtProto;
using idunno.AtProto.Authentication;
using idunno.Bluesky.Drafts.Model;

using Microsoft.Extensions.Logging;

namespace idunno.Bluesky;

public partial class BlueskyServer
{
    /// <summary>
    /// Deletes a draft by ID.
    /// </summary>
    /// <param name="draftId">The ID of the draft to delete.</param>
    /// <param name="service">The <see cref="Uri"/> of the service to retrieve the profile from.</param>
    /// <param name="accessCredentials">The <see cref="AccessCredentials"/> used to authenticate to <paramref name="service"/>.</param>
    /// <param name="httpClient">An <see cref="HttpClient"/> to use when making a request to the <paramref name="service"/>.</param>
    /// <param name="onCredentialsUpdated">An <see cref="Action{T}" /> to call if the credentials in the request need updating.</param>
    /// <param name="loggerFactory">An instance of <see cref="ILoggerFactory"/> to use to create a logger.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when any of <paramref name="draftId"/>, <paramref name="service"/>, <paramref name="accessCredentials"/> or <paramref name="httpClient"/> are <see langword="null"/>.</exception>
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
        Justification = "All types are preserved in the JsonSerializerOptions call to Get().")]
    [UnconditionalSuppressMessage("AOT",
        "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
        Justification = "All types are preserved in the JsonSerializerOptions call to Get().")]
    public static async Task<AtProtoHttpResult<EmptyResponse>> DeleteDraft(
        TimestampIdentifier draftId,
        Uri service,
        AccessCredentials accessCredentials,
        HttpClient httpClient,
        Action<AtProtoCredential>? onCredentialsUpdated = null,
        ILoggerFactory? loggerFactory = default,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(draftId);
        ArgumentNullException.ThrowIfNull(service);
        ArgumentNullException.ThrowIfNull(accessCredentials);
        ArgumentNullException.ThrowIfNull(httpClient);

        BlueskyHttpClient<EmptyResponse> request = new(AppViewProxy, loggerFactory);

        AtProtoHttpResult<EmptyResponse> result = await request.Post(
            service,
            record: new DeleteDraftRequest(draftId),
            endpoint: $"/xrpc/app.bsky.draft.deleteDraft?id={draftId}",
            credentials: accessCredentials,
            httpClient: httpClient,
            jsonSerializerOptions: BlueskyJsonSerializerOptions,
            onCredentialsUpdated: onCredentialsUpdated,
            subscribedLabelers: null,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        if (result.Succeeded)
        {
            return new AtProtoHttpResult<EmptyResponse>(
                new EmptyResponse(),
                result.StatusCode,
                result.HttpResponseHeaders,
                result.AtErrorDetail,
                result.RateLimit);
        }
        else
        {
            return new AtProtoHttpResult<EmptyResponse>(
                null,
                result.StatusCode,
                result.HttpResponseHeaders,
                result.AtErrorDetail,
                result.RateLimit);
        }
    }
}
