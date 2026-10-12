// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;
using System.Net.Http.Headers;

using idunno.AtProto.Authentication;

using Microsoft.Extensions.Logging;

namespace idunno.AtProto;

public static partial class AtProtoServer
{
    /// <summary>
    /// Imports a repository from a <paramref name="car"/> file.
    /// </summary>
    /// <param name="car">The repository CAR file contents.</param>
    /// <param name="service">The personal data server hosting the account.</param>
    /// <param name="accessCredentials">The credentials used to authenticate to <paramref name="service"/>.</param>
    /// <param name="httpClient">The HTTP client to use when making the request.</param>
    /// <param name="onCredentialsUpdated">A callback to invoke if the credentials are updated during the request.</param>
    /// <param name="loggerFactory">An optional logger factory.</param>
    /// <param name="maximumResponseSize">The maximum number of bytes to read from the response body.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the request.</param>
    /// <returns>The result of the import request.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="car"/> is empty.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="car"/>, <paramref name="service"/>, <paramref name="accessCredentials"/> or <paramref name="httpClient"/> is <see langword="null"/>.</exception>
    /// <exception cref="AccessTokenException">Thrown when <paramref name="accessCredentials"/> are not valid for <paramref name="service"/>.</exception>
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
        Justification = "All types are preserved in the JsonSerializerOptions call to PostBlob().")]
    [UnconditionalSuppressMessage("AOT",
        "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
        Justification = "All types are preserved in the JsonSerializerOptions call to PostBlob().")]
    public static async Task<AtProtoHttpResult<EmptyResponse>> ImportRepo(
        byte[] car,
        Uri service,
        AccessCredentials accessCredentials,
        HttpClient httpClient,
        Func<AtProtoCredential, CancellationToken, Task>? onCredentialsUpdated = null,
        ILoggerFactory? loggerFactory = null,
        int maximumResponseSize = AtProtoHttpClient.DefaultMaximumResponseSize,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(car);
        ArgumentNullException.ThrowIfNull(service);
        ArgumentNullException.ThrowIfNull(accessCredentials);
        ArgumentNullException.ThrowIfNull(httpClient);

        ArgumentOutOfRangeException.ThrowIfZero(car.Length);

        if (accessCredentials.Service != service)
        {
            throw new AccessTokenException("Credentials not valid for the specified service.");
        }

        List<NameValueHeaderValue> contentHeaders =
        [
            new NameValueHeaderValue("Content-Type", "application/vnd.ipld.car")
        ];

        AtProtoHttpClient<EmptyResponse> client = new(loggerFactory) { MaximumResponseSize = maximumResponseSize };

        return await client.PostBlob(
            service: service,
            endpoint: "/xrpc/com.atproto.repo.importRepo",
            blob: car,
            requestHeaders: null,
            contentHeaders: contentHeaders,
            credentials: accessCredentials,
            httpClient: httpClient,
            jsonSerializerOptions: AtProtoJsonSerializerOptions,
            onCredentialsUpdated: onCredentialsUpdated,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}
