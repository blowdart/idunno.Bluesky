// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

namespace idunno.AtProto;

public partial class AtProtoAgent
{
    /// <summary>
    /// Imports the current account's repository from a <paramref name="car"/> file.
    /// </summary>
    /// <param name="car">The repository CAR file contents.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the request.</param>
    /// <returns>The result of the import request.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="car"/> is empty.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="car"/> is <see langword="null"/>.</exception>
    /// <exception cref="AuthenticationRequiredException">Thrown when the agent is not authenticated.</exception>
    public async Task<AtProtoHttpResult<EmptyResponse>> ImportRepo(
        byte[] car,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(car);
        ArgumentOutOfRangeException.ThrowIfZero(car.Length);

        if (!IsAuthenticated)
        {
            throw new AuthenticationRequiredException();
        }

        return await AtProtoServer.ImportRepo(
            car,
            service: Service,
            accessCredentials: Credentials,
            httpClient: HttpClient,
            onCredentialsUpdated: InternalOnCredentialsUpdatedCallBack,
            loggerFactory: LoggerFactory,
            maximumResponseSize: MaximumResponseSize,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}
