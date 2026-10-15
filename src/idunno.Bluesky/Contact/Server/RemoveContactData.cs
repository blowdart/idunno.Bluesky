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
    /// Removes stored contact hashes, matches and synchronization status.
    /// </summary>
    /// <param name="service">The personal data server to proxy the request through.</param>
    /// <param name="accessCredentials">The credentials used to authenticate the request.</param>
    /// <param name="httpClient">The HTTP client used to make the request.</param>
    /// <param name="onCredentialsUpdated">An optional callback to persist updated credentials.</param>
    /// <param name="loggerFactory">An optional factory for request logging.</param>
    /// <param name="maximumResponseSize">The maximum response body size in bytes.</param>
    /// <param name="cancellationToken">A cancellation token for the operation.</param>
    /// <returns>The task representing the authenticated contact request.</returns>
    /// <exception cref="ArgumentNullException">An argument documented as required is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The maximum response size is not positive.</exception>
    /// <remarks>
    /// <para>Requires authentication. Named errors: <c>InvalidDid</c>, <c>InternalError</c>.</para>
    /// </remarks>
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Contact request and response types are source-generated; ContactDiscoveryTests exercises this endpoint with reflection disabled.")]
    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = "Contact request and response types are source-generated; ContactDiscoveryTests exercises this endpoint with reflection disabled.")]
    public static async Task<AtProtoHttpResult<EmptyResponse>> RemoveContactData(
        Uri service,
        AccessCredentials accessCredentials,
        HttpClient httpClient,
        Func<AtProtoCredential, CancellationToken, Task>? onCredentialsUpdated = null,
        ILoggerFactory? loggerFactory = null,
        int maximumResponseSize = AtProtoHttpClient.DefaultMaximumResponseSize,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(service);
        ArgumentNullException.ThrowIfNull(accessCredentials);
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumResponseSize);

        BlueskyHttpClient<EmptyResponse> client = new(AppViewProxy, loggerFactory) { MaximumResponseSize = maximumResponseSize };
        AtProtoHttpResult<EmptyResponse> response = await client.Post(
            service: service,
            endpoint: "/xrpc/app.bsky.contact.removeData",
            record: new EmptyResponse(),
            credentials: accessCredentials,
            httpClient: httpClient,
            jsonSerializerOptions: BlueskyJsonSerializerOptions,
            onCredentialsUpdated: onCredentialsUpdated,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return response;
    }
}
