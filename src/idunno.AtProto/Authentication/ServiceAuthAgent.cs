// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;

using idunno.AtProto.Authentication;

namespace idunno.AtProto;

public partial class AtProtoAgent
{
    /// <summary>
    /// Gets service credentials bound to an exact DID service reference.
    /// </summary>
    /// <param name="service">The server issuing the token.</param>
    /// <param name="audience">The destination service reference.</param>
    /// <param name="lxm">The XRPC method to bind the token to.</param>
    /// <param name="expiry">The requested token lifetime. The default is 14 minutes 55 seconds, leaving five seconds of clock-skew headroom below a 15-minute verifier maximum.</param>
    /// <param name="cancellationToken">A cancellation token for the request.</param>
    /// <returns>The service credential HTTP result.</returns>
    /// <exception cref="ArgumentNullException">A required argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The specified lifetime is zero or negative.</exception>
    /// <exception cref="AuthenticationRequiredException">The agent is not authenticated.</exception>
    [SuppressMessage("ApiDesign", "RS0026:Do not add multiple public overloads with optional parameters", Justification = "The required audience parameter distinguishes a service reference from a bare DID.")]
    public Task<AtProtoHttpResult<ServiceCredential>> GetServiceAuth(
        Uri service,
        AtProtoServiceReference audience,
        Nsid lxm,
        TimeSpan? expiry = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(service);
        ArgumentNullException.ThrowIfNull(audience);
        ArgumentNullException.ThrowIfNull(lxm);
        if (expiry is not null)
        {
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(expiry.Value.TotalSeconds, 0);
        }

        if (!IsAuthenticated)
        {
            throw new AuthenticationRequiredException();
        }

        return AtProtoServer.GetServiceAuthWithTimeProvider(
            timeProvider: Clock,
            audience: audience.ToString(),
            expiry: expiry,
            lxm: lxm,
            service: service,
            accessCredentials: Credentials,
            httpClient: HttpClient,
            credentialsUpdated: InternalOnCredentialsUpdatedCallBack,
            loggerFactory: LoggerFactory,
            maximumResponseSize: MaximumResponseSize,
            cancellationToken: cancellationToken);
    }
}
