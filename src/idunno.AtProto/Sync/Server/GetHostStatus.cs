// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;

using idunno.AtProto.Authentication;
using idunno.AtProto.Sync;

using Microsoft.Extensions.Logging;

namespace idunno.AtProto;

public static partial class AtProtoServer
{
    /// <summary>
    /// Gets information about an upstream host, such as a personal data server, as consumed by a relay.
    /// </summary>
    /// <param name="hostname">The hostname of the upstream host being queried.</param>
    /// <param name="service">The relay to query.</param>
    /// <param name="httpClient">An <see cref="HttpClient"/> to use when making a request to the <paramref name="service"/>.</param>
    /// <param name="accessCredentials">Optional access credentials for the specified <paramref name="service"/>.</param>
    /// <param name="onCredentialsUpdated">An <see cref="Func{T1, T2, TResult}" /> to await if the credentials in the request need updating.</param>
    /// <param name="loggerFactory">An instance of <see cref="ILoggerFactory"/> to use to create a logger.</param>
    /// <param name="maximumResponseSize">The maximum number of bytes to read from the response body.</param>
    /// <param name="cancellationToken">A cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="hostname"/>, <paramref name="service"/> or <paramref name="httpClient"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="hostname"/> is empty or white space.</exception>
    /// <exception cref="AccessTokenException">Thrown when <paramref name="accessCredentials"/> are not valid for the specified <paramref name="service"/>.</exception>
    /// <remarks>
    /// <para>
    /// This endpoint is only implemented by relays. Personal data servers do not implement it and will return an error,
    /// so <paramref name="service"/> should be a relay.
    /// </para>
    /// <para>A relay which does not know the host returns a <c>HostNotFound</c> error.</para>
    /// </remarks>
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
        Justification = "All types are preserved in the JsonSerializerOptions call to Get().")]
    [UnconditionalSuppressMessage("AOT",
        "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
        Justification = "All types are preserved in the JsonSerializerOptions call to Get().")]
    public static async Task<AtProtoHttpResult<HostDescription>> GetHostStatus(
        string hostname,
        Uri service,
        HttpClient httpClient,
        AccessCredentials? accessCredentials = null,
        Func<AtProtoCredential, CancellationToken, Task>? onCredentialsUpdated = null,
        ILoggerFactory? loggerFactory = default,
        int maximumResponseSize = AtProtoHttpClient.DefaultMaximumResponseSize,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(hostname);
        ArgumentNullException.ThrowIfNull(service);
        ArgumentNullException.ThrowIfNull(httpClient);

        if (accessCredentials is not null && service != accessCredentials.Service)
        {
            throw new AccessTokenException("Credentials not valid for the specified service.");
        }

        AtProtoHttpClient<HostDescription> request = new(loggerFactory) { MaximumResponseSize = maximumResponseSize };

        return await request.Get(
            service: service,
            endpoint: $"/xrpc/com.atproto.sync.getHostStatus?hostname={Uri.EscapeDataString(hostname)}",
            credentials: accessCredentials,
            httpClient: httpClient,
            jsonSerializerOptions: AtProtoJsonSerializerOptions,
            onCredentialsUpdated: onCredentialsUpdated,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}
