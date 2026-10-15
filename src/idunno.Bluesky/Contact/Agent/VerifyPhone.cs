// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using idunno.AtProto;
using idunno.Bluesky.Contact;

namespace idunno.Bluesky;

public partial class BlueskyAgent
{
    /// <summary>
    /// Verifies a phone number and obtains a single-use contact import token.
    /// </summary>
    /// <param name="phone">The phone number passed to phone verification.</param>
    /// <param name="code">The verification code received by SMS.</param>
    /// <param name="cancellationToken">A cancellation token for the operation.</param>
    /// <returns>The task representing the authenticated contact request.</returns>
    /// <exception cref="ArgumentNullException">An argument documented as required is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The maximum response size is not positive.</exception>
    /// <exception cref="AuthenticationRequiredException">The agent is not authenticated.</exception>
    /// <remarks>
    /// <para>Requires authentication. Named errors: <c>RateLimitExceeded</c>, <c>InvalidDid</c>, <c>InvalidPhone</c>, <c>InvalidCode</c>, <c>InternalError</c>.</para>
    /// <para>The <paramref name="phone"/> number, verification codes and import tokens are sensitive. Do not log or persist request or response bodies.</para>
    /// </remarks>
    public async Task<AtProtoHttpResult<PhoneVerificationResult>> VerifyPhone(
        string phone,
        string code,
        CancellationToken cancellationToken = default)
    {
        if (!IsAuthenticated)
        {
            throw new AuthenticationRequiredException();
        }

        return await BlueskyServer.VerifyPhone(
            phone: phone,
            code: code,
            service: Service,
            accessCredentials: Credentials,
            httpClient: HttpClient,
            onCredentialsUpdated: InternalOnCredentialsUpdatedCallBack,
            loggerFactory: LoggerFactory,
            maximumResponseSize: MaximumResponseSize,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}
