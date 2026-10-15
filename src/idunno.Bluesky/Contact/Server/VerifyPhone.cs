// Copyright (c) Barry Dorrans. All rights reserved.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;

using idunno.AtProto;
using idunno.AtProto.Authentication;
using idunno.Bluesky.Contact;
using idunno.Bluesky.Contact.Model;

using Microsoft.Extensions.Logging;

namespace idunno.Bluesky;

public static partial class BlueskyServer
{
    /// <summary>
    /// Verifies a phone number and obtains a single-use contact import token.
    /// </summary>
    /// <param name="phone">The phone number passed to phone verification.</param>
    /// <param name="code">The verification code received by SMS.</param>
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
    /// <para>Requires authentication. Named errors: <c>RateLimitExceeded</c>, <c>InvalidDid</c>, <c>InvalidPhone</c>, <c>InvalidCode</c>, <c>InternalError</c>.</para>
    /// <para>The <paramref name="phone"/> number, verification codes and import tokens are sensitive. Do not log or persist request or response bodies.</para>
    /// </remarks>
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Contact request and response types are source-generated; ContactDiscoveryTests exercises this endpoint with reflection disabled.")]
    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = "Contact request and response types are source-generated; ContactDiscoveryTests exercises this endpoint with reflection disabled.")]
    public static async Task<AtProtoHttpResult<PhoneVerificationResult>> VerifyPhone(
        string phone,
        string code,
        Uri service,
        AccessCredentials accessCredentials,
        HttpClient httpClient,
        Func<AtProtoCredential, CancellationToken, Task>? onCredentialsUpdated = null,
        ILoggerFactory? loggerFactory = null,
        int maximumResponseSize = AtProtoHttpClient.DefaultMaximumResponseSize,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(phone);
        ArgumentNullException.ThrowIfNull(code);
        ArgumentNullException.ThrowIfNull(service);
        ArgumentNullException.ThrowIfNull(accessCredentials);
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumResponseSize);

        BlueskyHttpClient<PhoneVerificationResult> client = new(AppViewProxy, loggerFactory) { MaximumResponseSize = maximumResponseSize };
        AtProtoHttpResult<PhoneVerificationResult> response = await client.Post(
            service: service,
            endpoint: "/xrpc/app.bsky.contact.verifyPhone",
            record: new VerifyPhoneRequest(phone, code),
            credentials: accessCredentials,
            httpClient: httpClient,
            jsonSerializerOptions: BlueskyJsonSerializerOptions,
            onCredentialsUpdated: onCredentialsUpdated,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return response;
    }
}
