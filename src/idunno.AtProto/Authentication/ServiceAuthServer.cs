// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;

using idunno.AtProto.Authentication;

using Microsoft.Extensions.Logging;

namespace idunno.AtProto;

public static partial class AtProtoServer
{
    /// <summary>
    /// Gets service credentials bound to an exact DID service reference.
    /// </summary>
    /// <param name="audience">The destination service reference.</param>
    /// <param name="expiry">The requested token lifetime. The default is 15 minutes.</param>
    /// <param name="lxm">The XRPC method to bind the token to.</param>
    /// <param name="service">The server issuing the token.</param>
    /// <param name="accessCredentials">The requesting account's credentials.</param>
    /// <param name="httpClient">The HTTP client used to make the request.</param>
    /// <param name="credentialsUpdated">An optional callback for refreshed credentials.</param>
    /// <param name="loggerFactory">An optional logger factory.</param>
    /// <param name="maximumResponseSize">The maximum response size in bytes.</param>
    /// <param name="cancellationToken">A cancellation token for the request.</param>
    /// <returns>The service credential HTTP result.</returns>
    /// <exception cref="ArgumentNullException">A required argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The access token is empty or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The specified lifetime is zero or negative.</exception>
    [SuppressMessage("ApiDesign", "RS0026:Do not add multiple public overloads with optional parameters", Justification = "The required audience parameter distinguishes a service reference from a bare DID.")]
    public static Task<AtProtoHttpResult<ServiceCredential>> GetServiceAuth(
        AtProtoServiceReference audience,
        TimeSpan? expiry,
        Nsid lxm,
        Uri service,
        AccessCredentials accessCredentials,
        HttpClient httpClient,
        Func<AtProtoCredential, CancellationToken, Task>? credentialsUpdated = null,
        ILoggerFactory? loggerFactory = default,
        int maximumResponseSize = AtProtoHttpClient.DefaultMaximumResponseSize,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(audience);

        return GetServiceAuthWithTimeProvider(
            timeProvider: TimeProvider.System,
            audience: audience.ToString(),
            expiry: expiry,
            lxm: lxm,
            service: service,
            accessCredentials: accessCredentials,
            httpClient: httpClient,
            credentialsUpdated: credentialsUpdated,
            loggerFactory: loggerFactory,
            maximumResponseSize: maximumResponseSize,
            cancellationToken: cancellationToken);
    }
}
